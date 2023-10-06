using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CacheManager.Core.Logging;
using Jellyfin.Data.Events;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Library
{
    internal class ItemRefreshTaskManager : IItemRefreshTaskManager, IDisposable
    {
        private readonly object _refreshQueueLock = new();
        private readonly ConcurrentDictionary<Guid, double> _activeRefreshes = new();
        private readonly CancellationTokenSource _disposeCancellationTokenSource = new();
        private readonly PriorityQueue<(Guid ItemId, MetadataRefreshOptions RefreshOptions), RefreshPriority> _refreshQueue = new();
        private readonly ILogger<ItemRefreshTaskManager> _logger;
        private readonly IItemRefresher _itemRefresher;
        private readonly IItemService _itemService;
        private bool _disposed;
        private bool _isProcessingRefreshQueue;

        public ItemRefreshTaskManager(
            ILogger<ItemRefreshTaskManager> logger,
            IItemRefresher itemRefresher,
            IItemService itemService)
        {
            _logger = logger;
            _itemRefresher = itemRefresher;
            _itemService = itemService;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<BaseItem>>? RefreshStarted;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<BaseItem>>? RefreshCompleted;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Tuple<BaseItem, double>>>? RefreshProgress;

        /// <inheritdoc/>
        public HashSet<Guid> GetRefreshQueue()
        {
            lock (_refreshQueueLock)
            {
                return _refreshQueue.UnorderedItems.Select(x => x.Element.ItemId).ToHashSet();
            }
        }

        /// <inheritdoc/>
        public void OnRefreshStart(BaseItem item)
        {
            _logger.LogDebug("OnRefreshStart {Item:N}", item.Id);
            _activeRefreshes[item.Id] = 0;
            try
            {
                RefreshStarted?.Invoke(this, new GenericEventArgs<BaseItem>(item));
            }
            catch (Exception ex)
            {
                // EventHandlers should never propagate exceptions, but we have little control over plugins...
                _logger.LogError(ex, "Invoking {RefreshEvent} event handlers failed", nameof(RefreshStarted));
            }
        }

        /// <inheritdoc/>
        public void OnRefreshComplete(BaseItem item)
        {
            _logger.LogDebug("OnRefreshComplete {Item:N}", item.Id);
            _activeRefreshes.TryRemove(item.Id, out _);

            try
            {
                RefreshCompleted?.Invoke(this, new GenericEventArgs<BaseItem>(item));
            }
            catch (Exception ex)
            {
                // EventHandlers should never propagate exceptions, but we have little control over plugins...
                _logger.LogError(ex, "Invoking {RefreshEvent} event handlers failed", nameof(RefreshCompleted));
            }
        }

        /// <inheritdoc/>
        public double? GetRefreshProgress(Guid id)
        {
            if (_activeRefreshes.TryGetValue(id, out double value))
            {
                return value;
            }

            return null;
        }

        /// <inheritdoc/>
        public void OnRefreshProgress(BaseItem item, double progress)
        {
            var id = item.Id;
            _logger.LogDebug("OnRefreshProgress {Id:N} {Progress}", id, progress);

            // TODO: Need to hunt down the conditions for this happening
            _activeRefreshes.AddOrUpdate(
                id,
                _ => throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Cannot update refresh progress of item '{0}' ({1}) because a refresh for this item is not running",
                        item.GetType().Name,
                        item.Id.ToString("N", CultureInfo.InvariantCulture))),
                (_, _) => progress);

            try
            {
                RefreshProgress?.Invoke(this, new GenericEventArgs<Tuple<BaseItem, double>>(new Tuple<BaseItem, double>(item, progress)));
            }
            catch (Exception ex)
            {
                // EventHandlers should never propagate exceptions, but we have little control over plugins...
                _logger.LogError(ex, "Invoking {RefreshEvent} event handlers failed", nameof(RefreshProgress));
            }
        }

        /// <inheritdoc/>
        public void QueueRefresh(Guid itemId, MetadataRefreshOptions options, RefreshPriority priority)
        {
            if (_disposed)
            {
                return;
            }

            _refreshQueue.Enqueue((itemId, options), priority);

            lock (_refreshQueueLock)
            {
                if (!_isProcessingRefreshQueue)
                {
                    _isProcessingRefreshQueue = true;
                    Task.Run(StartProcessingRefreshQueue);
                }
            }
        }

        private async Task StartProcessingRefreshQueue()
        {
            if (_disposed)
            {
                return;
            }

            var cancellationToken = _disposeCancellationTokenSource.Token;

            while (_refreshQueue.TryDequeue(out var refreshItem, out _))
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    var item = _itemService.GetItemById(refreshItem.ItemId);
                    if (item is null)
                    {
                        continue;
                    }

                    var task = item is MusicArtist artist
                        ? _itemRefresher.RefreshArtist(artist, refreshItem.RefreshOptions, cancellationToken)
                        : _itemRefresher.RefreshFullItem(item, refreshItem.RefreshOptions, cancellationToken);

                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing item");
                }
            }

            lock (_refreshQueueLock)
            {
                _isProcessingRefreshQueue = false;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and optionally managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (!_disposeCancellationTokenSource.IsCancellationRequested)
            {
                _disposeCancellationTokenSource.Cancel();
            }

            if (disposing)
            {
                _disposeCancellationTokenSource.Dispose();
            }

            _disposed = true;
        }
    }
}
