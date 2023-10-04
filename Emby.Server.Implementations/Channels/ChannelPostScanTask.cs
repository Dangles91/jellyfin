using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Channels
{
    /// <summary>
    /// A task to remove all non-installed channels from the database.
    /// </summary>
    public class ChannelPostScanTask
    {
        private readonly IChannelManager _channelManager;
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IItemService _itemService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelPostScanTask"/> class.
        /// </summary>
        /// <param name="channelManager">The channel manager.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="libraryManager">The library manager.</param>
        /// <param name="itemService">The item service.</param>
        public ChannelPostScanTask(
            IChannelManager channelManager,
            ILogger logger,
            ILibraryManager libraryManager,
            IItemService itemService)
        {
            _channelManager = channelManager;
            _logger = logger;
            _libraryManager = libraryManager;
            _itemService = itemService;
        }

        /// <summary>
        /// Runs this task.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completed task.</returns>
        public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            CleanDatabase(cancellationToken);

            progress.Report(100);
            return Task.CompletedTask;
        }

        private void CleanDatabase(CancellationToken cancellationToken)
        {
            var installedChannelIds = ((ChannelManager)_channelManager).GetInstalledChannelIds();

            var uninstalledChannels = _itemService.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Channel },
                ExcludeItemIds = installedChannelIds.ToArray()
            });

            foreach (var channel in uninstalledChannels)
            {
                cancellationToken.ThrowIfCancellationRequested();

                CleanChannel((Channel)channel, cancellationToken);
            }
        }

        private void CleanChannel(Channel channel, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Cleaning channel {0} from database", channel.Id);

            // Delete all channel items
            var items = _itemService.GetItemList(new InternalItemsQuery
            {
                ChannelIds = new[] { channel.Id }
            });

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _itemService.DeleteItem(
                    item,
                    new DeleteOptions
                    {
                        DeleteFileLocation = false
                    });
            }

            // Finally, delete the channel itself
            _itemService.DeleteItem(
                channel,
                new DeleteOptions
                {
                    DeleteFileLocation = false
                });
        }
    }
}
