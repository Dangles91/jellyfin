using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace Emby.Server.Implementations.Library
{
    /// <summary>
    /// Retriece latest item lists.
    /// </summary>
    public class LatestItemsService : ILatestItemsService
    {
        private readonly IUserManager _userManager;
        private readonly IItemService _itemService;
        private readonly IChannelManager _channelManager;
        private readonly ILibraryRootFolderManager _libraryRootFolderManager;
        private readonly IItemQueryService _itemQueryService;

        /// <summary>
        /// Initializes a new instance of the <see cref="LatestItemsService"/> class.
        /// </summary>
        /// <param name="userManager">The user manaer.</param>
        /// <param name="itemService">The item service.</param>
        /// <param name="channelManager">The channel manager.</param>
        /// <param name="libraryRootFolderManager">The root folder manager.</param>
        /// <param name="itemQueryService">The item query service.</param>
        public LatestItemsService(
            IUserManager userManager,
            IItemService itemService,
            IChannelManager channelManager,
            ILibraryRootFolderManager libraryRootFolderManager,
            IItemQueryService itemQueryService)
        {
            _userManager = userManager;
            _itemService = itemService;
            _channelManager = channelManager;
            _libraryRootFolderManager = libraryRootFolderManager;
            _itemQueryService = itemQueryService;
        }

        /// <inheritdoc/>
        public List<Tuple<BaseItem, List<BaseItem>>> GetLatestItems(LatestItemsQuery request, DtoOptions options)
        {
            var user = _userManager.GetUserById(request.UserId);

            var libraryItems = GetItemsForLatestItems(user!, request, options);

            var list = new List<Tuple<BaseItem, List<BaseItem>>>();

            foreach (var item in libraryItems)
            {
                // Only grab the index container for media
                var container = item.IsFolder || !request.GroupItems ? null : item.LatestItemsIndexContainer;

                if (container is null)
                {
                    list.Add(new Tuple<BaseItem, List<BaseItem>>(null!, new List<BaseItem> { item }));
                }
                else
                {
                    var current = list.FirstOrDefault(i => i.Item1 is not null && i.Item1.Id.Equals(container.Id));

                    if (current is not null)
                    {
                        current.Item2.Add(item);
                    }
                    else
                    {
                        list.Add(new Tuple<BaseItem, List<BaseItem>>(container, new List<BaseItem> { item }));
                    }
                }

                if (list.Count >= request.Limit)
                {
                    break;
                }
            }

            return list;
        }

        private IReadOnlyList<BaseItem> GetItemsForLatestItems(User user, LatestItemsQuery request, DtoOptions options)
        {
            var parentId = request.ParentId;

            var includeItemTypes = request.IncludeItemTypes;
            var limit = request.Limit ?? 10;

            var parents = new List<BaseItem>();

            if (!parentId.Equals(default))
            {
                var parentItem = _itemService.GetItemById(parentId);
                if (parentItem is Channel)
                {
                    return _channelManager.GetLatestChannelItemsInternal(
                        new InternalItemsQuery(user)
                        {
                            ChannelIds = new[] { parentId },
                            IsPlayed = request.IsPlayed,
                            StartIndex = request.StartIndex,
                            Limit = request.Limit,
                            IncludeItemTypes = request.IncludeItemTypes,
                            EnableTotalRecordCount = false
                        },
                        CancellationToken.None).GetAwaiter().GetResult().Items;
                }

                if (parentItem is Folder parent)
                {
                    parents.Add(parent);
                }
            }

            var isPlayed = request.IsPlayed;

            if (parents.OfType<ICollectionFolder>().Any(i => string.Equals(i.CollectionType, CollectionType.Music, StringComparison.OrdinalIgnoreCase)))
            {
                isPlayed = null;
            }

            if (parents.Count == 0)
            {
                parents = _libraryRootFolderManager.GetUserRootFolder().GetChildren(user, true)
                    .Where(i => i is Folder)
                    .Where(i => !user.GetPreferenceValues<Guid>(PreferenceKind.LatestItemExcludes)
                        .Contains(i.Id))
                    .ToList();
            }

            if (parents.Count == 0)
            {
                return Array.Empty<BaseItem>();
            }

            if (includeItemTypes.Length == 0)
            {
                // Handle situations with the grouping setting, e.g. movies showing up in tv, etc.
                // Thanks to mixed content libraries included in the UserView
                var hasCollectionType = parents.OfType<UserView>().ToArray();
                if (hasCollectionType.Length > 0)
                {
                    if (hasCollectionType.All(i => string.Equals(i.CollectionType, CollectionType.Movies, StringComparison.OrdinalIgnoreCase)))
                    {
                        includeItemTypes = new[] { BaseItemKind.Movie };
                    }
                    else if (hasCollectionType.All(i => string.Equals(i.CollectionType, CollectionType.TvShows, StringComparison.OrdinalIgnoreCase)))
                    {
                        includeItemTypes = new[] { BaseItemKind.Episode };
                    }
                }
            }

            var mediaTypes = new List<string>();

            if (includeItemTypes.Length == 0)
            {
                foreach (var parent in parents.OfType<ICollectionFolder>())
                {
                    switch (parent.CollectionType)
                    {
                        case CollectionType.Books:
                            mediaTypes.Add(MediaType.Book);
                            mediaTypes.Add(MediaType.Audio);
                            break;
                        case CollectionType.Music:
                            mediaTypes.Add(MediaType.Audio);
                            break;
                        case CollectionType.Photos:
                            mediaTypes.Add(MediaType.Photo);
                            mediaTypes.Add(MediaType.Video);
                            break;
                        case CollectionType.HomeVideos:
                            mediaTypes.Add(MediaType.Photo);
                            mediaTypes.Add(MediaType.Video);
                            break;
                        default:
                            mediaTypes.Add(MediaType.Video);
                            break;
                    }
                }

                mediaTypes = mediaTypes.Distinct().ToList();
            }

            var excludeItemTypes = includeItemTypes.Length == 0 && mediaTypes.Count == 0
                ? new[]
                {
                    BaseItemKind.Person,
                    BaseItemKind.Studio,
                    BaseItemKind.Year,
                    BaseItemKind.MusicGenre,
                    BaseItemKind.Genre
                }
                : Array.Empty<BaseItemKind>();

            var query = new InternalItemsQuery(user)
            {
                IncludeItemTypes = includeItemTypes,
                OrderBy = new[]
                {
                    (ItemSortBy.DateCreated, SortOrder.Descending),
                    (ItemSortBy.SortName, SortOrder.Descending),
                    (ItemSortBy.ProductionYear, SortOrder.Descending)
                },
                IsFolder = includeItemTypes.Length == 0 ? false : null,
                ExcludeItemTypes = excludeItemTypes,
                IsVirtualItem = false,
                Limit = limit * 5,
                IsPlayed = isPlayed,
                DtoOptions = options,
                MediaTypes = mediaTypes.ToArray()
            };

            if (parents.Count == 0)
            {
                return _itemQueryService.GetItemList(query, false);
            }

            return _itemQueryService.GetItemList(query, parents);
        }
    }
}
