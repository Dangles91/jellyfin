#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CacheManager.Core.Logging;
using EasyCaching.Core.Diagnostics;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Entities.Libraries;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Library;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.Extensions.Logging;
using TMDbLib.Objects.Changes;

namespace Emby.Server.Implementations.Library
{
    /// <summary>
    /// Service wrapper ItemRepository.
    /// </summary>
    public class ItemService : IItemService
    {
        private readonly ILibraryItemIdGenerator _libraryItemIdGenerator;
        private readonly IItemRepository _itemRepository;
        private readonly ILogger<ItemService> _logger;
        private readonly ILibraryRootFolderManager _libraryRootFolderManager;
        private readonly ConcurrentDictionary<Guid, BaseItem> _cache = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemService"/> class.
        /// </summary>
        /// <param name="libraryItemIdGenerator">The instance of <see cref="ILibraryItemIdGenerator"/> interface.</param>
        /// <param name="itemRepository">The instance of <see cref="IItemRepository"/> interface.</param>
        /// <param name="logger"></param>
        /// <param name="libraryRootFolderManager"></param>
        public ItemService(
            ILibraryItemIdGenerator libraryItemIdGenerator,
            IItemRepository itemRepository,
            ILogger<ItemService> logger,
            ILibraryRootFolderManager libraryRootFolderManager)
        {
            _libraryItemIdGenerator = libraryItemIdGenerator;
            _itemRepository = itemRepository;
            _logger = logger;
            _libraryRootFolderManager = libraryRootFolderManager;
        }

        /// <summary>
        /// Occurs when [item added].
        /// </summary>
        public event EventHandler<ItemChangeEventArgs> ItemAdded;

        /// <summary>
        /// Occurs when [item updated].
        /// </summary>
        public event EventHandler<ItemChangeEventArgs> ItemUpdated;

        /// <summary>
        /// Occurs when [item removed].
        /// </summary>
        public event EventHandler<ItemRemovedEventArgs> ItemRemoved;

        /// <inheritdoc/>
        public BaseItem FindItemByPath(string path, bool? isFolder)
        {
            // If this returns multiple items it could be tricky figuring out which one is correct.
            // In most cases, the newest one will be and the others obsolete but not yet cleaned up
            ArgumentException.ThrowIfNullOrEmpty(path);

            var query = new InternalItemsQuery
            {
                Path = path,
                IsFolder = isFolder,
                OrderBy = new[] { (ItemSortBy.DateCreated, SortOrder.Descending) },
                Limit = 1,
                DtoOptions = new DtoOptions(true)
            };

            return GetItemList(query)
                .FirstOrDefault()!;
        }

        /// <summary>
        /// Retrieves the item.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>BaseItem.</returns>
        public BaseItem RetrieveItem(Guid id)
        {
            return _itemRepository.RetrieveItem(id);
        }

        /// <inheritdoc/>
        public T CreateItemByName<T>(Func<string, string> getPathFn, string name, DtoOptions options)
            where T : BaseItem, new()
        {
            if (typeof(T) == typeof(MusicArtist))
            {
                var existing = GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.MusicArtist },
                    Name = name,
                    DtoOptions = options
                }).Cast<MusicArtist>()
                .OrderBy(i => i.IsAccessedByName ? 1 : 0)
                .Cast<T>()
                .FirstOrDefault();

