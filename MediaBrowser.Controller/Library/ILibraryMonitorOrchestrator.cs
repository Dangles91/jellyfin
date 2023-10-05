using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Manage a <see cref="ILibraryMonitor"/> instance.
    /// </summary>
    public interface ILibraryMonitorOrchestrator
    {
        /// <summary>
        /// Register the library monitor instance to be managed.
        /// </summary>
        /// <param name="monitor">THe monitor instance.</param>
        void RegisterLibraryMonitor(ILibraryMonitor monitor);

        /// <summary>
        /// Request to start the library monitor.
        /// </summary>
        void RequestStart();

        /// <summary>
        /// Request to stop the library monitor.
        /// </summary>
        void RequestStop();
    }
}
