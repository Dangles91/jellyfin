using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        /// ok.
        /// </summary>
        /// <param name="name">yes.</param>
        /// <param name="collectionType">no.</param>
        /// <param name="options">ok. then.</param>
        /// <param name="refreshLibrary">yedd.</param>
        /// <returns>ret.</returns>
        Task AddVirtualFolder(string name, CollectionTypeOptions? collectionType, LibraryOptions options, bool refreshLibrary);
    }
}
