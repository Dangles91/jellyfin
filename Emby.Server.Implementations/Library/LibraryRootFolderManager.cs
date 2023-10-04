using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.Playlists;
using MediaBrowser.Common.Progress;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Library
{
    // TODO: Dangles: eventual circular dependecy between ItemService and this.
    public class LibraryRootFolderManager : ILibraryRootFolderManager
    {
        private readonly IServerConfigurationManager _serverConfigurationManager;
        private readonly ILibraryItemIdGenerator _libraryItemIdGenerator;
        private readonly IItemRepository _itemRepository;
        private readonly IFileSystem _fileSystem;
        private readonly IItemPathResolver _itemPathResolver;
        private readonly ILogger<LibraryRootFolderManager> _logger;

        /// <summary>
        /// The _root folder sync lock.
        /// </summary>
        private readonly object _rootFolderSyncLock = new();
        private readonly object _userRootFolderSyncLock = new();

        /// <summary>
        /// The _root folder.
        /// </summary>
        private volatile AggregateFolder? _rootFolder;
        private volatile UserRootFolder? _userRootFolder;

        /// <summary>
        /// Initializes a new instance of the <see cref="LibraryRootFolderManager"/> class.
        /// </summary>
        /// <param name="serverConfigurationManager">x.</param>
        /// <param name="libraryItemIdGenerator">xx.</param>
        /// <param name="itemRepository">xxx.</param>
        /// <param name="loggerFactory">xxxx.</param>
        /// <param name="fileSystem">xxxxx.</param>
        /// <param name="itemPathResolver">The item path resolver.</param>
        public LibraryRootFolderManager(
            IServerConfigurationManager serverConfigurationManager,
            ILibraryItemIdGenerator libraryItemIdGenerator,
            IItemRepository itemRepository,
            ILoggerFactory loggerFactory,
            IFileSystem fileSystem,
            IItemPathResolver itemPathResolver)
        {
            _serverConfigurationManager = serverConfigurationManager;
            _libraryItemIdGenerator = libraryItemIdGenerator;
            _itemRepository = itemRepository;
            _fileSystem = fileSystem;
            _itemPathResolver = itemPathResolver;
            _logger = loggerFactory.CreateLogger<LibraryRootFolderManager>();
        }

        public AggregateFolder GetRootFolder()
        {
            if (_rootFolder is null)
            {
                lock (_rootFolderSyncLock)
                {
                    _rootFolder ??= CreateRootFolder();
                }
            }

            return _rootFolder;
        }

        public Folder GetUserRootFolder()
        {
            if (_userRootFolder is null)
            {
                lock (_userRootFolderSyncLock)
                {
                    if (_userRootFolder is null)
                    {
                        var userRootPath = _serverConfigurationManager.ApplicationPaths.DefaultUserViewsPath;

                        _logger.LogDebug("Creating userRootPath at {Path}", userRootPath);
                        Directory.CreateDirectory(userRootPath);

                        var newItemId = _libraryItemIdGenerator.Generate(userRootPath, typeof(UserRootFolder));
                        UserRootFolder? tmpItem = null;
                        try
                        {
                            tmpItem = _itemRepository.RetrieveItem(newItemId) as UserRootFolder;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error creating UserRootFolder {Path}", newItemId);
                        }

                        if (tmpItem is null)
                        {
                            _logger.LogDebug("Creating new userRootFolder with DeepCopy");
                            tmpItem = (_itemPathResolver.ResolvePath(_fileSystem.GetDirectoryInfo(userRootPath)) as Folder)?.DeepCopy<Folder, UserRootFolder>();
                        }

                        // In case program data folder was moved
                        if (!string.Equals(tmpItem!.Path, userRootPath, StringComparison.Ordinal))
                        {
                            _logger.LogInformation("Resetting user root folder path to {0}", userRootPath);
                            tmpItem.Path = userRootPath;
                        }

                        _userRootFolder = tmpItem;
                        _logger.LogDebug("Setting userRootFolder: {Folder}", _userRootFolder);
                    }
                }
            }

            return _userRootFolder;
        }

        /// <summary>
        /// Creates the root media folder.
        /// </summary>
        /// <returns>AggregateFolder.</returns>
        /// <exception cref="InvalidOperationException">Cannot create the root folder until plugins have loaded.</exception>
        public AggregateFolder CreateRootFolder()
        {
            var rootFolderPath = _serverConfigurationManager.ApplicationPaths.RootFolderPath;

            Directory.CreateDirectory(rootFolderPath);

            var rootFolder = _itemRepository.RetrieveItem(_libraryItemIdGenerator.Generate(rootFolderPath, typeof(AggregateFolder))) as AggregateFolder ??
                             (_itemPathResolver.ResolvePath(_fileSystem.GetDirectoryInfo(rootFolderPath)) as Folder)?
                             .DeepCopy<Folder, AggregateFolder>();

            // In case program data folder was moved
            if (!string.Equals(rootFolder!.Path, rootFolderPath, StringComparison.Ordinal))
            {
                _logger.LogInformation("Resetting root folder path to {0}", rootFolderPath);
                rootFolder.Path = rootFolderPath;
            }

            // Add in the plug-in folders
            var path = Path.Combine(_serverConfigurationManager.ApplicationPaths.DataPath, "playlists");

            Directory.CreateDirectory(path);

            Folder folder = new PlaylistsFolder
            {
                Path = path
            };

            if (folder.Id.Equals(Guid.Empty))
            {
                if (string.IsNullOrEmpty(folder.Path))
                {
                    folder.Id = _libraryItemIdGenerator.Generate(folder.GetType().Name, folder.GetType());
                }
                else
                {
                    folder.Id = _libraryItemIdGenerator.Generate(folder.Path, folder.GetType());
                }
            }

            var dbItem = _itemRepository.RetrieveItem(folder.Id) as BasePluginFolder;

            if (dbItem is not null && string.Equals(dbItem.Path, folder.Path, StringComparison.OrdinalIgnoreCase))
            {
                folder = dbItem;
            }

            if (!folder.ParentId.Equals(rootFolder.Id))
            {
                folder.ParentId = rootFolder.Id;
                folder.UpdateToRepositoryAsync(ItemUpdateType.MetadataImport, CancellationToken.None).GetAwaiter().GetResult();
            }

            rootFolder.AddVirtualChild(folder);

            return rootFolder!;
        }

        public async Task ValidateTopLibraryFolders(CancellationToken cancellationToken)
        {
            await GetRootFolder().RefreshMetadata(cancellationToken).ConfigureAwait(false);

            // Start by just validating the children of the root, but go no further
            await GetRootFolder().ValidateChildren(
                new SimpleProgress<double>(),
                new MetadataRefreshOptions(new DirectoryService(_fileSystem)),
                recursive: false,
                cancellationToken).ConfigureAwait(false);

            await GetUserRootFolder().RefreshMetadata(cancellationToken).ConfigureAwait(false);

            await GetUserRootFolder().ValidateChildren(
                new SimpleProgress<double>(),
                new MetadataRefreshOptions(new DirectoryService(_fileSystem)),
                recursive: false,
                cancellationToken).ConfigureAwait(false);

            // Quickly scan CollectionFolders for changes
            foreach (var folder in GetUserRootFolder().Children.OfType<Folder>())
            {
                await folder.RefreshMetadata(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
