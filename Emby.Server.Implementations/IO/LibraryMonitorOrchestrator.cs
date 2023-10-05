using System;
using System.Timers;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.IO
{
    /// <summary>
    /// Orchestrator for managing real time library monitoring.
    /// </summary>
    public class LibraryMonitorOrchestrator : ILibraryMonitorOrchestrator, IDisposable
    {
        // Safety timer to restart the monitor if it is stopped for more than 5 minutes.
        private readonly Timer _restartTimeoutTimer = new(TimeSpan.FromSeconds(300));
        private readonly ILogger<LibraryMonitorOrchestrator> _logger;
        private ILibraryMonitor? _libraryMonitor;

        private bool _disposed;
        private int _requestCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="LibraryMonitorOrchestrator"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public LibraryMonitorOrchestrator(
            ILogger<LibraryMonitorOrchestrator> logger
            )
        {
            _restartTimeoutTimer.Enabled = true;
            _restartTimeoutTimer.Elapsed += RestartTimeoutTimerElapsed;
            _logger = logger;
        }

        private void RestartTimeoutTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            _logger.LogInformation("Realtime library monitoring restart timer elapsed. Restarting realtime library monitoring.");
            _requestCount = 0;
            _libraryMonitor?.Start();
        }

        /// <inheritdoc/>
        public void RequestStop()
        {
            if (_libraryMonitor == null)
            {
                return;
            }

            _logger.LogInformation("Received request to stop realtime monitoring.");

            _requestCount++;
            _restartTimeoutTimer.Start();
            _libraryMonitor.Stop();
        }

        /// <inheritdoc/>
        public void RequestStart()
        {
            if (_libraryMonitor == null)
            {
                return;
            }

            _requestCount--;
            if (_requestCount < 0)
            {
                _requestCount = 0;
            }

            if (_requestCount != 0)
            {
                _logger.LogWarning("Realtime library monitoring start request received but waiting on previous start signals.");
                return;
            }

            _logger.LogInformation("Received request to start realtime monitoring. Starting monitoring.");
            _restartTimeoutTimer.Stop();
            _libraryMonitor.Start();
        }

        /// <inheritdoc/>
        public void RegisterLibraryMonitor(ILibraryMonitor monitor)
        {
            _libraryMonitor = monitor;
        }

         /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _restartTimeoutTimer?.Stop();
            }

            _disposed = true;
        }
    }
}
