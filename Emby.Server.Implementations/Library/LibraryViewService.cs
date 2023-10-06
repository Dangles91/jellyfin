using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Manager;

namespace Emby.Server.Implementations.Library
{
    /// <summary>
    /// Retreive named views.
    /// </summary>
    public class LibraryViewService : ILibraryViewService
    {
        private readonly IItemService _itemService;
        private readonly IServerConfigurationManager _serverConfigurationManager;
        private readonly ILibraryItemIdGenerator _libraryItemIdGenerator;
        private readonly IFileSystem _fileSystem;
        private readonly IItemRefreshTaskManager _refreshTaskManager;
        private readonly TimeSpan _viewRefreshInterval = TimeSpan.FromHours(24);

        /// <summary>
        /// Initializes a new instance of the <see cref="LibraryViewService"/> class.
        /// </summary>
        /// <param name="itemService">The item service.</param>
        /// <param name="serverConfigurationManager">The server configuration manager.</param>
        /// <param name="libraryItemIdGenerator">The library item id generator.</param>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="refreshTaskManager">The item refresh task manager.</param>
        public LibraryViewService(
            IItemService itemService,
            IServerConfigurationManager serverConfigurationManager,
            ILibraryItemIdGenerator libraryItemIdGenerator,
            IFileSystem fileSystem,
            IItemRefreshTaskManager refreshTaskManager)
        {
            _itemService = itemService;
            _serverConfigurationManager = serverConfigurationManager;
            _libraryItemIdGenerator = libraryItemIdGenerator;
            _fileSystem = fileSystem;
            _refreshTaskManager = refreshTaskManager;
        }

        /// <inheritdoc/>
        public UserView GetNamedView(
            User user,
            string name,
            Guid parentId,
            string viewType,
            string sortName)
        {
            var parentIdString = parentId.Equals(Guid.Empty)
                ? null
                : parentId.ToString("N", CultureInfo.InvariantCulture);
            var idValues = "38_namedview_" + name + user.Id.ToString("N", CultureInfo.InvariantCulture) + (parentIdString ?? string.Empty) + (viewType ?? string.Empty);

            var id = _libraryItemIdGenerator.Generate(idValues, typeof(UserView));

            var path = Path.Combine(_serverConfigurationManager.ApplicationPaths.InternalMetadataPath, "views", id.ToString("N", CultureInfo.InvariantCulture));

            var item = _itemService.GetItemById(id) as UserView;

            var isNew = false;

            if (item is null)
            {
                Directory.CreateDirectory(path);

                item = new UserView
                {
                    Path = path,
                    Id = id,
                    DateCreated = DateTime.UtcNow,
                    Name = name,
                    ViewType = viewType,
                    ForcedSortName = sortName,
                    UserId = user.Id,
                    DisplayParentId = parentId
                };

                _itemService.CreateItem(item, null!);

                isNew = true;
            }

            var refresh = isNew || DateTime.UtcNow - item.DateLastRefreshed >= _viewRefreshInterval;

            if (!refresh && !item.DisplayParentId.Equals(default))
            {
                var displayParent = _itemService.GetItemById(item.DisplayParentId);
                refresh = displayParent is not null && displayParent.DateLastSaved > item.DateLastRefreshed;
            }

            if (refresh)
            {
                // TODO: don't pass around the file system
                _refreshTaskManager.QueueRefresh(
                    item.Id,
                    new MetadataRefreshOptions(new DirectoryService(_fileSystem))
                    {
                        // Need to force save to increment DateLastSaved
                        ForceSave = true
                    },
                    RefreshPriority.Normal);
            }

            return item;
        }

        /// <inheritdoc/>
        public UserView GetNamedView(
            string name,
            Guid parentId,
            string viewType,
            string sortName,
            string uniqueId)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            var parentIdString = parentId.Equals(Guid.Empty)
                ? null
                : parentId.ToString("N", CultureInfo.InvariantCulture);
            var idValues = "37_namedview_" + name + (parentIdString ?? string.Empty) + (viewType ?? string.Empty);
            if (!string.IsNullOrEmpty(uniqueId))
            {
                idValues += uniqueId;
            }

            var id = _libraryItemIdGenerator.Generate(idValues, typeof(UserView));

            var path = Path.Combine(_serverConfigurationManager.ApplicationPaths.InternalMetadataPath, "views", id.ToString("N", CultureInfo.InvariantCulture));

