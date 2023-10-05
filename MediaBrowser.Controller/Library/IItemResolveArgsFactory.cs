using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Factory for creating <see cref="ItemResolveArgs"/>.
    /// </summary>
    public interface IItemResolveArgsFactory
    {
        /// <summary>
        /// Creates a new items resolve args.
        /// </summary>
        /// <param name="parent">The parent.</param>
        /// <param name="fileInfo">The fileInfo.</param>
        /// <param name="collectionType">The collection type.</param>
        /// <param name="libraryOptions">The library options.</param>
        /// <returns>A new <see cref="ItemResolveArgs"/> instance.</returns>
        ItemResolveArgs Create(Folder? parent, FileSystemMetadata fileInfo, string? collectionType, LibraryOptions? libraryOptions);
    }
}
