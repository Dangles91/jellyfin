#pragma warning disable CS1591

using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Data
{
    public class CleanDatabaseScheduledTask : ILibraryPostScanTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<CleanDatabaseScheduledTask> _logger;
        private readonly IItemService _itemService;
        private readonly IItemQueryService _itemQueryService;

        public CleanDatabaseScheduledTask(
            ILibraryManager libraryManager,
            ILogger<CleanDatabaseScheduledTask> logger,
            IItemService itemService,
            IItemQueryService itemQueryService)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _itemService = itemService;
            _itemQueryService = itemQueryService;
        }

        public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            CleanDeadItems(cancellationToken, progress);
            return Task.CompletedTask;
        }

        private void CleanDeadItems(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var itemIds = _itemQueryService.GetItemIds(new InternalItemsQuery
            {
                HasDeadParentId = true
            });

            var numComplete = 0;
            var numItems = itemIds.Count;

            _logger.LogDebug("Cleaning {0} items with dead parent links", numItems);

            foreach (var itemId in itemIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = _itemService.GetItemById(itemId);

                if (item is not null)
                {
                    _logger.LogInformation("Cleaning item {0} type: {1} path: {2}", item.Name, item.GetType().Name, item.Path ?? string.Empty);

                    _itemService.DeleteItem(item, new DeleteOptions
                    {
                        DeleteFileLocation = false
                    });
                }

                numComplete++;
                double percent = numComplete;
                percent /= numItems;
                progress.Report(percent * 100);
            }

            progress.Report(100);
        }
    }
}
