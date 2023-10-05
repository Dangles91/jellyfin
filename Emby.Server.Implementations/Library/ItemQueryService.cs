using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Library;
using MediaBrowser.Model.Querying;

namespace Emby.Server.Implementations.Library
{
    /// <summary>
    /// Perform complex queries for items.
    /// </summary>
    public class ItemQueryService : IItemQueryService
    {
        private readonly IItemRepository _itemRepository;
        private readonly IItemQueryBuilder _itemQueryBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemQueryService"/> class.
        /// </summary>
        /// <param name="itemRepository">The item repository.</param>
        /// <param name="itemQueryBuilder">The item query builder.</param>
        public ItemQueryService(IItemRepository itemRepository, IItemQueryBuilder itemQueryBuilder)
        {
            _itemRepository = itemRepository;
            _itemQueryBuilder = itemQueryBuilder;
        }

        /// <inheritdoc/>
        public List<Guid> GetItemIds(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                _itemQueryBuilder.AddUserToQuery(query, query.User);
            }

            return _itemRepository.GetItemIdsList(query);
        }

        /// <inheritdoc/>
        public QueryResult<BaseItem> QueryItems(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                _itemQueryBuilder.AddUserToQuery(query, query.User);
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

        /// <inheritdoc/>
        public QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetStudios(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                _itemQueryBuilder.AddUserToQuery(query, query.User);
            }

            _itemQueryBuilder.SetTopParentOrAncestorIds(query);
            return _itemRepository.GetStudios(query);
        }

        /// <inheritdoc/>
        public QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetGenres(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                _itemQueryBuilder.AddUserToQuery(query, query.User);
            }

            _itemQueryBuilder.SetTopParentOrAncestorIds(query);
            return _itemRepository.GetGenres(query);
        }

        /// <inheritdoc/>
        public QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetMusicGenres(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                _itemQueryBuilder.AddUserToQuery(query, query.User);
            }

            _itemQueryBuilder.SetTopParentOrAncestorIds(query);
            return _itemRepository.GetMusicGenres(query);
        }

        /// <inheritdoc/>
        public QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetAllArtists(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                _itemQueryBuilder.AddUserToQuery(query, query.User);
            }

            _itemQueryBuilder.SetTopParentOrAncestorIds(query);
            return _itemRepository.GetAllArtists(query);
        }

        /// <inheritdoc/>
        public QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetArtists(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                _itemQueryBuilder.AddUserToQuery(query, query.User);
            }

            _itemQueryBuilder.SetTopParentOrAncestorIds(query);
            return _itemRepository.GetArtists(query);
        }

        /// <inheritdoc/>
        public QueryResult<(BaseItem Item, ItemCounts ItemCounts)> GetAlbumArtists(InternalItemsQuery query)
        {
            if (query.User is not null)
            {
                _itemQueryBuilder.AddUserToQuery(query, query.User);
            }

            _itemQueryBuilder.SetTopParentOrAncestorIds(query);
            return _itemRepository.GetAlbumArtists(query);
        }

        /// <inheritdoc/>
        public QueryResult<BaseItem> GetItemsResult(InternalItemsQuery query)
        {
            if (query.Recursive && !query.ParentId.Equals(Guid.Empty))
            {
                var parent = _itemRepository.RetrieveItem(query.ParentId);
                if (parent is not null)
                {
                    _itemQueryBuilder.SetTopParentIdsOrAncestors(query, new[] { parent });
                }
            }

            if (query.User is not null)
            {
                _itemQueryBuilder.AddUserToQuery(query, query.User);
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

        /// <inheritdoc/>
        public List<BaseItem> GetItemList(InternalItemsQuery query, bool allowExternalContent)
        {
            if (query.Recursive && !query.ParentId.Equals(Guid.Empty))
            {
                var parent = _itemRepository.RetrieveItem(query.ParentId);
                if (parent is not null)
                {
                    _itemQueryBuilder.SetTopParentIdsOrAncestors(query, new[] { parent });
                }
            }

            if (query.User is not null)
            {
                _itemQueryBuilder.AddUserToQuery(query, query.User, allowExternalContent);
            }

            var itemList = _itemRepository.GetItemList(query);
            var user = query.User;
            if (user is not null)
            {
                return itemList.Where(i => i.IsVisible(user)).ToList();
            }

            return itemList;
        }

        /// <inheritdoc/>
        public List<BaseItem> GetItemList(InternalItemsQuery query)
        {
            return GetItemList(query, true);
        }

        /// <inheritdoc/>
        public List<BaseItem> GetItemList(InternalItemsQuery query, List<BaseItem> parents)
        {
            _itemQueryBuilder.SetTopParentIdsOrAncestors(query, parents);

            if (query.AncestorIds.Length == 0 && query.TopParentIds.Length == 0 && query.User is not null)
            {
                _itemQueryBuilder.AddUserToQuery(query, query.User);
            }

            return _itemRepository.GetItemList(query);
        }

        /// <inheritdoc/>
        public int GetCount(InternalItemsQuery query)
        {
            if (query.Recursive && !query.ParentId.Equals(Guid.Empty))
            {
                var parent = _itemRepository.RetrieveItem(query.ParentId);
                if (parent is not null)
                {
                    _itemQueryBuilder.SetTopParentIdsOrAncestors(query, new[] { parent });
                }
            }

            if (query.User is not null)
            {
                _itemQueryBuilder.AddUserToQuery(query, query.User);
            }

            return _itemRepository.GetCount(query);
        }

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
    }
}
