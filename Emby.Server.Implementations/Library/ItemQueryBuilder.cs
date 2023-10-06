using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Library;

namespace Emby.Server.Implementations.Library
{
    /// <summary>
    /// Helper for building item queries.
    /// </summary>
    public class ItemQueryBuilder : IItemQueryBuilder
    {
        private readonly IItemRepository _itemRepository;
        private readonly IVirtualFolderManager _virtualFolderManager;
        private readonly IUserViewManager _userViewManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemQueryBuilder"/> class.
        /// </summary>
        /// <param name="itemRepositry">The item service.</param>
        /// <param name="virtualFolderManager">The virtual folder manager.</param>
        /// <param name="userViewManager">The user view manager.</param>
        public ItemQueryBuilder(
            IItemRepository itemRepositry,
            IVirtualFolderManager virtualFolderManager,
            IUserViewManager userViewManager)
        {
            _itemRepository = itemRepositry;
            _virtualFolderManager = virtualFolderManager;
            _userViewManager = userViewManager;
        }

        /// <inheritdoc/>
        public void AddUserToQuery(InternalItemsQuery query, User user, bool allowExternalContent = true)
        {
            if (query.AncestorIds.Length == 0 &&
                query.ParentId.Equals(Guid.Empty) &&
                query.ChannelIds.Count == 0 &&
                query.TopParentIds.Length == 0 &&
                string.IsNullOrEmpty(query.AncestorWithPresentationUniqueKey) &&
                string.IsNullOrEmpty(query.SeriesPresentationUniqueKey) &&
                query.ItemIds.Length == 0)
            {
                var userViews = _userViewManager.GetUserViews(new UserViewQuery
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

        /// <inheritdoc/>
        public IEnumerable<Guid> GetTopParentIdsForQuery(BaseItem item, User user)
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
                    var displayParent = _itemRepository.RetrieveItem(view.DisplayParentId);
                    if (displayParent is not null)
                    {
                        return GetTopParentIdsForQuery(displayParent, user);
                    }

                    return Array.Empty<Guid>();
                }

                if (!view.ParentId.Equals(Guid.Empty))
                {
                    var displayParent = _itemRepository.RetrieveItem(view.ParentId);
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
                    return _virtualFolderManager.GetUserRootFolder()
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

        /// <inheritdoc/>
        public void SetTopParentIdsOrAncestors(InternalItemsQuery query, IReadOnlyCollection<BaseItem> parents)
        {
            if (parents.All(i => i is ICollectionFolder || i is UserView))
            {
                // Optimize by querying against top level views
                query.TopParentIds = parents.SelectMany(i => GetTopParentIdsForQuery(i, query.User!)).ToArray();

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

        /// <inheritdoc/>
        public void SetTopParentOrAncestorIds(InternalItemsQuery query)
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
                parents[i] = _itemRepository.RetrieveItem(ancestorIds[i]);
                if (parents[i] is not (ICollectionFolder or UserView))
                {
                    return;
                }
            }

            // Optimize by querying against top level views
            query.TopParentIds = parents.SelectMany(i => GetTopParentIdsForQuery(i, query.User!)).ToArray();
            query.AncestorIds = Array.Empty<Guid>();

            // Prevent searching in all libraries due to empty filter
            if (query.TopParentIds.Length == 0)
            {
                query.TopParentIds = new[] { Guid.NewGuid() };
            }
        }
    }
}
