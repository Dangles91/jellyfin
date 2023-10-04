using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;

namespace Emby.Server.Implementations.Library
{
    /// <summary>
    /// Get item content types from server configuration.
    /// </summary>
    public class ItemContentTypeProvider : IItemContentTypeProvider
    {
        private readonly IServerConfigurationManager _serverConfigurationManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILibraryRootFolderManager _libraryRootFolderManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemContentTypeProvider"/> class.
        /// </summary>
        /// <param name="serverConfigurationManager">The instance of <see cref="IServerConfigurationManager"/> interface.</param>
        /// <param name="fileSystem">The instance of <see cref="IFileSystem"/> interface.</param>
        /// <param name="libraryRootFolderManager">The root folder manager.</param>
        public ItemContentTypeProvider(
            IServerConfigurationManager serverConfigurationManager,
            IFileSystem fileSystem,
            ILibraryRootFolderManager libraryRootFolderManager)
        {
            _serverConfigurationManager = serverConfigurationManager;
            _fileSystem = fileSystem;
            _libraryRootFolderManager = libraryRootFolderManager;
        }

        /// <inheritdoc/>
        public string? GetContentType(BaseItem item)
        {
            string? configuredContentType = GetConfiguredContentType(item, false);
            if (!string.IsNullOrEmpty(configuredContentType))
            {
                return configuredContentType;
            }

            configuredContentType = GetConfiguredContentType(item, true);
            if (!string.IsNullOrEmpty(configuredContentType))
            {
                return configuredContentType;
            }

            return GetInheritedContentType(item);
        }

        /// <inheritdoc/>
        public string? GetInheritedContentType(BaseItem item)
        {
            var type = GetTopFolderContentType(item);

            if (!string.IsNullOrEmpty(type))
            {
                return type;
            }

            return item.GetParents()
                .Select(GetConfiguredContentType)
                .LastOrDefault(i => !string.IsNullOrEmpty(i));
        }

        /// <inheritdoc/>
        public string? GetConfiguredContentType(BaseItem item)
        {
            return GetConfiguredContentType(item, false);
        }

        /// <inheritdoc/>
        public string? GetConfiguredContentType(string path)
        {
            return GetContentTypeOverride(path, false);
        }

        /// <inheritdoc/>
        public string? GetConfiguredContentType(BaseItem item, bool inheritConfiguredPath)
        {
            if (item is ICollectionFolder collectionFolder)
            {
                return collectionFolder.CollectionType;
            }

            return GetContentTypeOverride(item.ContainingFolderPath, inheritConfiguredPath);
        }

        /// <inheritdoc/>
        public string? GetContentTypeOverride(string path, bool inheritConfiguredPath)
        {
            var nameValuePair = _serverConfigurationManager.Configuration.ContentTypes
                                    .FirstOrDefault(i => _fileSystem.AreEqual(i.Name, path)
                                                         || (inheritConfiguredPath && !string.IsNullOrEmpty(i.Name)
                                                            && _fileSystem.ContainsSubPath(i.Name, path)));
            return nameValuePair?.Value;
        }

        private string GetTopFolderContentType(BaseItem item)
        {
            if (item is null)
            {
                return null!;
            }

            while (!item.ParentId.Equals(Guid.Empty))
            {
                var parent = item.GetParent();
                if (parent is null || parent is AggregateFolder)
                {
                    break;
                }

                item = parent;
            }

            return _libraryRootFolderManager.GetUserRootFolder().Children
                .OfType<ICollectionFolder>()
                .Where(i => string.Equals(i.Path, item.Path, StringComparison.OrdinalIgnoreCase) || i.PhysicalLocations.Contains(item.Path))
                .Select(i => i.CollectionType)
                .FirstOrDefault(i => !string.IsNullOrEmpty(i))!;
        }
    }
}
