using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Genereates new item IDs.
    /// </summary>
    public interface ILibraryItemIdGenerator
    {
        /// <summary>
        /// Generate an ID for a new library item.
        /// </summary>
        /// <typeparam name="T">The object type of the item.</typeparam>
        /// <param name="key">The key to seed the ID with. Usually item path.</param>
        /// <returns>A new item ID.</returns>
        Guid Generate<T>(string key)
            where T : BaseItem;

        /// <summary>
        /// Generate an ID for a new library item.
        /// </summary>
        /// <param name="key">The key to seed the ID with. Usually item path.</param>
        /// <param name="itemType">The object type of the item.</param>
        /// <returns>A new item ID.</returns>
        Guid Generate(string key, Type itemType);
    }
}
