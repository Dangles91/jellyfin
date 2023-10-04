using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Manage library collection folders.
    /// </summary>
    public interface ILibraryCollectionManager
    {
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
    }
}
