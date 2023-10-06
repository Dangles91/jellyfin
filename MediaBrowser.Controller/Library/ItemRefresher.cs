using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Progress;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Refreshes items. Primarily used in item update tasks.
    /// </summary>
    public class ItemRefresher : IItemRefresher
    {
        private readonly IItemQueryService _itemQueryService;
        private readonly IProviderManager _providerManager;
        private readonly ILogger<ItemRefresher> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemRefresher"/> class.
        /// </summary>
        /// <param name="itemQueryService">Instance of <see cref="IItemQueryService"/> interface.</param>
        /// <param name="providerManager">Instance of <see cref="IProviderManager"/> interface.</param>
        /// <param name="logger">Instance of <see cref="ILogger{ItemRefresher}"/> interface.</param>
        public ItemRefresher(
            IItemQueryService itemQueryService,
            IProviderManager providerManager,
            ILogger<ItemRefresher> logger)
        {
            _itemQueryService = itemQueryService;
            _providerManager = providerManager;
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<ItemUpdateType> RefreshSingleItem(BaseItem item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            var service = _providerManager.GetMetadataServiceFor(item);

            if (service is null)
            {
                _logger.LogError("Unable to find a metadata service for item of type {TypeName}", item.GetType().Name);
                return Task.FromResult(ItemUpdateType.None);
            }

            return service.RefreshMetadata(item, options, cancellationToken);
        }

        /// <inheritdoc/>
        public Task RefreshFullItem(BaseItem item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            return RefreshItem(item, options, cancellationToken);
        }

        private async Task RefreshItem(BaseItem item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            await item.RefreshMetadata(options, cancellationToken).ConfigureAwait(false);

            // Collection folders don't validate their children so we'll have to simulate that here
            switch (item)
            {
                case CollectionFolder collectionFolder:
                    await RefreshCollectionFolderChildren(options, collectionFolder, cancellationToken).ConfigureAwait(false);
                    break;
                case Folder folder:
                    await folder.ValidateChildren(new SimpleProgress<double>(), options, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
            }
        }

        private async Task RefreshCollectionFolderChildren(MetadataRefreshOptions options, CollectionFolder collectionFolder, CancellationToken cancellationToken)
        {
            foreach (var child in collectionFolder.GetPhysicalFolders())
            {
                await child.RefreshMetadata(options, cancellationToken).ConfigureAwait(false);

                await child.ValidateChildren(new SimpleProgress<double>(), options, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task RefreshArtist(MusicArtist item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            var albums = _itemQueryService
                .GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.MusicAlbum },
                    ArtistIds = new[] { item.Id },
                    DtoOptions = new DtoOptions(false)
                    {
                        EnableImages = false
                    }
                })
                .OfType<MusicAlbum>();

            var musicArtists = albums
                .Select(i => i.MusicArtist)
                .Where(i => i is not null);

            var musicArtistRefreshTasks = musicArtists.Select(i => i.ValidateChildren(new SimpleProgress<double>(), options, true, cancellationToken));

            await Task.WhenAll(musicArtistRefreshTasks).ConfigureAwait(false);

            try
            {
                await item.RefreshMetadata(options, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing library");
            }
        }
    }
}
