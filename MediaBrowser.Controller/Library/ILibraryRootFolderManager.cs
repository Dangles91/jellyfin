using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Manage library root folers.
    /// </summary>
    public interface ILibraryRootFolderManager
    {
        /// <summary>
        /// Create library root folder.
        /// </summary>
        /// <returns>The library root foler instance.</returns>
        AggregateFolder CreateRootFolder();

        /// <summary>
        /// Get the root folder. Create if it doesn't exist.
        /// </summary>
        /// <returns>The library root folder instance.</returns>
        AggregateFolder GetRootFolder();

        /// <summary>
        /// Get the user root folder.
        /// </summary>
        /// <returns>The user root folder instance.</returns>
        Folder GetUserRootFolder();

        /// <summary>
        /// Validate top level library folders.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task ValidateTopLibraryFolders(CancellationToken cancellationToken);
    }
}
