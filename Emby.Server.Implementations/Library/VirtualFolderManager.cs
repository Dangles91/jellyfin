using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using Jellyfin.Extensions;
using Jellyfin.Extensions.Json;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Library
{
    /// <summary>
    /// Manage virtual folders and their options.
    /// </summary>
    public class VirtualFolderManager : IVirtualFolderManager
    {
        private const string ShortcutFileExtension = ".mblink";

        private static readonly ConcurrentDictionary<string, LibraryOptions> _libraryOptionsCache = new();
        private static readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Options;

        private readonly ILibraryMonitorOrchestrator _libraryMonitorOrchestrator;
        private readonly IFileSystem _fileSystem;
        private readonly IServerConfigurationManager _configurationManager;
        private readonly ILibraryRootFolderManager _libraryRootFolderManager;
        private readonly ITaskManager _taskManager;
        private readonly IXmlSerializer _xmlSerializer;
        private readonly ILogger<VirtualFolderManager> _logger;
        private readonly IItemRefreshTaskManager _itemRefreshTaskManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualFolderManager"/> class.
        /// </summary>
        /// <param name="libraryMonitorOrchestrator">The library monitor orchestrator.</param>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="xmlSerializer">The xml serialzier.</param>
        /// <param name="configurationManager">Config manager.</param>
        /// <param name="libraryRootFolderManager">The library root folder manager.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="itemRefreshTaskManager">The refresh task manager.</param>
        /// <param name="taskManager">The task manager.</param>
        public VirtualFolderManager(
            ILibraryMonitorOrchestrator libraryMonitorOrchestrator,
            IFileSystem fileSystem,
            IXmlSerializer xmlSerializer,
            IServerConfigurationManager configurationManager,
            ILibraryRootFolderManager libraryRootFolderManager,
            ILogger<VirtualFolderManager> logger,
            IItemRefreshTaskManager itemRefreshTaskManager,
            ITaskManager taskManager)
        {
            _libraryMonitorOrchestrator = libraryMonitorOrchestrator;
            _fileSystem = fileSystem;
            _xmlSerializer = xmlSerializer;
            _configurationManager = configurationManager;
            _libraryRootFolderManager = libraryRootFolderManager;
            _logger = logger;
            _itemRefreshTaskManager = itemRefreshTaskManager;
            _taskManager = taskManager;
        }

        /// <inheritdoc/>
        public AggregateFolder GetRootFolder()
        {
            return _libraryRootFolderManager.GetRootFolder();
        }

        /// <inheritdoc/>
        public Folder GetUserRootFolder()
        {
            return _libraryRootFolderManager.GetRootFolder();
        }

        /// <summary>
        /// Gets the default view.
        /// </summary>
        /// <returns>IEnumerable{VirtualFolderInfo}.</returns>
        public List<VirtualFolderInfo> GetVirtualFolders()
        {
            return GetVirtualFolders(false);
        }

        /// <inheritdoc/>
        public List<VirtualFolderInfo> GetVirtualFolders(bool includeRefreshState)
        {
            _logger.LogDebug("Getting topLibraryFolders");
            var topLibraryFolders = _libraryRootFolderManager.GetUserRootFolder().Children.ToList();

            _logger.LogDebug("Getting refreshQueue");

            var refreshQueue = includeRefreshState ? _itemRefreshTaskManager.GetRefreshQueue() : null;

            return _fileSystem.GetDirectoryPaths(_configurationManager.ApplicationPaths.DefaultUserViewsPath)
                .Select(dir => GetVirtualFolderInfo(dir, topLibraryFolders, refreshQueue!))
                .ToList()!;
        }

        private VirtualFolderInfo? GetVirtualFolderInfo(string dir, List<BaseItem> allCollectionFolders, HashSet<Guid> refreshQueue)
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
                            return _fileSystem.ExpandVirtualPath(_fileSystem.ResolveShortcut(i)!);
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

                info.LibraryOptions = GetLibraryOptions(libraryFolder);

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

        /// <inheritdoc/>
        public List<Folder> GetCollectionFolders(BaseItem item)
        {
            return GetCollectionFolders(item, _libraryRootFolderManager.GetUserRootFolder().Children.OfType<Folder>());
        }

        /// <inheritdoc/>
        public List<Folder> GetCollectionFolders(BaseItem item, IEnumerable<Folder> allUserRootChildren)
        {
            while (item is not null)
            {
                var parent = item.GetParent();

                if (parent is AggregateFolder)
                {
                    break;
                }

                if (parent is null)
                {
                    var owner = item.GetOwner();

                    if (owner is null)
                    {
                        break;
                    }

                    item = owner;
                }
                else
                {
                    item = parent;
                }
            }

            if (item is null)
            {
                return new List<Folder>();
            }

            return GetCollectionFoldersInternal(item, allUserRootChildren);
        }

        private static List<Folder> GetCollectionFoldersInternal(BaseItem item, IEnumerable<Folder> allUserRootChildren)
        {
            return allUserRootChildren
                .Where(i => string.Equals(i.Path, item.Path, StringComparison.OrdinalIgnoreCase) ||
                    i.PhysicalLocations.Contains(item.Path.AsSpan(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <inheritdoc/>
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

            _libraryMonitorOrchestrator.RequestStop();

            try
            {
                Directory.CreateDirectory(virtualFolderPath);

                if (collectionType is not null)
                {
                    var path = Path.Combine(virtualFolderPath, collectionType?.ToString().ToLowerInvariant() + ".collection");

                    File.WriteAllBytes(path, Array.Empty<byte>());
                }

                SaveLibraryOptions(virtualFolderPath, options);

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
                    await _libraryRootFolderManager.ValidateTopLibraryFolders(CancellationToken.None).ConfigureAwait(false);

                    _taskManager.CancelIfRunningAndQueue<RefreshMediaLibraryTask>();
                }
                else
                {
                    // Need to add a delay here or directory watchers may still pick up the changes
                    await Task.Delay(1000).ConfigureAwait(false);
                    _libraryMonitorOrchestrator.RequestStart();
                }
            }
        }

        /// <inheritdoc/>
        public void ClearOptionsCache()
        {
            _libraryOptionsCache.Clear();
        }

        /// <inheritdoc/>
        public LibraryOptions GetLibraryOptions(BaseItem item)
        {
            CollectionFolder? collectionFolder = null;
            if (item is not CollectionFolder)
            {
                // List.Find is more performant than FirstOrDefault due to enumerator allocation
                collectionFolder = GetCollectionFolders(item)
                    .Find(folder => folder is CollectionFolder) as CollectionFolder;
            }

            if (collectionFolder is null)
            {
                return new();
            }

            return GetLibraryOptions(item.Path);
        }

        /// <inheritdoc/>
        public LibraryOptions GetLibraryOptions(string path)
        {
            return _libraryOptionsCache.GetOrAdd(path, LoadLibraryOptions(path));
        }

        private LibraryOptions LoadLibraryOptions(string path)
        {
            try
            {
                if (_xmlSerializer.DeserializeFromFile(typeof(LibraryOptions), GetLibraryOptionsPath(path)) is not LibraryOptions result)
                {
                    return new LibraryOptions();
                }

                foreach (var mediaPath in result.PathInfos)
                {
                    if (!string.IsNullOrEmpty(mediaPath.Path))
                    {
                        mediaPath.Path = _fileSystem.ExpandVirtualPath(mediaPath.Path);
                    }
                }

                return result;
            }
            catch (FileNotFoundException)
            {
                return new LibraryOptions();
            }
            catch (IOException)
            {
                return new LibraryOptions();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading library options");

                return new LibraryOptions();
            }
        }

        private string GetLibraryOptionsPath(string path)
        {
            return System.IO.Path.Combine(path, "options.xml");
        }

        /// <inheritdoc/>
        public void UpdateLibraryOptions(BaseItem item, LibraryOptions? options)
        {
            SaveLibraryOptions(item.Path, options);
        }

        /// <inheritdoc/>
        public void SaveLibraryOptions(string path, LibraryOptions? options)
        {
            _libraryOptionsCache[path] = options!;

            var clone = JsonSerializer.Deserialize<LibraryOptions>(JsonSerializer.SerializeToUtf8Bytes(options, _jsonOptions), _jsonOptions);

            if (clone == null)
            {
                return;
            }

            foreach (var mediaPath in clone.PathInfos)
            {
                if (!string.IsNullOrEmpty(mediaPath.Path))
                {
                    mediaPath.Path = _fileSystem.ReverseVirtualPath(mediaPath.Path);
                }
            }

            _xmlSerializer.SerializeToFile(clone, GetLibraryOptionsPath(path));
        }

        /// <inheritdoc/>
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
                var libraryOptions = GetLibraryOptions(virtualFolderPath);

                var list = libraryOptions.PathInfos.ToList();
                list.Add(pathInfo);

                libraryOptions.PathInfos = list.ToArray();

                SyncLibraryOptionsToLocations(virtualFolderPath, libraryOptions);

                SaveLibraryOptions(virtualFolderPath, libraryOptions);
            }
        }

        /// <inheritdoc/>
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

            _libraryMonitorOrchestrator.RequestStop();

            try
            {
                Directory.Delete(path, true);
            }
            finally
            {
                ClearOptionsCache();

                if (refreshLibrary)
                {
                    await _libraryRootFolderManager.ValidateTopLibraryFolders(CancellationToken.None).ConfigureAwait(false);
                    _taskManager.CancelIfRunningAndQueue<RefreshMediaLibraryTask>();
                }
                else
                {
                    // Need to add a delay here or directory watchers may still pick up the changes
                    await Task.Delay(1000).ConfigureAwait(false);
                    _libraryMonitorOrchestrator.RequestStart();
                }
            }
        }

        private void RemoveContentTypeOverrides(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            List<NameValuePair> removeList = new();

            foreach (var contentType in _configurationManager.Configuration.ContentTypes)
            {
                if (string.IsNullOrWhiteSpace(contentType.Name)
                    || _fileSystem.AreEqual(path, contentType.Name)
                    || _fileSystem.ContainsSubPath(path, contentType.Name))
                {
                    removeList.Add(contentType);
                }
            }

            if (removeList.Any())
            {
                _configurationManager.Configuration.ContentTypes = _configurationManager.Configuration.ContentTypes
                    .Except(removeList)
                    .ToArray();

                _configurationManager.SaveConfiguration();
            }
        }

        /// <inheritdoc/>
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
                .FirstOrDefault(f => _fileSystem.ExpandVirtualPath(_fileSystem.ResolveShortcut(f)!).Equals(mediaPath, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(shortcut))
            {
                _fileSystem.DeleteFile(shortcut);
            }

            var libraryOptions = GetLibraryOptions(virtualFolderPath);

            libraryOptions.PathInfos = libraryOptions
                .PathInfos
                .Where(i => !string.Equals(i.Path, mediaPath, StringComparison.Ordinal))
                .ToArray();

            SaveLibraryOptions(virtualFolderPath, libraryOptions);
        }

        /// <inheritdoc/>
        public void UpdateMediaPath(string virtualFolderName, MediaPathInfo mediaPath)
        {
            ArgumentNullException.ThrowIfNull(mediaPath);

            var rootFolderPath = _configurationManager.ApplicationPaths.DefaultUserViewsPath;
            var virtualFolderPath = Path.Combine(rootFolderPath, virtualFolderName);

            var libraryOptions = GetLibraryOptions(virtualFolderPath);

            SyncLibraryOptionsToLocations(virtualFolderPath, libraryOptions);

            var list = libraryOptions.PathInfos.ToList();
            foreach (var originalPathInfo in list.Where(originalPathInfo => string.Equals(mediaPath.Path, originalPathInfo.Path, StringComparison.Ordinal)))
            {
                originalPathInfo.NetworkPath = mediaPath.NetworkPath;
                break;
            }

            libraryOptions.PathInfos = list.ToArray();

            SaveLibraryOptions(virtualFolderPath, libraryOptions);
        }

        private void SyncLibraryOptionsToLocations(string virtualFolderPath, LibraryOptions options)
        {
            var topLibraryFolders = _libraryRootFolderManager.GetUserRootFolder().Children.ToList();
            var info = GetVirtualFolderInfo(virtualFolderPath, topLibraryFolders, null!);

            if (info!.Locations.Length > 0 && info.Locations.Length != options.PathInfos.Length)
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
    }
}
