#nullable disable

#pragma warning disable CS1591

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Emby.Naming.Common;
using Emby.Naming.TV;
using Emby.Server.Implementations.Library.Resolvers;
using Emby.Server.Implementations.Library.Validators;
using Emby.Server.Implementations.Playlists;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using Jellyfin.Extensions;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Progress;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Controller.Sorting;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Library;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;
using EpisodeInfo = Emby.Naming.TV.EpisodeInfo;
using Genre = MediaBrowser.Controller.Entities.Genre;
using Person = MediaBrowser.Controller.Entities.Person;
using VideoResolver = Emby.Naming.Video.VideoResolver;

namespace Emby.Server.Implementations.Library
{
    /// <summary>
    /// Class LibraryManager.
    /// </summary>
    public class LibraryManager : ILibraryManager
    {
        private const string ShortcutFileExtension = ".mblink";

        private readonly ILogger<LibraryManager> _logger;
        private readonly ITaskManager _taskManager;
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataRepository;
        private readonly IServerConfigurationManager _configurationManager;
        private readonly Lazy<ILibraryMonitor> _libraryMonitorFactory;
        private readonly Lazy<IProviderManager> _providerManagerFactory;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly IFileSystem _fileSystem;
        private readonly IImageProcessor _imageProcessor;
        private readonly NamingOptions _namingOptions;
        private readonly ILibraryItemIdGenerator _libraryItemIdGenerator;
        private readonly ILibraryRootFolderManager _rootFolderManager;
        private readonly ILibraryOptionsManager _libraryOptionsManager;
        private readonly IItemService _itemService;
        private readonly IItemPathResolver _itemPathResolver;
        private readonly ExtraResolver _extraResolver;

        private readonly TimeSpan _viewRefreshInterval = TimeSpan.FromHours(24);

        private bool _wizardCompleted;

        /// <summary>
        /// Initializes a new instance of the <see cref="LibraryManager" /> class.
        /// </summary>
        /// <param name="appHost">The application host.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="taskManager">The task manager.</param>
        /// <param name="userManager">The user manager.</param>
        /// <param name="configurationManager">The configuration manager.</param>
        /// <param name="userDataRepository">The user data repository.</param>
        /// <param name="libraryMonitorFactory">The library monitor.</param>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="providerManagerFactory">The provider manager.</param>
        /// <param name="mediaEncoder">The media encoder.</param>
        /// <param name="imageProcessor">The image processor.</param>
        /// <param name="namingOptions">The naming options.</param>
        /// <param name="directoryService">The directory service.</param>
        /// <param name="libraryItemIdGenerator">The configured itemd id generator.</param>
        /// <param name="rootFolderManager">The instance of <see cref="ILibraryRootFolderManager"/> interface.</param>
        /// <param name="libraryOptionsManager">The instance of <see cref="ILibraryOptionsManager"/> interface.</param>
        /// <param name="itemService">The instance of <see cref="IItemService"/> interface.</param>
        /// <param name="itemPathResolver">The instance of <see cref="IItemPathResolver"/> interface.</param>
        public LibraryManager(
            IServerApplicationHost appHost,
            ILoggerFactory loggerFactory,
            ITaskManager taskManager,
            IUserManager userManager,
            IServerConfigurationManager configurationManager,
            IUserDataManager userDataRepository,
            Lazy<ILibraryMonitor> libraryMonitorFactory,
            IFileSystem fileSystem,
            Lazy<IProviderManager> providerManagerFactory,
            IMediaEncoder mediaEncoder,
            IImageProcessor imageProcessor,
            NamingOptions namingOptions,
            IDirectoryService directoryService,
            ILibraryItemIdGenerator libraryItemIdGenerator,
            ILibraryRootFolderManager rootFolderManager,
            ILibraryOptionsManager libraryOptionsManager,
            IItemService itemService,
            IItemPathResolver itemPathResolver)
        {
            _logger = loggerFactory.CreateLogger<LibraryManager>();
            _taskManager = taskManager;
            _userManager = userManager;
            _configurationManager = configurationManager;
            _userDataRepository = userDataRepository;
            _libraryMonitorFactory = libraryMonitorFactory;
            _fileSystem = fileSystem;
            _providerManagerFactory = providerManagerFactory;
            _mediaEncoder = mediaEncoder;
            _imageProcessor = imageProcessor;
            _namingOptions = namingOptions;
            _libraryItemIdGenerator = libraryItemIdGenerator;
            _rootFolderManager = rootFolderManager;
            _libraryOptionsManager = libraryOptionsManager;
            _itemService = itemService;
            _itemPathResolver = itemPathResolver;
            _extraResolver = new ExtraResolver(loggerFactory.CreateLogger<ExtraResolver>(), namingOptions, directoryService);

            _configurationManager.ConfigurationUpdated += ConfigurationUpdated;

            _itemService.ItemRemoved += OnItemRemoved;

            // NOTE: Is this going to be safe when there are multiple subscribers?
            // I think I've hit a point where ItemService is just becoming LibaryMaanger
            _itemService.ItemUpdated += async (s, e) => await OnItemUpdatedAsync(s, e);

            RecordConfigurationValues(configurationManager.Configuration);
        }

        private ILibraryMonitor LibraryMonitor => _libraryMonitorFactory.Value;

        private IProviderManager ProviderManager => _providerManagerFactory.Value;

        /// <summary>
        /// Gets or sets the postscan tasks.
        /// </summary>
        /// <value>The postscan tasks.</value>
        private ILibraryPostScanTask[] PostscanTasks { get; set; } = Array.Empty<ILibraryPostScanTask>();

        /// <summary>
        /// Gets or sets the intro providers.
        /// </summary>
        /// <value>The intro providers.</value>
        private IIntroProvider[] IntroProviders { get; set; } = Array.Empty<IIntroProvider>();

        /// <summary>
        /// Gets or sets the list of entity resolution ignore rules.
        /// </summary>
        /// <value>The entity resolution ignore rules.</value>
        private IResolverIgnoreRule[] EntityResolutionIgnoreRules { get; set; } = Array.Empty<IResolverIgnoreRule>();

        /// <summary>
        /// Gets or sets the list of currently registered entity resolvers.
        /// </summary>
        /// <value>The entity resolvers enumerable.</value>
        private IItemResolver[] EntityResolvers { get; set; } = Array.Empty<IItemResolver>();

        private IMultiItemResolver[] MultiItemResolvers { get; set; } = Array.Empty<IMultiItemResolver>();

        /// <summary>
        /// Gets or sets the comparers.
        /// </summary>
        /// <value>The comparers.</value>
        private IBaseItemComparer[] Comparers { get; set; } = Array.Empty<IBaseItemComparer>();

        public bool IsScanRunning { get; private set; }

