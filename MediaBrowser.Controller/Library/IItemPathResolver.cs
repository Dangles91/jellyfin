using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Resolve library items from paths.
    /// </summary>
    public interface IItemPathResolver
    {
        /// <summary>
        /// Adds the parts.
        /// </summary>
        /// <param name="itemResolvers">The item resolvers.</param>
        /// <param name="rules">The rules.</param>
        public void AddParts(
          IEnumerable<IItemResolver> itemResolvers,
          IEnumerable<IResolverIgnoreRule> rules);

        /// <summary>
        /// Resolves the path.
        /// </summary>
        /// <param name="fileInfo">The file information.</param>
        /// <param name="parent">The parent.</param>
        /// <returns>BaseItem.</returns>
        BaseItem? ResolvePath(FileSystemMetadata fileInfo, Folder? parent = null);

        /// <summary>
        /// Resolves the path.
        /// </summary>
        /// <param name="fileInfo">The file information.</param>
        /// <param name="resolvers">The resolvers.</param>
        /// <param name="parent">The parent.</param>
        /// <param name="collectionType">The collection type.</param>
        /// <param name="libraryOptions">The library options.</param>
        /// <returns>BaseItem.</returns>
        public BaseItem? ResolvePath(
            FileSystemMetadata fileInfo,
            IItemResolver[] resolvers,
            Folder? parent = null,
            string? collectionType = null,
            LibraryOptions? libraryOptions = null);

        /// <summary>
        /// Resolves a set of files into a list of BaseItem.
        /// </summary>
        /// <param name="files">The list of tiles.</param>
        /// <param name="parent">The parent folder.</param>
        /// <param name="libraryOptions">The library options.</param>
        /// <param name="collectionType">The collection type.</param>
        /// <returns>The items resolved from the paths.</returns>
        IEnumerable<BaseItem> ResolvePaths(
            IEnumerable<FileSystemMetadata> files,
            Folder parent,
            LibraryOptions libraryOptions,
            string? collectionType = null);

        /// <summary>
        /// Normalizes the root path list.
        /// </summary>
        /// <param name="paths">The paths.</param>
        /// <returns>IEnumerable{System.String}.</returns>
        List<FileSystemMetadata> NormalizeRootPathList(IEnumerable<FileSystemMetadata> paths);

        /// <summary>
        /// Ignores the file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="parent">The parent.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool IgnoreFile(FileSystemMetadata file, BaseItem parent);
    }
}
