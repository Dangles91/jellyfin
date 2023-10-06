using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Refreshes items. Primarily used in item update tasks via <see cref="IItemRefreshTaskManager"/>.
    /// </summary>
    public interface IItemRefresher
    {
        /// <summary>
        /// Refresh an artist.
        /// </summary>
        /// <param name="item">The item to refresh.</param>
        /// <param name="options">The refresh options.</param>
        /// <param name="cancellationToken">Cancellation token for async task.</param>
        /// <returns>A task.</returns>
        Task RefreshArtist(MusicArtist item, MetadataRefreshOptions options, CancellationToken cancellationToken);

        /// <summary>
        /// Fully refresh an item and children.
        /// </summary>
        /// <param name="item">The item to refresh.</param>
        /// <param name="options">The refresh options.</param>
        /// <param name="cancellationToken">Cancellation token for async task.</param>
        /// <returns>A task.</returns>
        Task RefreshFullItem(BaseItem item, MetadataRefreshOptions options, CancellationToken cancellationToken);

        /// <summary>
        /// Refresh an item.
        /// </summary>
        /// <param name="item">The item to refresh.</param>
        /// <param name="options">The refresh options.</param>
        /// <param name="cancellationToken">Cancellation token for async task.</param>
        /// <returns>A task.</returns>
        Task<ItemUpdateType> RefreshSingleItem(BaseItem item, MetadataRefreshOptions options, CancellationToken cancellationToken);
    }
}
