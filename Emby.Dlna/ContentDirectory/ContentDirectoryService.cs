#pragma warning disable CS1591

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Emby.Dlna.Service;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dlna;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Globalization;
using Microsoft.Extensions.Logging;

namespace Emby.Dlna.ContentDirectory
{
    /// <summary>
    /// Defines the <see cref="ContentDirectoryService" />.
    /// </summary>
    public class ContentDirectoryService : BaseService, IContentDirectory
    {
        private readonly IImageProcessor _imageProcessor;
        private readonly IUserDataManager _userDataManager;
        private readonly IDlnaManager _dlna;
        private readonly IServerConfigurationManager _config;
        private readonly IUserManager _userManager;
        private readonly ILocalizationManager _localization;
        private readonly IMediaSourceManager _mediaSourceManager;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly ITVSeriesManager _tvSeriesManager;
        private readonly ILibraryRootFolderManager _libraryRootFolderManager;
        private readonly IItemService _itemService;
        private readonly ILatestItemsService _latestItemsService;
        private readonly IItemQueryService _itemQueryService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentDirectoryService"/> class.
        /// </summary>
        /// <param name="dlna">The <see cref="IDlnaManager"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        /// <param name="userDataManager">The <see cref="IUserDataManager"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        /// <param name="imageProcessor">The <see cref="IImageProcessor"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        /// <param name="config">The <see cref="IServerConfigurationManager"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        /// <param name="userManager">The <see cref="IUserManager"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        /// <param name="logger">The <see cref="ILogger{ContentDirectoryService}"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        /// <param name="httpClient">The <see cref="IHttpClientFactory"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        /// <param name="localization">The <see cref="ILocalizationManager"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        /// <param name="mediaSourceManager">The <see cref="IMediaSourceManager"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        /// <param name="mediaEncoder">The <see cref="IMediaEncoder"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        /// <param name="tvSeriesManager">The <see cref="ITVSeriesManager"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        /// <param name="libraryRootFolderManager">The <see cref="ILibraryRootFolderManager"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        /// <param name="itemService">The <see cref="IItemService"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        /// <param name="latestItemsService">latest item service.</param>
        /// <param name="itemQueryService">The <see cref="IItemQueryService"/> to use in the <see cref="ContentDirectoryService"/> instance.</param>
        public ContentDirectoryService(
            IDlnaManager dlna,
            IUserDataManager userDataManager,
            IImageProcessor imageProcessor,
            IServerConfigurationManager config,
            IUserManager userManager,
            ILogger<ContentDirectoryService> logger,
            IHttpClientFactory httpClient,
            ILocalizationManager localization,
            IMediaSourceManager mediaSourceManager,
            IMediaEncoder mediaEncoder,
            ITVSeriesManager tvSeriesManager,
            ILibraryRootFolderManager libraryRootFolderManager,
            IItemService itemService,
            ILatestItemsService latestItemsService,
            IItemQueryService itemQueryService)
            : base(logger, httpClient)
        {
            _dlna = dlna;
            _userDataManager = userDataManager;
            _imageProcessor = imageProcessor;
            _config = config;
            _userManager = userManager;
            _localization = localization;
            _mediaSourceManager = mediaSourceManager;
            _mediaEncoder = mediaEncoder;
            _tvSeriesManager = tvSeriesManager;
            _libraryRootFolderManager = libraryRootFolderManager;
            _itemService = itemService;
            _latestItemsService = latestItemsService;
            _itemQueryService = itemQueryService;
        }

        /// <summary>
        /// Gets the system id. (A unique id which changes on when our definition changes.)
        /// </summary>
        private static int SystemUpdateId
        {
            get
            {
                var now = DateTime.UtcNow;

                return now.Year + now.DayOfYear + now.Hour;
            }
        }

        /// <inheritdoc />
        public string GetServiceXml()
        {
            return ContentDirectoryXmlBuilder.GetXml();
        }

        /// <inheritdoc />
        public Task<ControlResponse> ProcessControlRequestAsync(ControlRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var profile = _dlna.GetProfile(request.Headers) ?? _dlna.GetDefaultProfile();

            var serverAddress = request.RequestedUrl.Substring(0, request.RequestedUrl.IndexOf("/dlna", StringComparison.OrdinalIgnoreCase));

            var user = GetUser(profile);

            return new ControlHandler(
                Logger,
                profile,
                serverAddress,
                null,
                _imageProcessor,
                _userDataManager,
                user,
                SystemUpdateId,
                _config,
                _localization,
                _mediaSourceManager,
                _mediaEncoder,
                _tvSeriesManager,
                _itemService,
                _latestItemsService,
                _libraryRootFolderManager,
                _itemQueryService)
                .ProcessControlRequestAsync(request);
        }

        /// <summary>
        /// Get the user stored in the device profile.
        /// </summary>
        /// <param name="profile">The <see cref="DeviceProfile"/>.</param>
        /// <returns>The <see cref="User"/>.</returns>
        private User? GetUser(DeviceProfile profile)
        {
            if (!string.IsNullOrEmpty(profile.UserId))
            {
                var user = _userManager.GetUserById(Guid.Parse(profile.UserId));

                if (user is not null)
                {
                    return user;
                }
            }

            var userId = _config.GetDlnaConfiguration().DefaultUserId;

            if (!string.IsNullOrEmpty(userId))
            {
                var user = _userManager.GetUserById(Guid.Parse(userId));

                if (user is not null)
                {
                    return user;
                }
            }

            foreach (var user in _userManager.Users)
            {
                if (user.HasPermission(PermissionKind.IsAdministrator))
                {
                    return user;
                }
            }

            return _userManager.Users.FirstOrDefault();
        }
    }
}
