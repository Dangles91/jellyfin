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
        Guid GetItemIdFromPath<T>(string path)
            where T : BaseItem, new();

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
        /// Gets the people.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>List&lt;PersonInfo&gt;.</returns>
        List<PersonInfo> GetPeople(BaseItem item);

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

        /// <summary>
        /// Gets the person.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{Person}.</returns>
        Person GetPerson(string name);

        /// <summary>
        /// Get all artist names.
        /// </summary>
        /// <returns>List of artist names.</returns>
        List<string> GetAllArtistNames();

        /// <summary>
        /// Get all studio names.
        /// </summary>
        /// <returns>A list of studio names.</returns>
        List<string> GetStudioNames();
    }
}
