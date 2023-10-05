using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Virt manager.
    /// </summary>
    public interface IVirtualFolderManager
    {
        /// <summary>
        /// Add a new virtual folder.
        /// </summary>
        /// <param name="name">The name of the new virtual folder.</param>
        /// <param name="collectionType">The collection type.</param>
        /// <param name="options">The library options of the new folder.</param>
        /// <param name="refreshLibrary">Whether to refresh the library.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task AddVirtualFolder(string name, CollectionTypeOptions? collectionType, LibraryOptions options, bool refreshLibrary);

        /// <summary>
        /// Gets the default view.
        /// </summary>
        /// <returns>IEnumerable{VirtualFolderInfo}.</returns>
        List<VirtualFolderInfo> GetVirtualFolders();

        /// <summary>
        /// Get a list containing the information about virutal folders.
        /// </summary>
        /// <param name="includeRefreshState">Whether to include refresh state.</param>
        /// <returns>A list of virtual folder information.</returns>
        List<VirtualFolderInfo> GetVirtualFolders(bool includeRefreshState);

        /// <summary>
        /// Gets the collection folders.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The folders that contain the item.</returns>
        List<Folder> GetCollectionFolders(BaseItem item);

        /// <summary>
        /// Gets the collection folders.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="allUserRootChildren">The root folders to consider.</param>
        /// <returns>The folders that contain the item.</returns>
        List<Folder> GetCollectionFolders(BaseItem item, IEnumerable<Folder> allUserRootChildren);

        /// <summary>
        /// Clear the internal library options cache.
        /// </summary>
        void ClearOptionsCache();

        /// <summary>
        /// Retrieve the library options for the item. If the items belongs to multiple collections,
        /// only the options of the first collection is returned.
        /// </summary>
        /// <param name="item">The item to retrieve options for.</param>
        /// <returns>The <see cref="LibraryOptions"/> for the parent library.</returns>
        LibraryOptions GetLibraryOptions(BaseItem item);

        /// <summary>
        /// Retrieve the library options for the item. If the items belongs to multiple collections,
        /// only the options of the first collection is returned.
        /// </summary>
        /// <param name="path">The item path to retrieve options for.</param>
        /// <returns>The <see cref="LibraryOptions"/> for the parent library.</returns>
        LibraryOptions GetLibraryOptions(string path);

        /// <summary>
        /// Save library options.
        /// </summary>
        /// <param name="path">The path of the library options.</param>
        /// <param name="options">The library options.</param>
        void SaveLibraryOptions(string path, LibraryOptions options);

        /// <summary>
        /// Update library options for a given item.
        /// </summary>
        /// <param name="item">The item to update.</param>
        /// <param name="options">The library options.</param>
        void UpdateLibraryOptions(BaseItem item, LibraryOptions? options);

        /// <summary>
        /// Remove a virtual folder.
        /// </summary>
        /// <param name="name">The name of the virtual folder.</param>
        /// <param name="refreshLibrary">Whether to refresh the library after removing.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task RemoveVirtualFolder(string name, bool refreshLibrary);

        /// <summary>
        /// Add a media path to a virtual folder. Used for library folders.
        /// </summary>
        /// <param name="virtualFolderName">The name of the virtual folder.</param>
        /// <param name="mediaPath">The media path to add.</param>
        void AddMediaPath(string virtualFolderName, MediaPathInfo mediaPath);

        /// <summary>
        /// Update a virtual media path on a virtual folder.
        /// </summary>
        /// <param name="virtualFolderName">The virutal folder to update.</param>
        /// <param name="mediaPath">The media path to update.</param>
        void UpdateMediaPath(string virtualFolderName, MediaPathInfo mediaPath);

        /// <summary>
        /// Remove a media path from a virtual folder.
        /// </summary>
        /// <param name="virtualFolderName">The name of the virtual folder to update.</param>
        /// <param name="mediaPath">The media path to remove.</param>
        void RemoveMediaPath(string virtualFolderName, string mediaPath);
    }
}
