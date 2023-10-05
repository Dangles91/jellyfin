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
    /// Essentially helper methods for buildig InternalItemQuery.
    /// </summary>
    public interface IItemQueryBuilder
    {
        /// <summary>
        /// Add user view details to the query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="user">The user.</param>
        /// <param name="allowExternalContent">Whether to allow external content.</param>
        void AddUserToQuery(InternalItemsQuery query, User user, bool allowExternalContent = true);

        /// <summary>
        /// Get top parent ids for the item used in a query.
        /// </summary>
        /// <param name="item">The item being used.</param>
        /// <param name="user">The user being used.</param>
        /// <returns>List of items Ids.</returns>
        IEnumerable<Guid> GetTopParentIdsForQuery(BaseItem item, User user);

        /// <summary>
        /// Set top parent or ancestor ids onto query.
        /// </summary>
        /// <param name="query">The query being used.</param>
        /// <param name="parents">The list of parents.</param>
        void SetTopParentIdsOrAncestors(InternalItemsQuery query, IReadOnlyCollection<BaseItem> parents);

        /// <summary>
        /// Set to parent or ancestor ids on the query.
        /// </summary>
        /// <param name="query">The query being used.</param>
        void SetTopParentOrAncestorIds(InternalItemsQuery query);
    }
}