                if (existing is not null)
                {
                    return existing;
                }
            }

            var path = getPathFn(name);
            var id = GetItemByNameId<T>(path);
            var item = GetItemById(id) as T;
            if (item is null)
            {
                item = new T
                {
                    Name = name,
                    Id = id,
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow,
                    Path = path
                };

                CreateItem(item, null!);
            }

            return item;
        }

        /// <inheritdoc/>
        public Guid GetItemByNameId<T>(string path)
              where T : BaseItem, new()
        {
            return _libraryItemIdGenerator.Generate(path, typeof(T));
        }

        /// <summary>
        /// Gets the item by id.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>BaseItem.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
        public BaseItem GetItemById(Guid id)
        {
            if (id.Equals(Guid.Empty))
            {
                throw new ArgumentException("Guid can't be empty", nameof(id));
            }

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            if (_cache.TryGetValue(id, out BaseItem item))
            {
                return item;
            }
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

            item = RetrieveItem(id);

            if (item is not null)
            {
                RegisterItemInCache(item);
            }

            return item;
        }

        public List<BaseItem> GetItemList(InternalItemsQuery query, bool allowExternalContent)
        {
            if (query.Recursive && !query.ParentId.Equals(Guid.Empty))
            {
                var parent = GetItemById(query.ParentId);
                if (parent is not null)
                {
                    SetTopParentIdsOrAncestors(query, new[] { parent });
                }
            }

            if (query.User is not null)
            {
                AddUserToQuery(query, query.User, allowExternalContent);
            }

            var itemList = _itemRepository.GetItemList(query);
            var user = query.User;
            if (user is not null)
            {
                return itemList.Where(i => i.IsVisible(user)).ToList();
            }

            return itemList;
        }

        public List<BaseItem> GetItemList(InternalItemsQuery query)
        {
            return GetItemList(query, true);
        }

        public List<BaseItem> GetItemList(InternalItemsQuery query, List<BaseItem> parents)
        {
            SetTopParentIdsOrAncestors(query, parents);

            if (query.AncestorIds.Length == 0 && query.TopParentIds.Length == 0 && query.User is not null)
            {
                AddUserToQuery(query, query.User);
            }

            return _itemRepository.GetItemList(query);
        }

        /// <inheritdoc/>
        public void DeleteItem(BaseItem item, DeleteOptions deleteOptions)
        {
            if (_itemRepository.RetrieveItem(item.Id) is null)
            {
                return;
            }

            _itemRepository.DeleteItem(item.Id);
            OnItemDeleted(item, item.GetParent(), deleteOptions);
        }

        /// <inheritdoc/>
        public void DeleteItem(BaseItem item, BaseItem parent, DeleteOptions deleteOptions)
        {
            if (_itemRepository.RetrieveItem(item.Id) is null)
            {
                return;
            }

            _itemRepository.DeleteItem(item.Id);
            OnItemDeleted(item, parent, deleteOptions);
        }

        /// <inheritdoc/>
        public void DeleteItem(BaseItem item)
        {
            _itemRepository.DeleteItem(item.Id);
            OnItemDeleted(item, item.GetParent(), null);
        }

        private void OnItemDeleted(BaseItem item, BaseItem parent, DeleteOptions deleteOptions)
        {
            ItemRemoved?.Invoke(this, new ItemRemovedEventArgs(deleteOptions ?? new())
                {
                    Item = item,
                    Parent = parent
                });
        }

        public int GetCount(InternalItemsQuery query)
        {
            if (query.Recursive && !query.ParentId.Equals(Guid.Empty))
            {
                var parent = GetItemById(query.ParentId);
                if (parent is not null)
                {
                    SetTopParentIdsOrAncestors(query, new[] { parent });
                }
            }

            if (query.User is not null)
            {
                AddUserToQuery(query, query.User);
            }

            return _itemRepository.GetCount(query);
        }

        public QueryResult<BaseItem> QueryItems(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                AddUserToQuery(query, query.User);
            }

            if (query.EnableTotalRecordCount)
            {
                return _itemRepository.GetItems(query);
            }

            return new QueryResult<BaseItem>(
                query.StartIndex,
                null,
                _itemRepository.GetItemList(query));
        }

        public List<Guid> GetItemIds(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                AddUserToQuery(query, query.User);
            }

            return _itemRepository.GetItemIdsList(query);
        }

        public QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetStudios(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                AddUserToQuery(query, query.User);
            }

            SetTopParentOrAncestorIds(query);
            return _itemRepository.GetStudios(query);
        }

        public Studio GetStudio(string name)
        {
            return CreateItemByName<Studio>(Studio.GetPath, name, new DtoOptions(true));
        }

        public QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetGenres(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                AddUserToQuery(query, query.User);
            }

            SetTopParentOrAncestorIds(query);
            return _itemRepository.GetGenres(query);
        }

        public QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetMusicGenres(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                AddUserToQuery(query, query.User);
            }

            SetTopParentOrAncestorIds(query);
            return _itemRepository.GetMusicGenres(query);
        }

        public QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetAllArtists(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                AddUserToQuery(query, query.User);
            }

            SetTopParentOrAncestorIds(query);
            return _itemRepository.GetAllArtists(query);
        }

        public QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetArtists(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                AddUserToQuery(query, query.User);
            }

            SetTopParentOrAncestorIds(query);
            return _itemRepository.GetArtists(query);
        }

        public QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetAlbumArtists(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                AddUserToQuery(query, query.User);
            }

            SetTopParentOrAncestorIds(query);
            return _itemRepository.GetAlbumArtists(query);
        }

        public QueryResult<BaseItem> GetItemsResult(InternalItemsQuery query)
        {
            if (query.Recursive && !query.ParentId.Equals(Guid.Empty))
            {
                var parent = GetItemById(query.ParentId);
                if (parent is not null)
                {
                    SetTopParentIdsOrAncestors(query, new[] { parent });
                }
            }

            if (query.User is not null)
            {
                AddUserToQuery(query, query.User);
            }

            if (query.EnableTotalRecordCount)
            {
                return _itemRepository.GetItems(query);
            }

            return new QueryResult<BaseItem>(
                query.StartIndex,
                null,
                _itemRepository.GetItemList(query));
        }

        private void SetTopParentIdsOrAncestors(InternalItemsQuery query, IReadOnlyCollection<BaseItem> parents)
        {
            if (parents.All(i => i is ICollectionFolder || i is UserView))
            {
                // Optimize by querying against top level views
                query.TopParentIds = parents.SelectMany(i => GetTopParentIdsForQuery(i, query.User)).ToArray();

                // Prevent searching in all libraries due to empty filter
                if (query.TopParentIds.Length == 0)
                {
                    query.TopParentIds = new[] { Guid.NewGuid() };
                }
            }
            else
            {
                // We need to be able to query from any arbitrary ancestor up the tree
                query.AncestorIds = parents.SelectMany(i => i.GetIdsForAncestorQuery()).ToArray();

                // Prevent searching in all libraries due to empty filter
                if (query.AncestorIds.Length == 0)
                {
                    query.AncestorIds = new[] { Guid.NewGuid() };
                }
            }

            query.Parent = null;
        }

        private IEnumerable<Guid> GetTopParentIdsForQuery(BaseItem item, User user)
        {
            if (item is UserView view)
            {
                if (string.Equals(view.ViewType, CollectionType.LiveTv, StringComparison.Ordinal))
                {
                    return new[] { view.Id };
                }

                // Translate view into folders
                if (!view.DisplayParentId.Equals(Guid.Empty))
                {
                    var displayParent = GetItemById(view.DisplayParentId);
                    if (displayParent is not null)
                    {
                        return GetTopParentIdsForQuery(displayParent, user);
                    }

                    return Array.Empty<Guid>();
                }

                if (!view.ParentId.Equals(Guid.Empty))
                {
                    var displayParent = GetItemById(view.ParentId);
                    if (displayParent is not null)
                    {
                        return GetTopParentIdsForQuery(displayParent, user);
                    }

                    return Array.Empty<Guid>();
                }

                // Handle grouping
                if (user is not null && !string.IsNullOrEmpty(view.ViewType) && UserView.IsEligibleForGrouping(view.ViewType)
                    && user.GetPreference(PreferenceKind.GroupedFolders).Length > 0)
                {
                    return _libraryRootFolderManager.GetUserRootFolder()
                        .GetChildren(user, true)
                        .OfType<CollectionFolder>()
                        .Where(i => string.IsNullOrEmpty(i.CollectionType) || string.Equals(i.CollectionType, view.ViewType, StringComparison.OrdinalIgnoreCase))
                        .Where(i => user.IsFolderGrouped(i.Id))
                        .SelectMany(i => GetTopParentIdsForQuery(i, user));
                }

                return Array.Empty<Guid>();
            }

            if (item is CollectionFolder collectionFolder)
            {
                return collectionFolder.PhysicalFolderIds;
            }

            var topParent = item.GetTopParent();
            if (topParent is not null)
            {
                return new[] { topParent.Id };
            }

            return Array.Empty<Guid>();
        }


        private void AddUserToQuery(InternalItemsQuery query, User user, bool allowExternalContent = true)
        {
            if (query.AncestorIds.Length == 0 &&
                query.ParentId.Equals(Guid.Empty) &&
                query.ChannelIds.Count == 0 &&
                query.TopParentIds.Length == 0 &&
                string.IsNullOrEmpty(query.AncestorWithPresentationUniqueKey) &&
                string.IsNullOrEmpty(query.SeriesPresentationUniqueKey) &&
                query.ItemIds.Length == 0)
            {
                var userViews = UserViewManager.GetUserViews(new UserViewQuery
                {
                    UserId = user.Id,
                    IncludeHidden = true,
                    IncludeExternalContent = allowExternalContent
                });

                query.TopParentIds = userViews.SelectMany(i => GetTopParentIdsForQuery(i, user)).ToArray();

                // Prevent searching in all libraries due to empty filter
                if (query.TopParentIds.Length == 0)
                {
                    query.TopParentIds = new[] { Guid.NewGuid() };
                }
            }
        }

        private void SetTopParentOrAncestorIds(InternalItemsQuery query)
        {
            var ancestorIds = query.AncestorIds;
            int len = ancestorIds.Length;
            if (len == 0)
            {
                return;
            }

            var parents = new BaseItem[len];
            for (int i = 0; i < len; i++)
            {
                parents[i] = GetItemById(ancestorIds[i]);
                if (parents[i] is not (ICollectionFolder or UserView))
                {
                    return;
                }
            }

            // Optimize by querying against top level views
            query.TopParentIds = parents.SelectMany(i => GetTopParentIdsForQuery(i, query.User)).ToArray();
            query.AncestorIds = Array.Empty<Guid>();

            // Prevent searching in all libraries due to empty filter
            if (query.TopParentIds.Length == 0)
            {
                query.TopParentIds = new[] { Guid.NewGuid() };
            }
        }

        public void UpdatePeople(Guid itemId, List<PersonInfo> people)
        {
            _itemRepository.UpdatePeople(itemId, people);
        }

        /// <inheritdoc/>
        public void RegisterItemInCache(BaseItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (item is IItemByName)
            {
                if (item is not MusicArtist)
                {
                    return;
                }
            }
            else if (!item.IsFolder && item is not Video && item is not LiveTvChannel)
            {
                return;
            }

            _cache[item.Id] = item;
        }

        /// <summary>
        /// Gets a Genre.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{Genre}.</returns>
        public MusicArtist GetArtist(string name)
        {
            return GetArtist(name, new DtoOptions(true));
        }

        public MusicArtist GetArtist(string name, DtoOptions options)
        {
            return CreateItemByName<MusicArtist>(MusicArtist.GetPath, name, options);
        }

        /// <inheritdoc/>
        public List<ChapterInfo> GetChapters(BaseItem item)
        {
            return _itemRepository.GetChapters(item);
        }

        /// <inheritdoc/>
        public ChapterInfo GetChapter(BaseItem item, int index)
        {
            return _itemRepository.GetChapter(item, index);
        }

        /// <inheritdoc/>
        public List<PersonInfo> GetPeople(InternalPeopleQuery query)
        {
            return _itemRepository.GetPeople(query);
        }

        /// <inheritdoc/>
        public List<string> GetPeopleNames(InternalPeopleQuery query)
        {
            return _itemRepository.GetPeopleNames(query);
        }

        /// <inheritdoc/>
        public void SaveItems(IReadOnlyList<BaseItem> items, CancellationToken cancellationToken)
        {
            if (items is null || !items.Any())
            {
                return;
            }

            _itemRepository.SaveItems(items, cancellationToken);
            foreach (var item in items)
            {
                RegisterItemInCache(item);

                // With the live tv guide this just creates too much noise
                if (item.SourceType == SourceType.Library)
                {
                    continue;
                }

                ItemAdded?.Invoke(
                    this,
                    new ItemChangeEventArgs
                    {
                        Item = item,
                        Parent = item.GetParent()
                    });
            }
        }

        public Task UpdateItemAsync(BaseItem item, BaseItem parent, ItemUpdateType updateReason, CancellationToken cancellationToken)
        {
            UpdateItems(new BaseItem[] { item }, parent, updateReason, cancellationToken);
            // NOTE: Dangles: this is hacky but I'm staring to get lazy now.
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void UpdateItems(IReadOnlyList<BaseItem> items, BaseItem parent, ItemUpdateType updateReason, CancellationToken cancellationToken)
        {
            _itemRepository.SaveItems(items, cancellationToken);

            foreach (var item in items)
            {
                // With the live tv guide this just creates too much noise
                if (item.SourceType != SourceType.Library)
                {
                    continue;
                }

                ItemUpdated?.Invoke(
                    this,
                    new ItemChangeEventArgs
                    {
                        Item = item,
                        Parent = parent,
                        UpdateReason = updateReason
                    });
            }
        }

        /// <summary>
        /// Creates the item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="parent">The parent item.</param>
        public void CreateItem(BaseItem item, BaseItem parent)
        {
            CreateItems(new[] { item }, parent, CancellationToken.None);
        }

        /// <summary>
        /// Creates the items.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="parent">The parent item.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public void CreateItems(IReadOnlyList<BaseItem> items, BaseItem parent, CancellationToken cancellationToken)
        {
            _itemRepository.SaveItems(items, cancellationToken);

            foreach (var item in items)
            {
                RegisterItemInCache(item);
            }

            if (ItemAdded is not null)
            {
                foreach (var item in items)
                {
                    // With the live tv guide this just creates too much noise
                    if (item.SourceType != SourceType.Library)
                    {
                        continue;
                    }

                    try
                    {
                        ItemAdded(
                            this,
                            new ItemChangeEventArgs
                            {
                                Item = item,
                                Parent = parent ?? item.GetParent()
                            });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in ItemAdded event handler");
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void UpdateInheritedValues()
        {
            _itemRepository.UpdateInheritedValues();
        }

        /// <inheritdoc/>
        public void SaveImages(BaseItem item)
        {
            _itemRepository.SaveImages(item);
        }
    }
}
