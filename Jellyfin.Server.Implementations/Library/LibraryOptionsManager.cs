using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Serialization;
using CacheManager.Core.Logging;
using Jellyfin.Extensions.Json;
using MediaBrowser.Common;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Library
{
    /// <summary>
    /// Manage library (collection) options.
    /// </summary>
    public class LibraryOptionsManager : ILibraryOptionsManager
    {
        private static readonly ConcurrentDictionary<string, LibraryOptions> _libraryOptionsCache = new();
        private static readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Options;

        private readonly ILibraryCollectionManager _libraryCollectionManager;
        private readonly IFileSystem _fileSystem;
        private readonly IXmlSerializer _xmlSerializer;
        private readonly ILogger<LibraryOptionsManager> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LibraryOptionsManager"/> class.
        /// </summary>
        /// <param name="libraryCollectionManager">The configured <see cref="ILibraryCollectionManager"/>.</param>
        /// <param name="fileSystem">The <see cref="IFileSystem"/> to use.</param>
        /// <param name="xmlSerializer">The <see cref="IXmlSerializer"/> to use.</param>
        /// <param name="logger">The logger for this class.</param>
        public LibraryOptionsManager(
            ILibraryCollectionManager libraryCollectionManager,
            IFileSystem fileSystem,
            IXmlSerializer xmlSerializer,
            ILogger<LibraryOptionsManager> logger)
        {
            _libraryCollectionManager = libraryCollectionManager;
            _fileSystem = fileSystem;
            _xmlSerializer = xmlSerializer;
            _logger = logger;
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
                collectionFolder = _libraryCollectionManager.GetCollectionFolders(item)
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
    }
}
