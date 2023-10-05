using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Perform comples queries for items.
    /// </summary>
    public interface IItemQueryService
    {
        /// <summary>
        /// Get album artists.
        /// </summary>
        /// <param name="query">The item query to use.</param>
        /// <returns>Result from query.</returns>
        QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetAlbumArtists(InternalItemsQuery query);

        /// <summary>
        /// Get all artists.
        /// </summary>
        /// <param name="query">The item query to use.</param>
        /// <returns>Result from query.</returns>
        QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetAllArtists(InternalItemsQuery query);

        /// <summary>
        /// Get artists.
        /// </summary>
        /// <param name="query">The query to use.</param>
        /// <returns>Results from query.</returns>
        QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetArtists(InternalItemsQuery query);

        /// <summary>
        /// Get genres.
        /// </summary>
        /// <param name="query">The query to use.</param>
        /// <returns>Results from the query.</returns>
        QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetGenres(InternalItemsQuery query);

        /// <summary>
        /// Gets the item ids.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>List&lt;Guid&gt;.</returns>
        List<Guid> GetItemIds(InternalItemsQuery query);

        /// <summary>
        /// Gets the items.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>QueryResult&lt;BaseItem&gt;.</returns>
        List<BaseItem> GetItemList(InternalItemsQuery query);

        /// <summary>
        /// Gets the items.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="allowExternalContent">Allow exernal content.</param>
        /// <returns>QueryResult&lt;BaseItem&gt;.</returns>
        List<BaseItem> GetItemList(InternalItemsQuery query, bool allowExternalContent);

        /// <summary>
        /// Gets the items.
        /// </summary>
        /// <param name="query">The query to use.</param>
        /// <param name="parents">Items to use for query.</param>
        /// <returns>List of items.</returns>
        List<BaseItem> GetItemList(InternalItemsQuery query, List<BaseItem> parents);

        /// <summary>
        /// Gets the items result.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>QueryResult&lt;BaseItem&gt;.</returns>
        QueryResult<BaseItem> GetItemsResult(InternalItemsQuery query);

        /// <summary>
        /// Get music genres.
        /// </summary>
        /// <param name="query">The query to use.</param>
        /// <returns>Reuslts from the query.</returns>
        QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetMusicGenres(InternalItemsQuery query);

        /// <summary>
        /// Get studios.
        /// </summary>
        /// <param name="query">The query to use.</param>
        /// <returns>Reuslts from the query.</returns>
        QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetStudios(InternalItemsQuery query);

        /// <summary>
        /// Queries the items.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>QueryResult&lt;BaseItem&gt;.</returns>
        QueryResult<BaseItem> QueryItems(InternalItemsQuery query);

        /// <summary>
        /// Get a count of items matching the query.
        /// </summary>
        /// <param name="query">The query to use.</param>
        /// <returns>The number of items matching the query.</returns>
        int GetCount(InternalItemsQuery query);

        /// <summary>
        /// Finds the by path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="isFolder"><c>true</c> is the path is a directory; otherwise <c>false</c>.</param>
        /// <returns>BaseItem.</returns>
        BaseItem FindItemByPath(string path, bool? isFolder);
    }
}