        /// <summary>
        /// Adds the parts.
        /// </summary>
        /// <param name="rules">The rules.</param>
        /// <param name="introProviders">The intro providers.</param>
        /// <param name="itemComparers">The item comparers.</param>
        /// <param name="postscanTasks">The post scan tasks.</param>
        public void AddParts(
            IEnumerable<IResolverIgnoreRule> rules,
            IEnumerable<IIntroProvider> introProviders,
            IEnumerable<IBaseItemComparer> itemComparers,
            IEnumerable<ILibraryPostScanTask> postscanTasks)
        {
            EntityResolutionIgnoreRules = rules.ToArray();
            IntroProviders = introProviders.ToArray();
            Comparers = itemComparers.ToArray();
            PostscanTasks = postscanTasks.ToArray();
        }

        /// <summary>
        /// Records the configuration values.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        private void RecordConfigurationValues(ServerConfiguration configuration)
        {
            _wizardCompleted = configuration.IsStartupWizardCompleted;
        }

        /// <summary>
        /// Configurations the updated.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        private void ConfigurationUpdated(object sender, EventArgs e)
        {
            var config = _configurationManager.Configuration;

            var wizardChanged = config.IsStartupWizardCompleted != _wizardCompleted;

            RecordConfigurationValues(config);

            if (wizardChanged)
            {
                _taskManager.CancelIfRunningAndQueue<RefreshMediaLibraryTask>();
            }
        }

        private async Task OnItemUpdatedAsync(object sender, ItemChangeEventArgs e)
        {
            await UpdateItemAsync(e.Item, e.UpdateReason);
        }

        private void OnItemRemoved(object sender, ItemRemovedEventArgs e)
        {
            if (e.Item is not null)
            {
                DeleteItem(e.Item, e.DeleteOptions, e.Parent);
            }
        }

        private void DeleteItem(BaseItem item, DeleteOptions options, BaseItem parent)
        {
            ArgumentNullException.ThrowIfNull(item);

            _logger.LogInformation(
                "Removing item, Type: {0}, Name: {1}, Path: {2}, Id: {3}",
                item.GetType().Name,
                item.Name ?? "Unknown name",
                item.Path ?? string.Empty,
                item.Id);

            var children = item.IsFolder
                ? ((Folder)item).GetRecursiveChildren(false)
                : Array.Empty<BaseItem>();

            foreach (var metadataPath in GetMetadataPaths(item, children))
            {
                if (!Directory.Exists(metadataPath))
                {
                    continue;
                }

                _logger.LogDebug(
                    "Deleting metadata path, Type: {0}, Name: {1}, Path: {2}, Id: {3}",
                    item.GetType().Name,
                    item.Name ?? "Unknown name",
                    metadataPath,
                    item.Id);

                try
                {
                    Directory.Delete(metadataPath, true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting {MetadataPath}", metadataPath);
                }
            }

            if (options.DeleteFileLocation && item.IsFileProtocol)
            {
                // Assume only the first is required
                // Add this flag to GetDeletePaths if required in the future
                var isRequiredForDelete = true;

                foreach (var fileSystemInfo in item.GetDeletePaths())
                {
                    if (Directory.Exists(fileSystemInfo.FullName) || File.Exists(fileSystemInfo.FullName))
                    {
                        try
                        {
                            _logger.LogInformation(
                                "Deleting item path, Type: {0}, Name: {1}, Path: {2}, Id: {3}",
                                item.GetType().Name,
                                item.Name ?? "Unknown name",
                                fileSystemInfo.FullName,
                                item.Id);

                            if (fileSystemInfo.IsDirectory)
                            {
                                Directory.Delete(fileSystemInfo.FullName, true);
                            }
                            else
                            {
                                File.Delete(fileSystemInfo.FullName);
                            }
                        }
                        catch (IOException)
                        {
                            if (isRequiredForDelete)
                            {
                                throw;
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            if (isRequiredForDelete)
                            {
                                throw;
                            }
                        }
                    }

                    isRequiredForDelete = false;
                }
            }

            foreach (var child in children)
            {
                _itemService.DeleteItem(child);
            }
        }

        private static IEnumerable<string> GetMetadataPaths(BaseItem item, IEnumerable<BaseItem> children)
        {
            var list = new List<string>
            {
                item.GetInternalMetadataPath()
            };

            list.AddRange(children.Select(i => i.GetInternalMetadataPath()));

            return list;
        }

        public Guid GetNewItemId(string key, Type type)
        {
            return _libraryItemIdGenerator.Generate(key, type);
        }

        public bool IgnoreFile(FileSystemMetadata file, BaseItem parent)
            => EntityResolutionIgnoreRules.Any(r => r.ShouldIgnore(file, parent));

        public Folder GetUserRootFolder()
        {
            return _rootFolderManager.GetUserRootFolder();
        }

        /// <summary>
        /// Gets the person.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{Person}.</returns>
        public Person GetPerson(string name)
        {
            var path = Person.GetPath(name);
            var id = _itemService.GetItemByNameId<Person>(path);
            if (GetItemById(id) is not Person item)
            {
                item = new Person
                {
                    Name = name,
                    Id = id,
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow,
                    Path = path
                };
            }

            return item;
        }

        public Guid GetStudioId(string name)
        {
            return _itemService.GetItemByNameId<Studio>(Studio.GetPath(name));
        }

        public Guid GetGenreId(string name)
        {
            return _itemService.GetItemByNameId<Genre>(Genre.GetPath(name));
        }

        public Guid GetMusicGenreId(string name)
        {
            return _itemService.GetItemByNameId<MusicGenre>(MusicGenre.GetPath(name));
        }

        /// <summary>
        /// Gets the genre.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{Genre}.</returns>
        public Genre GetGenre(string name)
        {
            return _itemService.CreateItemByName<Genre>(Genre.GetPath, name, new DtoOptions(true));
        }

        /// <summary>
        /// Gets the music genre.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{MusicGenre}.</returns>
        public MusicGenre GetMusicGenre(string name)
        {
            return _itemService.CreateItemByName<MusicGenre>(MusicGenre.GetPath, name, new DtoOptions(true));
        }

        /// <summary>
        /// Gets the year.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>Task{Year}.</returns>
        public Year GetYear(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Years less than or equal to 0 are invalid.");
            }

            var name = value.ToString(CultureInfo.InvariantCulture);

            return _itemService.CreateItemByName<Year>(Year.GetPath, name, new DtoOptions(true));
        }

        /// <inheritdoc />
        public Task ValidatePeopleAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            // Ensure the location is available.
            Directory.CreateDirectory(_configurationManager.ApplicationPaths.PeoplePath);

            return new PeopleValidator(this, _logger, _fileSystem, _itemService).ValidatePeople(cancellationToken, progress);
        }

