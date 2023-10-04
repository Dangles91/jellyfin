using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Events args sent when an item is removed from the library.
    /// </summary>
    public class ItemRemovedEventArgs : ItemChangeEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ItemRemovedEventArgs"/> class.
        /// </summary>
        /// <param name="deleteOptions">The delete options used when deleting the item.</param>
        public ItemRemovedEventArgs(DeleteOptions deleteOptions)
        {
            DeleteOptions = deleteOptions;
        }

        /// <summary>
        /// Gets the delete options used when removing the item.
        /// </summary>
        public DeleteOptions DeleteOptions { get; }
    }
}
