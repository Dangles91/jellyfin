using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Library.Validators
{
    /// <summary>
    /// Class StudiosValidator.
    /// </summary>
    public class StudiosValidator
    {
        private readonly IItemService _itemService;

        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILogger<StudiosValidator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StudiosValidator" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="itemService">The item repository.</param>
        public StudiosValidator(
            ILogger<StudiosValidator> logger,
            IItemService itemService)
        {
            _logger = logger;
            _itemService = itemService;
        }

        /// <summary>
        /// Runs the specified progress.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var names = _itemService.GetStudioNames();

            var numComplete = 0;
            var count = names.Count;

            foreach (var name in names)
            {
                try
                {
                    var item = _itemService.GetStudio(name);

                    await item.RefreshMetadata(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Don't clutter the log
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing {StudioName}", name);
                }

                numComplete++;
                double percent = numComplete;
                percent /= count;
                percent *= 100;

                progress.Report(percent);
            }

            var deadEntities = _itemService.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Studio },
                IsDeadStudio = true,
                IsLocked = false
            });

            foreach (var item in deadEntities)
            {
                _logger.LogInformation("Deleting dead {ItemType} {ItemId} {ItemName}", item.GetType().Name, item.Id.ToString("N", CultureInfo.InvariantCulture), item.Name);

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
