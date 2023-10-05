using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Library.Validators
{
    /// <summary>
    /// Class ArtistsValidator.
    /// </summary>
    public class ArtistsValidator
    {
        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILogger<ArtistsValidator> _logger;
        private readonly IItemService _itemService;
        private readonly IItemQueryService _itemQueryService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArtistsValidator" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="itemService">The item service.</param>
        /// <param name="itemQueryService">The item query service.</param>
        public ArtistsValidator(
            ILogger<ArtistsValidator> logger,
            IItemService itemService,
            IItemQueryService itemQueryService)
        {
            _logger = logger;
            _itemService = itemService;
            _itemQueryService = itemQueryService;
        }

        /// <summary>
        /// Runs the specified progress.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var names = _itemService.GetAllArtistNames();

            var numComplete = 0;
            var count = names.Count;

            foreach (var name in names)
            {
                try
                {
                    var item = _itemService.GetArtist(name);

                    await item.RefreshMetadata(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Don't clutter the log
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing {ArtistName}", name);
                }

                numComplete++;
                double percent = numComplete;
                percent /= count;
                percent *= 100;

                progress.Report(percent);
            }

            var deadEntities = _itemQueryService.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.MusicArtist },
                IsDeadArtist = true,
                IsLocked = false
            }).Cast<MusicArtist>().ToList();

            foreach (var item in deadEntities)
            {
                if (!item.IsAccessedByName)
                {
                    continue;
                }

                _logger.LogInformation("Deleting dead {2} {0} {1}.", item.Id.ToString("N", CultureInfo.InvariantCulture), item.Name, item.GetType().Name);

                _itemService.DeleteItem(
                    item,
                    new DeleteOptions
                    {
                        DeleteFileLocation = false
                    });
            }

            progress.Report(100);
        }
    }
}
