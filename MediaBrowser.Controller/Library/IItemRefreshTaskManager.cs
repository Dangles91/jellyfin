using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Data.Events;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Queue and manage item refresh tasks.
    /// </summary>
    public interface IItemRefreshTaskManager
    {
        /// <summary>
        /// Event fired when refresh is started.
        /// </summary>
        event EventHandler<GenericEventArgs<BaseItem>> RefreshStarted;

        /// <summary>
        /// Event fired when refresh has completed.
        /// </summary>
        event EventHandler<GenericEventArgs<BaseItem>> RefreshCompleted;

        /// <summary>
        /// Progress update for refresh task.
        /// </summary>
        event EventHandler<GenericEventArgs<Tuple<BaseItem, double>>> RefreshProgress;

        /// <summary>
        /// Queues the refresh.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <param name="options">MetadataRefreshOptions for operation.</param>
        /// <param name="priority">RefreshPriority for operation.</param>
        void QueueRefresh(Guid itemId, MetadataRefreshOptions options, RefreshPriority priority);

        /// <summary>
        /// Gets the refresh queue.
        /// </summary>
        /// <returns>Refresh queue.</returns>
        HashSet<Guid> GetRefreshQueue();

        /// <summary>
        /// On refresh start.
        /// </summary>
        /// <param name="item">The item.</param>
        void OnRefreshStart(BaseItem item);

        /// <summary>
        /// On refresh progress.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="progress">The progress change.</param>
        void OnRefreshProgress(BaseItem item, double progress);

        /// <summary>
        /// On refresh complete.
        /// </summary>
        /// <param name="item">The item completed.</param>
        void OnRefreshComplete(BaseItem item);

        /// <summary>
        /// Get the progress of the refresh task for this item id.
        /// </summary>
        /// <param name="id">The item id.</param>
        /// <returns>Percent completed.</returns>
        double? GetRefreshProgress(Guid id);
    }
}
