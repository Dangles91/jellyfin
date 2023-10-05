using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Entities;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Get named views from the library.
    /// </summary>
    public interface ILibraryViewService
    {
        /// <summary>
        /// Gets the named view.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="name">The name.</param>
        /// <param name="parentId">The parent identifier.</param>
        /// <param name="viewType">Type of the view.</param>
        /// <param name="sortName">Name of the sort.</param>
        /// <returns>The named view.</returns>
        UserView GetNamedView(
            User user,
            string name,
            Guid parentId,
            string viewType,
            string sortName);

        /// <summary>
        /// Gets the named view.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="name">The name.</param>
        /// <param name="viewType">Type of the view.</param>
        /// <param name="sortName">Name of the sort.</param>
        /// <returns>The named view.</returns>
        UserView GetNamedView(
            User user,
            string name,
            string viewType,
            string sortName);

        /// <summary>
        /// Gets the named view.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="viewType">Type of the view.</param>
        /// <param name="sortName">Name of the sort.</param>
        /// <returns>The named view.</returns>
        UserView GetNamedView(
            string name,
            string viewType,
            string sortName);

        /// <summary>
        /// Gets the named view.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="parentId">The parent identifier.</param>
        /// <param name="viewType">Type of the view.</param>
        /// <param name="sortName">Name of the sort.</param>
        /// <param name="uniqueId">The unique identifier.</param>
        /// <returns>The named view.</returns>
        UserView GetNamedView(
            string name,
            Guid parentId,
            string viewType,
            string sortName,
            string uniqueId);

        /// <summary>
        /// Gets the shadow view.
        /// </summary>
        /// <param name="parent">The parent.</param>
        /// <param name="viewType">Type of the view.</param>
        /// <param name="sortName">Name of the sort.</param>
        /// <returns>The shadow view.</returns>
        UserView GetShadowView(
            BaseItem parent,
            string viewType,
            string sortName);
    }
}
