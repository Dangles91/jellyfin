using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Configuration;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Manage library options.
    /// </summary>
    public interface ILibraryOptionsManager
    {
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
    }
}
