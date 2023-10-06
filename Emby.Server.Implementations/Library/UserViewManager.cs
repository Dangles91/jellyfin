#nullable disable

#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using Jellyfin.Extensions;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Library;
using MediaBrowser.Model.Querying;

namespace Emby.Server.Implementations.Library
{
    // TODO: Dangles: This also needs some work
    public class UserViewManager : IUserViewManager
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILocalizationManager _localizationManager;
        private readonly IUserManager _userManager;

        private readonly IChannelManager _channelManager;
        private readonly ILiveTvManager _liveTvManager;
        private readonly IServerConfigurationManager _config;
        private readonly ILibraryRootFolderManager _libraryRootFolderManager;
        private readonly ILibraryViewService _libraryViewService;

        public UserViewManager(
            ILibraryManager libraryManager,
            ILocalizationManager localizationManager,
            IUserManager userManager,
            IChannelManager channelManager,
            ILiveTvManager liveTvManager,
            IServerConfigurationManager config,
            ILibraryRootFolderManager libraryRootFolderManager,
            ILibraryViewService libraryViewService)
        {
            _libraryManager = libraryManager;
            _localizationManager = localizationManager;
            _userManager = userManager;
            _channelManager = channelManager;
            _liveTvManager = liveTvManager;
            _config = config;
            _libraryRootFolderManager = libraryRootFolderManager;
            _libraryViewService = libraryViewService;
        }

        public Folder[] GetUserViews(UserViewQuery query)
        {
            var user = _userManager.GetUserById(query.UserId);
            if (user is null)
            {
                throw new ArgumentException("User id specified in the query does not exist.", nameof(query));
            }

            var folders = _libraryRootFolderManager.GetUserRootFolder()
                .GetChildren(user, true)
                .OfType<Folder>()
                .ToList();

            var groupedFolders = new List<ICollectionFolder>();
            var list = new List<Folder>();

            foreach (var folder in folders)
            {
                var collectionFolder = folder as ICollectionFolder;
                var folderViewType = collectionFolder?.CollectionType;

                // Playlist library requires special handling because the folder only refrences user playlists
                if (string.Equals(folderViewType, CollectionType.Playlists, StringComparison.OrdinalIgnoreCase))
                {
                    var items = folder.GetItemList(new InternalItemsQuery(user)
                    {
                        ParentId = folder.ParentId
                    });

                    if (!items.Any(item => item.IsVisible(user)))
                    {
                        continue;
                    }
                }

                if (UserView.IsUserSpecific(folder))
                {
                    list.Add(_libraryViewService.GetNamedView(user, folder.Name, folder.Id, folderViewType, null));
                    continue;
                }

                if (collectionFolder is not null && UserView.IsEligibleForGrouping(folder) && user.IsFolderGrouped(folder.Id))
                {
                    groupedFolders.Add(collectionFolder);
                    continue;
                }

                if (query.PresetViews.Contains(folderViewType ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(GetUserView(folder, folderViewType, string.Empty));
                }
                else
                {
                    list.Add(folder);
                }
            }

            foreach (var viewType in new[] { CollectionType.Movies, CollectionType.TvShows })
            {
                var parents = groupedFolders.Where(i => string.Equals(i.CollectionType, viewType, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(i.CollectionType))
                    .ToList();

                if (parents.Count > 0)
                {
                    var localizationKey = string.Equals(viewType, CollectionType.TvShows, StringComparison.OrdinalIgnoreCase) ?
                        "TvShows" :
                        "Movies";

                    list.Add(GetUserView(parents, viewType, localizationKey, string.Empty, user, query.PresetViews));
                }
            }

            if (_config.Configuration.EnableFolderView)
            {
                var name = _localizationManager.GetLocalizedString("Folders");
                list.Add(_libraryViewService.GetNamedView(name, CollectionType.Folders, string.Empty));
            }

            if (query.IncludeExternalContent)
            {
                var channelResult = _channelManager.GetChannelsInternalAsync(new ChannelQuery
                {
                    UserId = query.UserId
                }).GetAwaiter().GetResult();

                var channels = channelResult.Items;

                list.AddRange(channels);

                if (_liveTvManager.GetEnabledUsers().Select(i => i.Id).Contains(query.UserId))
                {
                    list.Add(_liveTvManager.GetInternalLiveTvFolder(CancellationToken.None));
                }
            }

            if (!query.IncludeHidden)
            {
                list = list.Where(i => !user.GetPreferenceValues<Guid>(PreferenceKind.MyMediaExcludes).Contains(i.Id)).ToList();
            }

            var sorted = _libraryManager.Sort(list, user, new[] { ItemSortBy.SortName }, SortOrder.Ascending).ToList();
            var orders = user.GetPreferenceValues<Guid>(PreferenceKind.OrderedViews);

            return list
                .OrderBy(i =>
                {
                    var index = Array.IndexOf(orders, i.Id);
                    if (index == -1
                        && i is UserView view
                        && !view.DisplayParentId.Equals(default))
                    {
                        index = Array.IndexOf(orders, view.DisplayParentId);
                    }

                    return index == -1 ? int.MaxValue : index;
                })
                .ThenBy(sorted.IndexOf)
                .ThenBy(i => i.SortName)
                .ToArray();
        }

        public UserView GetUserSubViewWithName(string name, Guid parentId, string type, string sortName)
        {
            var uniqueId = parentId + "subview" + type;

            return _libraryViewService.GetNamedView(name, parentId, type, sortName, uniqueId);
        }

        public UserView GetUserSubView(Guid parentId, string type, string localizationKey, string sortName)
        {
            var name = _localizationManager.GetLocalizedString(localizationKey);

            return GetUserSubViewWithName(name, parentId, type, sortName);
        }

        private Folder GetUserView(
            List<ICollectionFolder> parents,
            string viewType,
            string localizationKey,
            string sortName,
            User user,
            string[] presetViews)
        {
            if (parents.Count == 1 && parents.All(i => string.Equals(i.CollectionType, viewType, StringComparison.OrdinalIgnoreCase)))
            {
                if (!presetViews.Contains(viewType, StringComparison.OrdinalIgnoreCase))
                {
                    return (Folder)parents[0];
                }

                return GetUserView((Folder)parents[0], viewType, string.Empty);
            }

            var name = _localizationManager.GetLocalizedString(localizationKey);
            return _libraryViewService.GetNamedView(user, name, viewType, sortName);
        }

        public UserView GetUserView(Folder parent, string viewType, string sortName)
        {
            return _libraryViewService.GetShadowView(parent, viewType, sortName);
        }
    }
}
