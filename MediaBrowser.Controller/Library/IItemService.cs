using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Service wrapper for item repository.
    /// </summary>
    public interface IItemService
    {
        /// <summary>
        /// Occurs when [item added].
        /// </summary>
        event EventHandler<ItemChangeEventArgs> ItemAdded;

        /// <summary>
        /// Occurs when [item updated].
        /// </summary>
        event EventHandler<ItemChangeEventArgs> ItemUpdated;

        /// <summary>
        /// Occurs when [item removed].
        /// </summary>
        event EventHandler<ItemRemovedEventArgs> ItemRemoved;

        /// <summary>
        /// Create an item by name.
        /// </summary>
        /// <typeparam name="T">Item type.</typeparam>
        /// <param name="getPathFn">The get path function.</param>
        /// <param name="name">The item name.</param>
        /// <param name="options">Dto options.</param>
        /// <returns>Instance of T.</returns>
        T CreateItemByName<T>(Func<string, string> getPathFn, string name, DtoOptions options)
            where T : BaseItem, new();

        /// <summary>
        /// Delete an item.
        /// </summary>
        /// <param name="item">The item to delete.</param>
        /// <param name="parent">The parent of the item.</param>
        /// <param name="deleteOptions">The delete options.</param>
        void DeleteItem(BaseItem item, BaseItem parent, DeleteOptions? deleteOptions);

        /// <summary>
        /// Delete an item.
        /// </summary>
        /// <param name="item">The item to delete.</param>
        void DeleteItem(BaseItem item);

        /// <summary>
        /// Delete an item.
        /// </summary>
        /// <param name="item">The item to delete.</param>
        /// <param name="deleteOptions">The delete options.</param>
        void DeleteItem(BaseItem item, DeleteOptions? deleteOptions);

        /// <summary>
        /// Finds the by path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="isFolder"><c>true</c> is the path is a directory; otherwise <c>false</c>.</param>
        /// <returns>BaseItem.</returns>
        BaseItem FindItemByPath(string path, bool? isFolder);

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
        /// Get artist.
        /// </summary>
        /// <param name="name">The name of the artist.</param>
        /// <returns>Music artist with matching name.</returns>
        MusicArtist GetArtist(string name);

        /// <summary>
        /// Get artist.
        /// </summary>
        /// <param name="name">The name of the artist.</param>
        /// <param name="options">The dto options.</param>
        /// <returns>Result from query.</returns>
        MusicArtist GetArtist(string name, DtoOptions options);

        /// <summary>
        /// Get artists.
        /// </summary>
        /// <param name="query">The query to use.</param>
        /// <returns>Results from query.</returns>
        QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetArtists(InternalItemsQuery query);

        /// <summary>
        /// Gets a single chapter for an item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="index">The chapter index.</param>
        /// <returns>The chapter info at the specified index.</returns>
        ChapterInfo GetChapter(BaseItem item, int index);

        /// <summary>
        /// Gets chapters for an item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The list of chapter info.</returns>
        List<ChapterInfo> GetChapters(BaseItem item);

        /// <summary>
        /// Get a count of items matching the query.
        /// </summary>
        /// <param name="query">The query to use.</param>
        /// <returns>The number of items matching the query.</returns>
        int GetCount(InternalItemsQuery query);

        /// <summary>
        /// Get genres.
        /// </summary>
        /// <param name="query">The query to use.</param>
        /// <returns>Results from the query.</returns>
        QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetGenres(InternalItemsQuery query);

        /// <summary>
        /// Get an item by id.
        /// </summary>
        /// <param name="id">The id of the item.</param>
        /// <returns>Item with matching id.</returns>
        BaseItem GetItemById(Guid id);

        /// <summary>
        /// Get an item by name id.
        /// </summary>
        /// <typeparam name="T">The item type to retrieve.</typeparam>
        /// <param name="path">Item path.</param>
        /// <returns>The matching item.</returns>
        Guid GetItemByNameId<T>(string path)
            where T : BaseItem, new();

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
        /// Registers the item.
        /// </summary>
        /// <param name="item">The item.</param>
        void RegisterItemInCache(BaseItem item);

        /// <summary>
        /// Retrieves the item.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>BaseItem.</returns>
        BaseItem RetrieveItem(Guid id);

        /// <summary>
        /// Gets the people.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>List&lt;PersonInfo&gt;.</returns>
        List<PersonInfo> GetPeople(InternalPeopleQuery query);

        /// <summary>
        /// Gets the people names.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>List&lt;System.String&gt;.</returns>
        List<string> GetPeopleNames(InternalPeopleQuery query);

        /// <summary>
        /// Gets a Studio.
        /// </summary>
        /// <param name="name">The name of the studio.</param>
        /// <returns>Task{Studio}.</returns>
        Studio GetStudio(string name);

        /// <summary>
        /// Updates the people.
        /// </summary>
        /// <param name="itemId">The item id.</param>
        /// <param name="people">The people.</param>
        void UpdatePeople(Guid itemId, List<PersonInfo> people);

        /// <summary>
        /// Creates the item.
        /// </summary>
        /// <param name="item">Item to create.</param>
        /// <param name="parent">Parent of new item.</param>
        void CreateItem(BaseItem item, BaseItem parent);

        /// <summary>
        /// Creates the items.
        /// </summary>
        /// <param name="items">Items to create.</param>
        /// <param name="parent">Parent of new items.</param>
        /// <param name="cancellationToken">CancellationToken to use for operation.</param>
        void CreateItems(IReadOnlyList<BaseItem> items, BaseItem parent, CancellationToken cancellationToken);

        /// <summary>
        /// Saves the items.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        void SaveItems(IReadOnlyList<BaseItem> items, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the inherited values.
        /// </summary>
        void UpdateInheritedValues();

        /// <summary>
        /// Save the images of the item.
        /// </summary>
        /// <param name="item">The item for which to save images.</param>
        void SaveImages(BaseItem item);

        /// <summary>
        /// Updates the item.
        /// </summary>
        /// <param name="items">Items to update.</param>
        /// <param name="parent">Parent of updated items.</param>
        /// <param name="updateReason">Reason for update.</param>
        /// <param name="cancellationToken">CancellationToken to use for operation.</param>
        void UpdateItems(IReadOnlyList<BaseItem> items, BaseItem parent, ItemUpdateType updateReason, CancellationToken cancellationToken);

        /// <summary>
        /// Update the item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="parent">The parent.</param>
        /// <param name="updateReason">The reason.</param>
        /// <param name="cancellationToken">The token.</param>
        /// <returns>Task.</returns>
        Task UpdateItemAsync(BaseItem item, BaseItem parent, ItemUpdateType updateReason, CancellationToken cancellationToken);
    }
}
