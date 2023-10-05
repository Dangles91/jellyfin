using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;

namespace Emby.Server.Implementations.Library
{
    /// <summary>
    /// Factory for creating <see cref="ItemResolveArgs"/>.
    /// </summary>
    public class ItemResolveArgsFactory : IItemResolveArgsFactory
    {
        private readonly IServerApplicationPaths _appPaths;
        private readonly ILibraryOptionsManager _libraryOptionsManager;
        private readonly IResolverIgnoreRulesProvider _ignoreRulesProvider;
        private readonly IItemContentTypeProvider _itemContentTypeProvider;
        private readonly IFileSystem _fileSystem;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemResolveArgsFactory" /> class.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <param name="libraryOptionsManager">The library options.</param>
        /// <param name="ignoreRulesProvider">Provider for ignore rules.</param>
        /// <param name="itemContentTypeProvider">The item content type provider.</param>
        /// <param name="fileSystem">The instance of <see cref="IFileSystem"/> interface.</param>
        public ItemResolveArgsFactory(
            IServerApplicationPaths appPaths,
            ILibraryOptionsManager libraryOptionsManager,
            IResolverIgnoreRulesProvider ignoreRulesProvider,
            IItemContentTypeProvider itemContentTypeProvider,
            IFileSystem fileSystem)
        {
            _appPaths = appPaths;
            _libraryOptionsManager = libraryOptionsManager;
            _ignoreRulesProvider = ignoreRulesProvider;
            _itemContentTypeProvider = itemContentTypeProvider;
            _fileSystem = fileSystem;
        }

        /// <inheritdoc/>
        public ItemResolveArgs Create(
            Folder? parent,
            FileSystemMetadata fileInfo,
            string? collectionType,
            LibraryOptions? libraryOptions)
        {
            ItemResolveArgs args = new(_appPaths, _libraryOptionsManager, _itemContentTypeProvider, _fileSystem, _ignoreRulesProvider)
            {
                Parent = parent,
                FileInfo = fileInfo,
                CollectionType = collectionType,
                LibraryOptions = libraryOptions
            };

            return args;
        }
    }
}
