using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Library.Validators
{
    /// <summary>
    /// Class ArtistsPostScanTask.
    /// </summary>
    public class ArtistsPostScanTask : ILibraryPostScanTask
    {
        private readonly IItemService _itemService;
        private readonly IItemQueryService _itemQueryService;
        private readonly ILogger<ArtistsValidator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArtistsPostScanTask" /> class.
        /// </summary>
        /// <param name="itemService">The item service.</param>
        /// <param name="itemQueryService">The item query service.</param>
        /// <param name="logger">The logger.</param>
        public ArtistsPostScanTask(
            IItemService itemService,
            IItemQueryService itemQueryService,
            ILogger<ArtistsValidator> logger)
        {
            _itemService = itemService;
            _itemQueryService = itemQueryService;
            _logger = logger;
        }

        /// <summary>
        /// Runs the specified progress.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            return new ArtistsValidator(_logger, _itemService, _itemQueryService).Run(progress, cancellationToken);
        }
    }
}