        /// <summary>
        /// Reloads the root media folder.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public Task ValidateMediaLibrary(IProgress<double> progress, CancellationToken cancellationToken)
        {
            // Just run the scheduled task so that the user can see it
            _taskManager.CancelIfRunningAndQueue<RefreshMediaLibraryTask>();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Validates the media library internal.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ValidateMediaLibraryInternal(IProgress<double> progress, CancellationToken cancellationToken)
        {
            IsScanRunning = true;
            LibraryMonitor.Stop();

            try
            {
                await PerformLibraryValidation(progress, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                LibraryMonitor.Start();
                IsScanRunning = false;
            }
        }

        private async Task PerformLibraryValidation(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Validating media library");

            await _rootFolderManager.ValidateTopLibraryFolders(cancellationToken).ConfigureAwait(false);

            var innerProgress = new ActionableProgress<double>();

            innerProgress.RegisterAction(pct => progress.Report(pct * 0.96));

            // Validate the entire media library
            await _rootFolderManager.GetRootFolder().ValidateChildren(innerProgress, new MetadataRefreshOptions(new DirectoryService(_fileSystem)), recursive: true, cancellationToken).ConfigureAwait(false);

            progress.Report(96);

            innerProgress = new ActionableProgress<double>();

            innerProgress.RegisterAction(pct => progress.Report(96 + (pct * .04)));

            await RunPostScanTasks(innerProgress, cancellationToken).ConfigureAwait(false);

            progress.Report(100);
        }

        /// <summary>
        /// Runs the post scan tasks.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task RunPostScanTasks(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var tasks = PostscanTasks.ToList();

            var numComplete = 0;
            var numTasks = tasks.Count;

            foreach (var task in tasks)
            {
                var innerProgress = new ActionableProgress<double>();

                // Prevent access to modified closure
                var currentNumComplete = numComplete;

                innerProgress.RegisterAction(pct =>
                {
                    double innerPercent = pct;
                    innerPercent /= 100;
                    innerPercent += currentNumComplete;

                    innerPercent /= numTasks;
                    innerPercent *= 100;

                    progress.Report(innerPercent);
                });

                _logger.LogDebug("Running post-scan task {0}", task.GetType().Name);

                try
                {
                    await task.Run(innerProgress, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Post-scan task cancelled: {0}", task.GetType().Name);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running post-scan task");
                }

                numComplete++;
                double percent = numComplete;
                percent /= numTasks;
                progress.Report(percent * 100);
            }

            _itemService.UpdateInheritedValues();

            progress.Report(100);
        }

        /// <summary>
        /// Gets the default view.
        /// </summary>
        /// <returns>IEnumerable{VirtualFolderInfo}.</returns>
        public List<VirtualFolderInfo> GetVirtualFolders()
        {
            return GetVirtualFolders(false);
        }

        public List<VirtualFolderInfo> GetVirtualFolders(bool includeRefreshState)
        {
            _logger.LogDebug("Getting topLibraryFolders");
            var topLibraryFolders = GetUserRootFolder().Children.ToList();

            _logger.LogDebug("Getting refreshQueue");
            var refreshQueue = includeRefreshState ? ProviderManager.GetRefreshQueue() : null;

            return _fileSystem.GetDirectoryPaths(_configurationManager.ApplicationPaths.DefaultUserViewsPath)
                .Select(dir => GetVirtualFolderInfo(dir, topLibraryFolders, refreshQueue))
                .ToList();
        }

        private VirtualFolderInfo GetVirtualFolderInfo(string dir, List<BaseItem> allCollectionFolders, HashSet<Guid> refreshQueue)
        {
            var info = new VirtualFolderInfo
            {
                Name = Path.GetFileName(dir),

                Locations = _fileSystem.GetFilePaths(dir, false)
                .Where(i => string.Equals(ShortcutFileExtension, Path.GetExtension(i), StringComparison.OrdinalIgnoreCase))
                    .Select(i =>
                    {
                        try
                        {
                            return _fileSystem.ExpandVirtualPath(_fileSystem.ResolveShortcut(i));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error resolving shortcut file {File}", i);
                            return null;
                        }
                    })
                    .Where(i => i is not null)
                    .Order()
                    .ToArray(),

                CollectionType = GetCollectionType(dir)
            };

            var libraryFolder = allCollectionFolders.FirstOrDefault(i => string.Equals(i.Path, dir, StringComparison.OrdinalIgnoreCase));
            if (libraryFolder is not null)
            {
                var libraryFolderId = libraryFolder.Id.ToString("N", CultureInfo.InvariantCulture);
                info.ItemId = libraryFolderId;
                if (libraryFolder.HasImage(ImageType.Primary))
                {
                    info.PrimaryImageItemId = libraryFolderId;
                }

                info.LibraryOptions = _libraryOptionsManager.GetLibraryOptions(libraryFolder);

                if (refreshQueue is not null)
                {
                    info.RefreshProgress = libraryFolder.GetRefreshProgress();

                    if (info.RefreshProgress.HasValue)
                    {
                        info.RefreshStatus = "Active";
                    }
                    else
                    {
                        info.RefreshStatus = refreshQueue.Contains(libraryFolder.Id) ? "Queued" : "Idle";
                    }
                }
            }

            return info;
        }

        private CollectionTypeOptions? GetCollectionType(string path)
        {
            var files = _fileSystem.GetFilePaths(path, new[] { ".collection" }, true, false);
            foreach (ReadOnlySpan<char> file in files)
            {
                if (Enum.TryParse<CollectionTypeOptions>(Path.GetFileNameWithoutExtension(file), true, out var res))
                {
                    return res;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the item by id.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>BaseItem.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
        public BaseItem GetItemById(Guid id)
        {
            return _itemService.GetItemById(id);
        }

        public List<BaseItem> GetItemList(InternalItemsQuery query)
        {
            return _itemService.GetItemList(query, true);
        }

        /// <summary>
        /// Gets the intros.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="user">The user.</param>
        /// <returns>IEnumerable{System.String}.</returns>
        public async Task<IEnumerable<Video>> GetIntros(BaseItem item, User user)
        {
            var tasks = IntroProviders
                .Take(1)
                .Select(i => GetIntros(i, item, user));

            var items = await Task.WhenAll(tasks).ConfigureAwait(false);

            return items
                .SelectMany(i => i.ToArray())
                .Select(ResolveIntro)
                .Where(i => i is not null);
        }

        /// <summary>
        /// Gets the intros.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <param name="item">The item.</param>
        /// <param name="user">The user.</param>
        /// <returns>Task&lt;IEnumerable&lt;IntroInfo&gt;&gt;.</returns>
        private async Task<IEnumerable<IntroInfo>> GetIntros(IIntroProvider provider, BaseItem item, User user)
        {
            try
            {
                return await provider.GetIntros(item, user).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting intros");

                return Enumerable.Empty<IntroInfo>();
            }
        }

        /// <summary>
        /// Resolves the intro.
        /// </summary>
        /// <param name="info">The info.</param>
        /// <returns>Video.</returns>
        private Video ResolveIntro(IntroInfo info)
        {
            Video video = null;

            if (info.ItemId.HasValue)
            {
                // Get an existing item by Id
                video = GetItemById(info.ItemId.Value) as Video;

                if (video is null)
                {
                    _logger.LogError("Unable to locate item with Id {ID}.", info.ItemId.Value);
                }
            }
            else if (!string.IsNullOrEmpty(info.Path))
            {
                try
                {
                    // Try to resolve the path into a video
                    video = _itemPathResolver.ResolvePath(_fileSystem.GetFileSystemInfo(info.Path)) as Video;

                    if (video is null)
                    {
                        _logger.LogError("Intro resolver returned null for {Path}.", info.Path);
                    }
                    else
                    {
                        // Pull the saved db item that will include metadata
                        var dbItem = GetItemById(video.Id) as Video;

                        if (dbItem is not null)
                        {
                            video = dbItem;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resolving path {Path}.", info.Path);
                }
            }
            else
            {
                _logger.LogError("IntroProvider returned an IntroInfo with null Path and ItemId.");
            }

            return video;
        }

        /// <summary>
        /// Sorts the specified sort by.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="user">The user.</param>
        /// <param name="sortBy">The sort by.</param>
        /// <param name="sortOrder">The sort order.</param>
        /// <returns>IEnumerable{BaseItem}.</returns>
        public IEnumerable<BaseItem> Sort(IEnumerable<BaseItem> items, User user, IEnumerable<string> sortBy, SortOrder sortOrder)
        {
            var isFirst = true;

            IOrderedEnumerable<BaseItem> orderedItems = null;

            foreach (var orderBy in sortBy.Select(o => GetComparer(o, user)).Where(c => c is not null))
            {
                if (isFirst)
                {
                    orderedItems = sortOrder == SortOrder.Descending ? items.OrderByDescending(i => i, orderBy) : items.OrderBy(i => i, orderBy);
                }
                else
                {
                    orderedItems = sortOrder == SortOrder.Descending ? orderedItems.ThenByDescending(i => i, orderBy) : orderedItems.ThenBy(i => i, orderBy);
                }

                isFirst = false;
            }

            return orderedItems ?? items;
        }

        public IEnumerable<BaseItem> Sort(IEnumerable<BaseItem> items, User user, IEnumerable<(string OrderBy, SortOrder SortOrder)> orderBy)
        {
            var isFirst = true;

            IOrderedEnumerable<BaseItem> orderedItems = null;

            foreach (var (name, sortOrder) in orderBy)
            {
                var comparer = GetComparer(name, user);
                if (comparer is null)
                {
                    continue;
                }

                if (isFirst)
                {
                    orderedItems = sortOrder == SortOrder.Descending ? items.OrderByDescending(i => i, comparer) : items.OrderBy(i => i, comparer);
                }
                else
                {
                    orderedItems = sortOrder == SortOrder.Descending ? orderedItems.ThenByDescending(i => i, comparer) : orderedItems.ThenBy(i => i, comparer);
                }

                isFirst = false;
            }

            return orderedItems ?? items;
        }

        /// <summary>
        /// Gets the comparer.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="user">The user.</param>
        /// <returns>IBaseItemComparer.</returns>
        private IBaseItemComparer GetComparer(string name, User user)
        {
            var comparer = Comparers.FirstOrDefault(c => string.Equals(name, c.Name, StringComparison.OrdinalIgnoreCase));

            // If it requires a user, create a new one, and assign the user
            if (comparer is IUserBaseItemComparer)
            {
                var userComparer = (IUserBaseItemComparer)Activator.CreateInstance(comparer.GetType());

                userComparer.User = user;
                userComparer.UserManager = _userManager;
                userComparer.UserDataRepository = _userDataRepository;

                return userComparer;
            }

            return comparer;
        }

        private bool ImageNeedsRefresh(ItemImageInfo image)
        {
            if (image.Path is not null && image.IsLocalFile)
            {
                if (image.Width == 0 || image.Height == 0 || string.IsNullOrEmpty(image.BlurHash))
                {
                    return true;
                }

                try
                {
                    return _fileSystem.GetLastWriteTimeUtc(image.Path) != image.DateModified;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cannot get file info for {0}", image.Path);
                    return false;
                }
            }

            return image.Path is not null && !image.IsLocalFile;
        }

        /// <inheritdoc />
        public async Task UpdateImagesAsync(BaseItem item, bool forceUpdate = false)
        {
            ArgumentNullException.ThrowIfNull(item);

            var outdated = forceUpdate
                ? item.ImageInfos.Where(i => i.Path is not null).ToArray()
                : item.ImageInfos.Where(ImageNeedsRefresh).ToArray();

            // Skip image processing if current or live tv source
            if (outdated.Length == 0 || item.SourceType != SourceType.Library)
            {
                return;
            }

            foreach (var img in outdated)
            {
                var image = img;
                if (!img.IsLocalFile)
                {
                    try
                    {
                        var index = item.GetImageIndex(img);
                        image = await ConvertImageToLocal(item, img, index).ConfigureAwait(false);
                    }
                    catch (ArgumentException)
                    {
                        _logger.LogWarning("Cannot get image index for {ImagePath}", img.Path);
                        continue;
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or IOException)
                    {
                        _logger.LogWarning(ex, "Cannot fetch image from {ImagePath}", img.Path);
                        continue;
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogWarning(ex, "Cannot fetch image from {ImagePath}. Http status code: {HttpStatus}", img.Path, ex.StatusCode);
                        continue;
                    }
                }

                ImageDimensions size;
                try
                {
                    size = _imageProcessor.GetImageDimensions(item, image);
                    image.Width = size.Width;
                    image.Height = size.Height;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cannot get image dimensions for {ImagePath}", image.Path);
                    size = default;
                    image.Width = 0;
                    image.Height = 0;
                }

                try
                {
                    image.BlurHash = _imageProcessor.GetImageBlurHash(image.Path, size);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cannot compute blurhash for {ImagePath}", image.Path);
                    image.BlurHash = string.Empty;
                }

                try
                {
                    image.DateModified = _fileSystem.GetLastWriteTimeUtc(image.Path);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cannot update DateModified for {ImagePath}", image.Path);
                }
            }

            _itemService.SaveImages(item);
        }

        private async Task UpdateItemAsync(BaseItem item, ItemUpdateType updateReason)
        {
            await RunMetadataSavers(item, updateReason).ConfigureAwait(false);
        }

        public async Task RunMetadataSavers(BaseItem item, ItemUpdateType updateReason)
        {
            if (item.IsFileProtocol)
            {
                await ProviderManager.SaveMetadataAsync(item, updateReason).ConfigureAwait(false);
            }

            item.DateLastSaved = DateTime.UtcNow;

            await UpdateImagesAsync(item, updateReason >= ItemUpdateType.ImageUpdate).ConfigureAwait(false);
        }

        public UserView GetNamedView(
            User user,
            string name,
            string viewType,
            string sortName)
        {
            return GetNamedView(user, name, Guid.Empty, viewType, sortName);
        }

        public UserView GetNamedView(
            string name,
            string viewType,
            string sortName)
        {
            var path = Path.Combine(
                _configurationManager.ApplicationPaths.InternalMetadataPath,
                "views",
                _fileSystem.GetValidFilename(viewType));

            var id = GetNewItemId(path + "_namedview_" + name, typeof(UserView));

            var item = GetItemById(id) as UserView;

            var refresh = false;

            if (item is null || !string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(path);

                item = new UserView
                {
                    Path = path,
                    Id = id,
                    DateCreated = DateTime.UtcNow,
                    Name = name,
                    ViewType = viewType,
                    ForcedSortName = sortName
                };

                _itemService.CreateItem(item, null);

                refresh = true;
            }

            if (refresh)
            {
                item.UpdateToRepositoryAsync(ItemUpdateType.MetadataImport, CancellationToken.None).GetAwaiter().GetResult();
                ProviderManager.QueueRefresh(item.Id, new MetadataRefreshOptions(new DirectoryService(_fileSystem)), RefreshPriority.Normal);
            }

            return item;
        }

        public UserView GetNamedView(
            User user,
            string name,
            Guid parentId,
            string viewType,
            string sortName)
        {
            var parentIdString = parentId.Equals(Guid.Empty)
                ? null
                : parentId.ToString("N", CultureInfo.InvariantCulture);
            var idValues = "38_namedview_" + name + user.Id.ToString("N", CultureInfo.InvariantCulture) + (parentIdString ?? string.Empty) + (viewType ?? string.Empty);

            var id = GetNewItemId(idValues, typeof(UserView));

            var path = Path.Combine(_configurationManager.ApplicationPaths.InternalMetadataPath, "views", id.ToString("N", CultureInfo.InvariantCulture));

            var item = GetItemById(id) as UserView;

            var isNew = false;

            if (item is null)
            {
                Directory.CreateDirectory(path);

                item = new UserView
                {
                    Path = path,
                    Id = id,
                    DateCreated = DateTime.UtcNow,
                    Name = name,
                    ViewType = viewType,
                    ForcedSortName = sortName,
                    UserId = user.Id,
                    DisplayParentId = parentId
                };

                _itemService.CreateItem(item, null);

                isNew = true;
            }

            var refresh = isNew || DateTime.UtcNow - item.DateLastRefreshed >= _viewRefreshInterval;

            if (!refresh && !item.DisplayParentId.Equals(default))
            {
                var displayParent = GetItemById(item.DisplayParentId);
                refresh = displayParent is not null && displayParent.DateLastSaved > item.DateLastRefreshed;
            }

            if (refresh)
            {
                ProviderManager.QueueRefresh(
                    item.Id,
                    new MetadataRefreshOptions(new DirectoryService(_fileSystem))
                    {
                        // Need to force save to increment DateLastSaved
                        ForceSave = true
                    },
                    RefreshPriority.Normal);
            }

            return item;
        }

        public UserView GetNamedView(
            string name,
            Guid parentId,
            string viewType,
            string sortName,
            string uniqueId)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            var parentIdString = parentId.Equals(Guid.Empty)
                ? null
                : parentId.ToString("N", CultureInfo.InvariantCulture);
            var idValues = "37_namedview_" + name + (parentIdString ?? string.Empty) + (viewType ?? string.Empty);
            if (!string.IsNullOrEmpty(uniqueId))
            {
                idValues += uniqueId;
            }

            var id = GetNewItemId(idValues, typeof(UserView));

            var path = Path.Combine(_configurationManager.ApplicationPaths.InternalMetadataPath, "views", id.ToString("N", CultureInfo.InvariantCulture));

            var item = GetItemById(id) as UserView;

            var isNew = false;

            if (item is null)
            {
                Directory.CreateDirectory(path);

                item = new UserView
                {
                    Path = path,
                    Id = id,
                    DateCreated = DateTime.UtcNow,
                    Name = name,
                    ViewType = viewType,
                    ForcedSortName = sortName
                };

                item.DisplayParentId = parentId;

                _itemService.CreateItem(item, null);

                isNew = true;
            }

            if (!string.Equals(viewType, item.ViewType, StringComparison.OrdinalIgnoreCase))
            {
                item.ViewType = viewType;
                item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).GetAwaiter().GetResult();
            }

            var refresh = isNew || DateTime.UtcNow - item.DateLastRefreshed >= _viewRefreshInterval;

            if (!refresh && !item.DisplayParentId.Equals(Guid.Empty))
            {
                var displayParent = GetItemById(item.DisplayParentId);
                refresh = displayParent is not null && displayParent.DateLastSaved > item.DateLastRefreshed;
            }

            if (refresh)
            {
                ProviderManager.QueueRefresh(
                    item.Id,
                    new MetadataRefreshOptions(new DirectoryService(_fileSystem))
                    {
                        // Need to force save to increment DateLastSaved
                        ForceSave = true
                    },
                    RefreshPriority.Normal);
            }

            return item;
        }

        public UserView GetShadowView(
            BaseItem parent,
            string viewType,
            string sortName)
        {
            ArgumentNullException.ThrowIfNull(parent);

            var name = parent.Name;
            var parentId = parent.Id;

            var idValues = "38_namedview_" + name + parentId + (viewType ?? string.Empty);

            var id = GetNewItemId(idValues, typeof(UserView));

            var path = parent.Path;

            var item = GetItemById(id) as UserView;

            var isNew = false;

            if (item is null)
            {
                Directory.CreateDirectory(path);

                item = new UserView
                {
                    Path = path,
                    Id = id,
                    DateCreated = DateTime.UtcNow,
                    Name = name,
                    ViewType = viewType,
                    ForcedSortName = sortName
                };

                item.DisplayParentId = parentId;

                _itemService.CreateItem(item, null);

                isNew = true;
            }

            var refresh = isNew || DateTime.UtcNow - item.DateLastRefreshed >= _viewRefreshInterval;

            if (!refresh && !item.DisplayParentId.Equals(Guid.Empty))
            {
                var displayParent = GetItemById(item.DisplayParentId);
                refresh = displayParent is not null && displayParent.DateLastSaved > item.DateLastRefreshed;
            }

            if (refresh)
            {
                ProviderManager.QueueRefresh(
                    item.Id,
                    new MetadataRefreshOptions(new DirectoryService(_fileSystem))
                    {
                        // Need to force save to increment DateLastSaved
                        ForceSave = true
                    },
                    RefreshPriority.Normal);
            }

            return item;
        }

        public BaseItem GetParentItem(Guid? parentId, Guid? userId)
        {
            if (parentId.HasValue)
            {
                return GetItemById(parentId.Value);
            }

            if (userId.HasValue && !userId.Equals(default))
            {
                return GetUserRootFolder();
            }

            return _rootFolderManager.GetRootFolder();
        }

        /// <inheritdoc />
        public void QueueLibraryScan()
        {
            _taskManager.QueueScheduledTask<RefreshMediaLibraryTask>();
        }

        /// <inheritdoc />
        public int? GetSeasonNumberFromPath(string path)
            => SeasonPathParser.Parse(path, true, true).SeasonNumber;

        /// <inheritdoc />
        public bool FillMissingEpisodeNumbersFromPath(Episode episode, bool forceRefresh)
        {
            var series = episode.Series;
            bool? isAbsoluteNaming = series is not null && string.Equals(series.DisplayOrder, "absolute", StringComparison.OrdinalIgnoreCase);
            if (!isAbsoluteNaming.Value)
            {
                // In other words, no filter applied
                isAbsoluteNaming = null;
            }

            var resolver = new EpisodeResolver(_namingOptions);

            var isFolder = episode.VideoType == VideoType.BluRay || episode.VideoType == VideoType.Dvd;

            // TODO nullable - what are we trying to do there with empty episodeInfo?
            EpisodeInfo episodeInfo = null;
            if (episode.IsFileProtocol)
            {
                episodeInfo = resolver.Resolve(episode.Path, isFolder, null, null, isAbsoluteNaming);
                // Resolve from parent folder if it's not the Season folder
                var parent = episode.GetParent();
                if (episodeInfo is null && parent.GetType() == typeof(Folder))
                {
                    episodeInfo = resolver.Resolve(parent.Path, true, null, null, isAbsoluteNaming);
                    if (episodeInfo is not null)
                    {
                        // add the container
                        episodeInfo.Container = Path.GetExtension(episode.Path)?.TrimStart('.');
                    }
                }
            }

            episodeInfo ??= new EpisodeInfo(episode.Path);

            try
            {
                var libraryOptions = _libraryOptionsManager.GetLibraryOptions(episode);
                if (libraryOptions.EnableEmbeddedEpisodeInfos && string.Equals(episodeInfo.Container, "mp4", StringComparison.OrdinalIgnoreCase))
                {
                    // Read from metadata
                    var mediaInfo = _mediaEncoder.GetMediaInfo(
                        new MediaInfoRequest
                        {
                            MediaSource = episode.GetMediaSources(false)[0],
                            MediaType = DlnaProfileType.Video
                        },
                        CancellationToken.None).GetAwaiter().GetResult();
                    if (mediaInfo.ParentIndexNumber > 0)
                    {
                        episodeInfo.SeasonNumber = mediaInfo.ParentIndexNumber;
                    }

                    if (mediaInfo.IndexNumber > 0)
                    {
                        episodeInfo.EpisodeNumber = mediaInfo.IndexNumber;
                    }

                    if (!string.IsNullOrEmpty(mediaInfo.ShowName))
                    {
                        episodeInfo.SeriesName = mediaInfo.ShowName;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading the episode information with ffprobe. Episode: {EpisodeInfo}", episodeInfo.Path);
            }

            var changed = false;

            if (episodeInfo.IsByDate)
            {
                if (episode.IndexNumber.HasValue)
                {
                    episode.IndexNumber = null;
                    changed = true;
                }

                if (episode.IndexNumberEnd.HasValue)
                {
                    episode.IndexNumberEnd = null;
                    changed = true;
                }

                if (!episode.PremiereDate.HasValue)
                {
                    if (episodeInfo.Year.HasValue && episodeInfo.Month.HasValue && episodeInfo.Day.HasValue)
                    {
                        episode.PremiereDate = new DateTime(episodeInfo.Year.Value, episodeInfo.Month.Value, episodeInfo.Day.Value).ToUniversalTime();
                    }

                    if (episode.PremiereDate.HasValue)
                    {
                        changed = true;
                    }
                }

                if (!episode.ProductionYear.HasValue)
                {
                    episode.ProductionYear = episodeInfo.Year;

                    if (episode.ProductionYear.HasValue)
                    {
                        changed = true;
                    }
                }
            }
            else
            {
                if (!episode.IndexNumber.HasValue || forceRefresh)
                {
                    if (episode.IndexNumber != episodeInfo.EpisodeNumber)
                    {
                        changed = true;
                    }

                    episode.IndexNumber = episodeInfo.EpisodeNumber;
                }

                if (!episode.IndexNumberEnd.HasValue || forceRefresh)
                {
                    if (episode.IndexNumberEnd != episodeInfo.EndingEpisodeNumber)
                    {
                        changed = true;
                    }

                    episode.IndexNumberEnd = episodeInfo.EndingEpisodeNumber;
                }

                if (!episode.ParentIndexNumber.HasValue || forceRefresh)
                {
                    if (episode.ParentIndexNumber != episodeInfo.SeasonNumber)
                    {
                        changed = true;
                    }

                    episode.ParentIndexNumber = episodeInfo.SeasonNumber;
                }
            }

            if (!episode.ParentIndexNumber.HasValue)
            {
                var season = episode.Season;

                if (season is not null)
                {
                    episode.ParentIndexNumber = season.IndexNumber;
                }
                else
                {
                    /*
                    Anime series don't generally have a season in their file name, however,
                    TVDb needs a season to correctly get the metadata.
                    Hence, a null season needs to be filled with something. */
                    // FIXME perhaps this would be better for TVDb parser to ask for season 1 if no season is specified
                    episode.ParentIndexNumber = 1;
                }

                if (episode.ParentIndexNumber.HasValue)
                {
                    changed = true;
                }
            }

            return changed;
        }

        public ItemLookupInfo ParseName(string name)
        {
            var namingOptions = _namingOptions;
            var result = VideoResolver.CleanDateTime(name, namingOptions);

            return new ItemLookupInfo
            {
                Name = VideoResolver.TryCleanString(result.Name, namingOptions, out var newName) ? newName : result.Name,
                Year = result.Year
            };
        }

        public IEnumerable<BaseItem> FindExtras(BaseItem owner, IReadOnlyList<FileSystemMetadata> fileSystemChildren, IDirectoryService directoryService)
        {
            var ownerVideoInfo = VideoResolver.Resolve(owner.Path, owner.IsFolder, _namingOptions);
            if (ownerVideoInfo is null)
            {
                yield break;
            }

            var count = fileSystemChildren.Count;
            for (var i = 0; i < count; i++)
            {
                var current = fileSystemChildren[i];
                if (current.IsDirectory && _namingOptions.AllExtrasTypesFolderNames.ContainsKey(current.Name))
                {
                    var filesInSubFolder = _fileSystem.GetFiles(current.FullName, null, false, false);
                    foreach (var file in filesInSubFolder)
                    {
                        if (!_extraResolver.TryGetExtraTypeForOwner(file.FullName, ownerVideoInfo, out var extraType))
                        {
                            continue;
                        }

                        var extra = GetExtra(file, extraType.Value);
                        if (extra is not null)
                        {
                            yield return extra;
                        }
                    }
                }
                else if (!current.IsDirectory && _extraResolver.TryGetExtraTypeForOwner(current.FullName, ownerVideoInfo, out var extraType))
                {
                    var extra = GetExtra(current, extraType.Value);
                    if (extra is not null)
                    {
                        yield return extra;
                    }
                }
            }

            BaseItem GetExtra(FileSystemMetadata file, ExtraType extraType)
            {
                var extra = _itemPathResolver.ResolvePath(_fileSystem.GetFileInfo(file.FullName), _extraResolver.GetResolversForExtraType(extraType));
                if (extra is not Video && extra is not Audio)
                {
                    return null;
                }

                // Try to retrieve it from the db. If we don't find it, use the resolved version
                var itemById = GetItemById(extra.Id);
                if (itemById is not null)
                {
                    extra = itemById;
                }

                extra.ExtraType = extraType;
                extra.ParentId = Guid.Empty;
                extra.OwnerId = owner.Id;
                return extra;
            }
        }

        public string GetPathAfterNetworkSubstitution(string path, BaseItem ownerItem)
        {
            string newPath;
            if (ownerItem is not null)
            {
                var libraryOptions = _libraryOptionsManager.GetLibraryOptions(ownerItem);
                if (libraryOptions is not null)
                {
                    foreach (var pathInfo in libraryOptions.PathInfos)
                    {
                        if (path.TryReplaceSubPath(pathInfo.Path, pathInfo.NetworkPath, out newPath))
                        {
                            return newPath;
                        }
                    }
                }
            }

            var metadataPath = _configurationManager.Configuration.MetadataPath;
            var metadataNetworkPath = _configurationManager.Configuration.MetadataNetworkPath;

            if (path.TryReplaceSubPath(metadataPath, metadataNetworkPath, out newPath))
            {
                return newPath;
            }

            foreach (var map in _configurationManager.Configuration.PathSubstitutions)
            {
                if (path.TryReplaceSubPath(map.From, map.To, out newPath))
                {
                    return newPath;
                }
            }

            return path;
        }

        public List<PersonInfo> GetPeople(BaseItem item)
        {
            if (item.SupportsPeople)
            {
                var people = _itemService.GetPeople(new InternalPeopleQuery
                {
                    ItemId = item.Id
                });

                if (people.Count > 0)
                {
                    return people;
                }
            }

            return new List<PersonInfo>();
        }

        public List<Person> GetPeopleItems(InternalPeopleQuery query)
        {
            return _itemService.GetPeopleNames(query)
            .Select(i =>
            {
                try
                {
                    return GetPerson(i);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting person");
                    return null;
                }
            })
            .Where(i => i is not null)
            .Where(i => query.User is null || i.IsVisible(query.User))
            .ToList();
        }

        public void UpdatePeople(BaseItem item, List<PersonInfo> people)
        {
            UpdatePeopleAsync(item, people, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        /// <inheritdoc />
        public async Task UpdatePeopleAsync(BaseItem item, List<PersonInfo> people, CancellationToken cancellationToken)
        {
            if (!item.SupportsPeople)
            {
                return;
            }

            _itemService.UpdatePeople(item.Id, people);

            await SavePeopleMetadataAsync(people, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ItemImageInfo> ConvertImageToLocal(BaseItem item, ItemImageInfo image, int imageIndex)
        {
            foreach (var url in image.Path.Split('|'))
            {
                try
                {
                    _logger.LogDebug("ConvertImageToLocal item {0} - image url: {1}", item.Id, url);

                    await ProviderManager.SaveImage(item, url, image.Type, imageIndex, CancellationToken.None).ConfigureAwait(false);

                    await item.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, CancellationToken.None).ConfigureAwait(false);

                    return item.GetImageInfo(image.Type, imageIndex);
                }
                catch (HttpRequestException ex)
                {
                    if (ex.StatusCode.HasValue
                        && (ex.StatusCode.Value == HttpStatusCode.NotFound || ex.StatusCode.Value == HttpStatusCode.Forbidden))
                    {
                        continue;
                    }

                    throw;
                }
            }

            // Remove this image to prevent it from retrying over and over
            item.RemoveImage(image);
            await item.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, CancellationToken.None).ConfigureAwait(false);

            throw new InvalidOperationException();
        }

        public async Task AddVirtualFolder(string name, CollectionTypeOptions? collectionType, LibraryOptions options, bool refreshLibrary)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            name = _fileSystem.GetValidFilename(name);

            var rootFolderPath = _configurationManager.ApplicationPaths.DefaultUserViewsPath;

            var existingNameCount = 1; // first numbered name will be 2
            var virtualFolderPath = Path.Combine(rootFolderPath, name);
            var originalName = name;
            while (Directory.Exists(virtualFolderPath))
            {
                existingNameCount++;
                name = originalName + existingNameCount;
                virtualFolderPath = Path.Combine(rootFolderPath, name);
            }

            var mediaPathInfos = options.PathInfos;
            if (mediaPathInfos is not null)
            {
                var invalidpath = mediaPathInfos.FirstOrDefault(i => !Directory.Exists(i.Path));
                if (invalidpath is not null)
                {
                    throw new ArgumentException("The specified path does not exist: " + invalidpath.Path + ".");
                }
            }

            LibraryMonitor.Stop();

            try
            {
                Directory.CreateDirectory(virtualFolderPath);

                if (collectionType is not null)
                {
                    var path = Path.Combine(virtualFolderPath, collectionType.ToString().ToLowerInvariant() + ".collection");

                    File.WriteAllBytes(path, Array.Empty<byte>());
                }

                _libraryOptionsManager.SaveLibraryOptions(virtualFolderPath, options);

                if (mediaPathInfos is not null)
                {
                    foreach (var path in mediaPathInfos)
                    {
                        AddMediaPathInternal(name, path, false);
                    }
                }
            }
            finally
            {
                if (refreshLibrary)
                {
                    await _rootFolderManager.ValidateTopLibraryFolders(CancellationToken.None).ConfigureAwait(false);

                    StartScanInBackground();
                }
                else
                {
                    // Need to add a delay here or directory watchers may still pick up the changes
                    await Task.Delay(1000).ConfigureAwait(false);
                    LibraryMonitor.Start();
                }
            }
        }

        private async Task SavePeopleMetadataAsync(IEnumerable<PersonInfo> people, CancellationToken cancellationToken)
        {
            List<BaseItem> personsToSave = null;

            foreach (var person in people)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var itemUpdateType = ItemUpdateType.MetadataDownload;
                var saveEntity = false;
                var personEntity = GetPerson(person.Name);

                // if PresentationUniqueKey is empty it's likely a new item.
                if (string.IsNullOrEmpty(personEntity.PresentationUniqueKey))
                {
                    personEntity.PresentationUniqueKey = personEntity.CreatePresentationUniqueKey();
                    saveEntity = true;
                }

                foreach (var id in person.ProviderIds)
                {
                    if (!string.Equals(personEntity.GetProviderId(id.Key), id.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        personEntity.SetProviderId(id.Key, id.Value);
                        saveEntity = true;
                    }
                }

                if (!string.IsNullOrWhiteSpace(person.ImageUrl) && !personEntity.HasImage(ImageType.Primary))
                {
                    personEntity.SetImage(
                        new ItemImageInfo
                        {
                            Path = person.ImageUrl,
                            Type = ImageType.Primary
                        },
                        0);

                    saveEntity = true;
                    itemUpdateType = ItemUpdateType.ImageUpdate;
                }

                if (saveEntity)
                {
                    (personsToSave ??= new()).Add(personEntity);
                    await RunMetadataSavers(personEntity, itemUpdateType).ConfigureAwait(false);
                }
            }

            if (personsToSave is not null)
            {
                _itemService.CreateItems(personsToSave, null, CancellationToken.None);
            }
        }

        private void StartScanInBackground()
        {
            Task.Run(() =>
            {
                // No need to start if scanning the library because it will handle it
                ValidateMediaLibrary(new SimpleProgress<double>(), CancellationToken.None);
            });
        }

        public void AddMediaPath(string virtualFolderName, MediaPathInfo mediaPath)
        {
            AddMediaPathInternal(virtualFolderName, mediaPath, true);
        }

        private void AddMediaPathInternal(string virtualFolderName, MediaPathInfo pathInfo, bool saveLibraryOptions)
        {
            ArgumentNullException.ThrowIfNull(pathInfo);

            var path = pathInfo.Path;

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(nameof(path));
            }

            if (!Directory.Exists(path))
            {
                throw new FileNotFoundException("The path does not exist.");
            }

            var rootFolderPath = _configurationManager.ApplicationPaths.DefaultUserViewsPath;
            var virtualFolderPath = Path.Combine(rootFolderPath, virtualFolderName);

            var shortcutFilename = Path.GetFileNameWithoutExtension(path);

            var lnk = Path.Combine(virtualFolderPath, shortcutFilename + ShortcutFileExtension);

            while (File.Exists(lnk))
            {
                shortcutFilename += "1";
                lnk = Path.Combine(virtualFolderPath, shortcutFilename + ShortcutFileExtension);
            }

            _fileSystem.CreateShortcut(lnk, _fileSystem.ReverseVirtualPath(path));

            RemoveContentTypeOverrides(path);

            if (saveLibraryOptions)
            {
                var libraryOptions = _libraryOptionsManager.GetLibraryOptions(virtualFolderPath);

                var list = libraryOptions.PathInfos.ToList();
                list.Add(pathInfo);
                libraryOptions.PathInfos = list.ToArray();

                SyncLibraryOptionsToLocations(virtualFolderPath, libraryOptions);

                _libraryOptionsManager.SaveLibraryOptions(virtualFolderPath, libraryOptions);
            }
        }

        public void UpdateMediaPath(string virtualFolderName, MediaPathInfo mediaPath)
        {
            ArgumentNullException.ThrowIfNull(mediaPath);

            var rootFolderPath = _configurationManager.ApplicationPaths.DefaultUserViewsPath;
            var virtualFolderPath = Path.Combine(rootFolderPath, virtualFolderName);

            var libraryOptions = _libraryOptionsManager.GetLibraryOptions(virtualFolderPath);

            SyncLibraryOptionsToLocations(virtualFolderPath, libraryOptions);

            var list = libraryOptions.PathInfos.ToList();
            foreach (var originalPathInfo in list.Where(originalPathInfo => string.Equals(mediaPath.Path, originalPathInfo.Path, StringComparison.Ordinal)))
            {
                originalPathInfo.NetworkPath = mediaPath.NetworkPath;
                break;
            }

            libraryOptions.PathInfos = list.ToArray();

            _libraryOptionsManager.SaveLibraryOptions(virtualFolderPath, libraryOptions);
        }

        private void SyncLibraryOptionsToLocations(string virtualFolderPath, LibraryOptions options)
        {
            var topLibraryFolders = GetUserRootFolder().Children.ToList();
            var info = GetVirtualFolderInfo(virtualFolderPath, topLibraryFolders, null);

            if (info.Locations.Length > 0 && info.Locations.Length != options.PathInfos.Length)
            {
                var list = options.PathInfos.ToList();

                foreach (var location in info.Locations)
                {
                    if (!list.Any(i => string.Equals(i.Path, location, StringComparison.Ordinal)))
                    {
                        list.Add(new MediaPathInfo(location));
                    }
                }

                options.PathInfos = list.ToArray();
            }
        }

        public async Task RemoveVirtualFolder(string name, bool refreshLibrary)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            var rootFolderPath = _configurationManager.ApplicationPaths.DefaultUserViewsPath;

            var path = Path.Combine(rootFolderPath, name);

            if (!Directory.Exists(path))
            {
                throw new FileNotFoundException("The media folder does not exist");
            }

            LibraryMonitor.Stop();

            try
            {
                Directory.Delete(path, true);
            }
            finally
            {
                _libraryOptionsManager.ClearOptionsCache();

                if (refreshLibrary)
                {
                    await _rootFolderManager.ValidateTopLibraryFolders(CancellationToken.None).ConfigureAwait(false);

                    StartScanInBackground();
                }
                else
                {
                    // Need to add a delay here or directory watchers may still pick up the changes
                    await Task.Delay(1000).ConfigureAwait(false);
                    LibraryMonitor.Start();
                }
            }
        }

        private void RemoveContentTypeOverrides(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            List<NameValuePair> removeList = null;

            foreach (var contentType in _configurationManager.Configuration.ContentTypes)
            {
                if (string.IsNullOrWhiteSpace(contentType.Name)
                    || _fileSystem.AreEqual(path, contentType.Name)
                    || _fileSystem.ContainsSubPath(path, contentType.Name))
                {
                    (removeList ??= new()).Add(contentType);
                }
            }

            if (removeList is not null)
            {
                _configurationManager.Configuration.ContentTypes = _configurationManager.Configuration.ContentTypes
                    .Except(removeList)
                    .ToArray();

                _configurationManager.SaveConfiguration();
            }
        }

        public void RemoveMediaPath(string virtualFolderName, string mediaPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(mediaPath);

            var rootFolderPath = _configurationManager.ApplicationPaths.DefaultUserViewsPath;
            var virtualFolderPath = Path.Combine(rootFolderPath, virtualFolderName);

            if (!Directory.Exists(virtualFolderPath))
            {
                throw new FileNotFoundException(
                    string.Format(CultureInfo.InvariantCulture, "The media collection {0} does not exist", virtualFolderName));
            }

            var shortcut = _fileSystem.GetFilePaths(virtualFolderPath, true)
                .Where(i => string.Equals(ShortcutFileExtension, Path.GetExtension(i), StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(f => _fileSystem.ExpandVirtualPath(_fileSystem.ResolveShortcut(f)).Equals(mediaPath, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(shortcut))
            {
                _fileSystem.DeleteFile(shortcut);
            }

            var libraryOptions = _libraryOptionsManager.GetLibraryOptions(virtualFolderPath);

            libraryOptions.PathInfos = libraryOptions
                .PathInfos
                .Where(i => !string.Equals(i.Path, mediaPath, StringComparison.Ordinal))
                .ToArray();

            _libraryOptionsManager.SaveLibraryOptions(virtualFolderPath, libraryOptions);
        }
    }
}
