using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Controller.Sorting;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Library
{
    public class ItemPathResolver : IItemPathResolver
    {
        private readonly IFileSystem _fileSystem;
        private readonly IServerConfigurationManager _serverConfigurationManager;
        private readonly ILogger<ItemPathResolver> _logger;
        private readonly ILibraryItemIdGenerator _libraryItemIdGenerator;
        private readonly ILibraryOptionsManager _libraryOptionsManager;
        private readonly IItemContentTypeProvider _itemContentTypeProvider;
        private readonly DirectoryService _directoryService;

        private IItemResolver[] _itemResolvers;
        private IMultiItemResolver[] _multiItemResolvers;
        private IResolverIgnoreRule[] _resolverIgnoreRules;

        public ItemPathResolver(
            IFileSystem fileSystem,
            IServerConfigurationManager serverConfigurationManager,
            ILogger<ItemPathResolver> logger,
            ILibraryItemIdGenerator libraryItemIdGenerator,
            ILibraryOptionsManager libraryOptionsManager,
            IItemContentTypeProvider itemContentTypeProvider)
        {
            _fileSystem = fileSystem;
            _serverConfigurationManager = serverConfigurationManager;
            _logger = logger;
            _libraryItemIdGenerator = libraryItemIdGenerator;
            _libraryOptionsManager = libraryOptionsManager;
            _itemContentTypeProvider = itemContentTypeProvider;
            _directoryService = new(fileSystem);

            _itemResolvers = Array.Empty<IItemResolver>();
            _resolverIgnoreRules = Array.Empty<IResolverIgnoreRule>();
            _multiItemResolvers = Array.Empty<IMultiItemResolver>();
        }

        /// <inheritdoc/>
        public void AddParts(
           IEnumerable<IItemResolver> itemResolvers,
           IEnumerable<IResolverIgnoreRule> rules)
        {
            _itemResolvers = itemResolvers.ToArray();
            _multiItemResolvers = _itemResolvers.OfType<IMultiItemResolver>().ToArray();
            _resolverIgnoreRules = rules.ToArray();
        }

        /// <inheritdoc/>
        public BaseItem? ResolvePath(FileSystemMetadata fileInfo, Folder? parent = null)
            => ResolvePath(fileInfo, parent);

        public BaseItem? ResolvePath(
            FileSystemMetadata fileInfo,
            IItemResolver[] resolvers,
            Folder? parent = null,
            string? collectionType = null,
            LibraryOptions? libraryOptions = null)
        {
            ArgumentNullException.ThrowIfNull(fileInfo);

            var fullPath = fileInfo.FullName;

            if (string.IsNullOrEmpty(collectionType) && parent is not null)
            {
                collectionType = _itemContentTypeProvider.GetContentTypeOverride(fullPath, true);
            }

            var args = new ItemResolveArgs(_serverConfigurationManager.ApplicationPaths, _libraryOptionsManager, this, _itemContentTypeProvider, _fileSystem)
            {
                Parent = parent,
                FileInfo = fileInfo,
                CollectionType = collectionType,
                LibraryOptions = libraryOptions
            };

            // Return null if ignore rules deem that we should do so
            if (IgnoreFile(args.FileInfo, args.Parent!))
            {
                return null;
            }

            // Gather child folder and files
            if (args.IsDirectory)
            {
                var isPhysicalRoot = args.IsPhysicalRoot;

                // When resolving the root, we need it's grandchildren (children of user views)
                var flattenFolderDepth = isPhysicalRoot ? 2 : 0;

                FileSystemMetadata[] files;
                var isVf = args.IsVf;

                try
                {
                    files = FileData.GetFilteredFileSystemEntries(_directoryService, args.Path, _fileSystem, _logger, args, flattenFolderDepth: flattenFolderDepth, resolveShortcuts: isPhysicalRoot || isVf);
                }
                catch (Exception ex)
                {
                    if (parent is not null && parent.IsPhysicalRoot)
                    {
                        _logger.LogError(ex, "Error in GetFilteredFileSystemEntries isPhysicalRoot: {0} IsVf: {1}", isPhysicalRoot, isVf);

                        files = Array.Empty<FileSystemMetadata>();
                    }
                    else
                    {
                        throw;
                    }
                }

                // Need to remove subpaths that may have been resolved from shortcuts
                // Example: if \\server\movies exists, then strip out \\server\movies\action
                if (isPhysicalRoot)
                {
                    files = NormalizeRootPathList(files).ToArray();
                }

                args.FileSystemChildren = files;
            }

            // Check to see if we should resolve based on our contents
            if (args.IsDirectory && !ShouldResolvePathContents(args))
            {
                return null;
            }

            return ResolveItem(args);
        }

        public List<FileSystemMetadata> NormalizeRootPathList(IEnumerable<FileSystemMetadata> paths)
        {
            var originalList = paths.ToList();

            var list = originalList.Where(i => i.IsDirectory)
                .Select(i => Path.TrimEndingDirectorySeparator(i.FullName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var dupes = list.Where(subPath => !subPath.EndsWith(":\\", StringComparison.OrdinalIgnoreCase) && list.Any(i => _fileSystem.ContainsSubPath(i, subPath)))
                .ToList();

            foreach (var dupe in dupes)
            {
                _logger.LogInformation("Found duplicate path: {0}", dupe);
            }

            var newList = list.Except(dupes, StringComparer.OrdinalIgnoreCase).Select(_fileSystem.GetDirectoryInfo).ToList();
            newList.AddRange(originalList.Where(i => !i.IsDirectory));
            return newList;
        }

        public IEnumerable<BaseItem> ResolvePaths(
            IEnumerable<FileSystemMetadata> files,
            Folder? parent,
            LibraryOptions libraryOptions,
            string? collectionType = null)
        {
            var fileList = files.Where(i => !IgnoreFile(i, parent!)).ToList();

            if (parent is not null)
            {
                foreach (var resolver in _multiItemResolvers)
                {
                    var result = resolver.ResolveMultiple(parent, fileList, collectionType!, _directoryService);

                    if (result?.Items.Count > 0)
                    {
                        var items = result.Items;
                        items.RemoveAll(item => !SetInitialItemValues(item, parent, _libraryItemIdGenerator, _directoryService ));
                        items.AddRange(ResolveFileList(result.ExtraFiles, parent, collectionType!, libraryOptions));
                        return items;
                    }
                }
            }

            return ResolveFileList(fileList, parent!, collectionType!, libraryOptions);
        }

        /// <summary>
        /// Determines whether a path should be ignored based on its contents - called after the contents have been read.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        private static bool ShouldResolvePathContents(ItemResolveArgs args)
        {
            // Ignore any folders containing a file called .ignore
            return !args.ContainsFileSystemEntryByName(".ignore");
        }

        public bool IgnoreFile(FileSystemMetadata file, BaseItem parent)
            => _resolverIgnoreRules.Any(r => r.ShouldIgnore(file, parent));

        private IEnumerable<BaseItem> ResolveFileList(
            IReadOnlyList<FileSystemMetadata> fileList,
            Folder parent,
            string collectionType,
            LibraryOptions libraryOptions)
        {
            // Given that fileList is a list we can save enumerator allocations by indexing
            for (var i = 0; i < fileList.Count; i++)
            {
                var file = fileList[i];
                BaseItem result = null!;
                try
                {
                    result = ResolvePath(file, Array.Empty<IItemResolver>(), parent, collectionType, libraryOptions)!;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resolving path {Path}", file.FullName);
                }

                if (result is not null)
                {
                    yield return result;
                }
            }
        }

        /// <summary>
        /// Resolves the item.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns>BaseItem.</returns>
        private BaseItem? ResolveItem(ItemResolveArgs args)
        {
            var item = _itemResolvers.Select(r => Resolve(args, r))
                .FirstOrDefault(i => i is not null);

            if (item is not null)
            {
                SetInitialItemValues(item, args, _fileSystem, _libraryItemIdGenerator);
            }

            return item;
        }

        private BaseItem? Resolve(ItemResolveArgs args, IItemResolver resolver)
        {
            try
            {
                return resolver.ResolvePath(args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in {Resolver} resolving {Path}", resolver.GetType().Name, args.Path);
                return null;
            }
        }

        /// <summary>
        /// Sets the initial item values.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="parent">The parent.</param>
        /// <param name="libraryItemIdGenerator">The library item id generator manager.</param>
        /// <param name="directoryService">The directory service.</param>
        /// <returns>True if initializing was successful.</returns>
        /// <exception cref="ArgumentException">Item must have a path.</exception>
        private bool SetInitialItemValues(BaseItem item, Folder? parent, ILibraryItemIdGenerator libraryItemIdGenerator, IDirectoryService directoryService)
        {
            // This version of the below method has no ItemResolveArgs, so we have to require the path already being set
            ArgumentException.ThrowIfNullOrEmpty(item.Path);

            // If the resolver didn't specify this
            if (parent is not null)
            {
                item.SetParent(parent);
            }

            item.Id = libraryItemIdGenerator.Generate(item.Path, item.GetType());

            item.IsLocked = item.Path.IndexOf("[dontfetchmeta]", StringComparison.OrdinalIgnoreCase) != -1 ||
                item.GetParents().Any(i => i.IsLocked);

            // Make sure DateCreated and DateModified have values
            var fileInfo = directoryService.GetFile(item.Path);
            if (fileInfo is null)
            {
                return false;
            }

            SetDateCreated(item, fileInfo);

            EnsureName(item, fileInfo);

            return true;
        }

        /// <summary>
        /// Sets the initial item values.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="args">The args.</param>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="libraryItemIdGenerator">The library item id generator.</param>
        public void SetInitialItemValues(BaseItem item, ItemResolveArgs args, IFileSystem fileSystem, ILibraryItemIdGenerator libraryItemIdGenerator)
        {
            // If the resolver didn't specify this
            if (string.IsNullOrEmpty(item.Path))
            {
                item.Path = args.Path;
            }

            // If the resolver didn't specify this
            if (args.Parent is not null)
            {
                item.SetParent(args.Parent);
            }

            item.Id = libraryItemIdGenerator.Generate(item.Path, item.GetType());

            // Make sure the item has a name
            EnsureName(item, args.FileInfo);

            item.IsLocked = item.Path.Contains("[dontfetchmeta]", StringComparison.OrdinalIgnoreCase) ||
                item.GetParents().Any(i => i.IsLocked);

            // Make sure DateCreated and DateModified have values
            EnsureDates(fileSystem, item, args);
        }

        /// <summary>
        /// Ensures the name.
        /// </summary>
        private void EnsureName(BaseItem item, FileSystemMetadata fileInfo)
        {
            // If the subclass didn't supply a name, add it here
            if (string.IsNullOrEmpty(item.Name) && !string.IsNullOrEmpty(item.Path))
            {
                item.Name = fileInfo.IsDirectory ? fileInfo.Name : Path.GetFileNameWithoutExtension(fileInfo.Name);
            }
        }

        /// <summary>
        /// Ensures DateCreated and DateModified have values.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="item">The item.</param>
        /// <param name="args">The args.</param>
        private void EnsureDates(IFileSystem fileSystem, BaseItem item, ItemResolveArgs args)
        {
            // See if a different path came out of the resolver than what went in
            if (!fileSystem.AreEqual(args.Path, item.Path))
            {
                var childData = args.IsDirectory ? args.GetFileSystemEntryByPath(item.Path) : null;

                if (childData is not null)
                {
                    SetDateCreated(item, childData);
                }
                else
                {
                    var fileData = fileSystem.GetFileSystemInfo(item.Path);

                    if (fileData.Exists)
                    {
                        SetDateCreated(item, fileData);
                    }
                }
            }
            else
            {
                SetDateCreated(item, args.FileInfo);
            }
        }

        private void SetDateCreated(BaseItem item, FileSystemMetadata? info)
        {
            var config  = _serverConfigurationManager.GetMetadataConfiguration();

            if (config.UseFileCreationTimeForDateAdded)
            {
                // directoryService.getFile may return null
                if (info is not null)
                {
                    var dateCreated = info.CreationTimeUtc;

                    if (dateCreated.Equals(DateTime.MinValue))
                    {
                        dateCreated = DateTime.UtcNow;
                    }

                    item.DateCreated = dateCreated;
                }
            }
            else
            {
                item.DateCreated = DateTime.UtcNow;
            }
        }
    }
}