            var item = _itemService.GetItemById(id) as UserView;

            var isNew = false;

            if (item is null)
            {
                Directory.CreateDirectory(path);

                item = new UserView
                {
                    Path = path,
                    Id = id,
                    DateCreated = DateTime.UtcNow,
                    Name = name,
                    ViewType = viewType,
                    ForcedSortName = sortName
                };

                item.DisplayParentId = parentId;

                _itemService.CreateItem(item, null!);

                isNew = true;
            }

            if (!string.Equals(viewType, item.ViewType, StringComparison.OrdinalIgnoreCase))
            {
                item.ViewType = viewType;
                item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).GetAwaiter().GetResult();
            }

            var refresh = isNew || DateTime.UtcNow - item.DateLastRefreshed >= _viewRefreshInterval;

            if (!refresh && !item.DisplayParentId.Equals(Guid.Empty))
            {
                var displayParent = _itemService.GetItemById(item.DisplayParentId);
                refresh = displayParent is not null && displayParent.DateLastSaved > item.DateLastRefreshed;
            }

            if (refresh)
            {
                _refreshTaskManager.QueueRefresh(
                    item.Id,
                    new MetadataRefreshOptions(new DirectoryService(_fileSystem))
                    {
                        // Need to force save to increment DateLastSaved
                        ForceSave = true
                    },
                    RefreshPriority.Normal);
            }

            return item;
        }

        /// <inheritdoc/>
        public UserView GetNamedView(
            User user,
            string name,
            string viewType,
            string sortName)
        {
            return GetNamedView(user, name, Guid.Empty, viewType, sortName);
        }

        /// <inheritdoc/>
        public UserView GetNamedView(
            string name,
            string viewType,
            string sortName)
        {
            var path = Path.Combine(
                _serverConfigurationManager.ApplicationPaths.InternalMetadataPath,
                "views",
                _fileSystem.GetValidFilename(viewType));

            var id = _libraryItemIdGenerator.Generate(path + "_namedview_" + name, typeof(UserView));

            var item = _itemService.GetItemById(id) as UserView;

            var refresh = false;

            if (item is null || !string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(path);

                item = new UserView
                {
                    Path = path,
                    Id = id,
                    DateCreated = DateTime.UtcNow,
                    Name = name,
                    ViewType = viewType,
                    ForcedSortName = sortName
                };

                _itemService.CreateItem(item, null!);

                refresh = true;
            }

            if (refresh)
            {
                item.UpdateToRepositoryAsync(ItemUpdateType.MetadataImport, CancellationToken.None).GetAwaiter().GetResult();
                _refreshTaskManager.QueueRefresh(item.Id, new MetadataRefreshOptions(new DirectoryService(_fileSystem)), RefreshPriority.Normal);
            }

            return item;
        }

        /// <inheritdoc/>
        public UserView GetShadowView(
            BaseItem parent,
            string viewType,
            string sortName)
        {
            ArgumentNullException.ThrowIfNull(parent);

            var name = parent.Name;
            var parentId = parent.Id;

            var idValues = "38_namedview_" + name + parentId + (viewType ?? string.Empty);

            var id = _libraryItemIdGenerator.Generate(idValues, typeof(UserView));

            var path = parent.Path;

            var item = _itemService.GetItemById(id) as UserView;

            var isNew = false;

            if (item is null)
            {
                Directory.CreateDirectory(path);

                item = new UserView
                {
                    Path = path,
                    Id = id,
                    DateCreated = DateTime.UtcNow,
                    Name = name,
                    ViewType = viewType,
                    ForcedSortName = sortName
                };

                item.DisplayParentId = parentId;

                _itemService.CreateItem(item, null!);

                isNew = true;
            }

            var refresh = isNew || DateTime.UtcNow - item.DateLastRefreshed >= _viewRefreshInterval;

            if (!refresh && !item.DisplayParentId.Equals(Guid.Empty))
            {
                var displayParent = _itemService.GetItemById(item.DisplayParentId);
                refresh = displayParent is not null && displayParent.DateLastSaved > item.DateLastRefreshed;
            }

            if (refresh)
            {
                _refreshTaskManager.QueueRefresh(
                    item.Id,
                    new MetadataRefreshOptions(new DirectoryService(_fileSystem))
                    {
                        // Need to force save to increment DateLastSaved
                        ForceSave = true
                    },
                    RefreshPriority.Normal);
            }

            return item;
        }
    }
}
