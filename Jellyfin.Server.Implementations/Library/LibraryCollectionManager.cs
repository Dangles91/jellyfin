using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Extensions;
using MediaBrowser.Common;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Library
{
    /// <summary>
    /// Manage library collections.
    /// </summary>
    public class LibraryCollectionManager : ILibraryCollectionManager
    {
        private readonly ILibraryRootFolderManager _libraryRootFolderManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="LibraryCollectionManager"/> class.
        /// </summary>
        /// <param name="libraryRootFolderManager">The library root folder manager.</param>
        public LibraryCollectionManager(
            ILibraryRootFolderManager libraryRootFolderManager)
        {
            _libraryRootFolderManager = libraryRootFolderManager;
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
    }
}
