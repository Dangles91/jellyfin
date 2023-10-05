using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;

namespace Emby.Server.Implementations.Library
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Critical Code Smell", "S4487:Unread \"private\" fields should be removed", Justification = "ok.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class VirtualFolderManager : IVirtualFolderManager
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "yes.")]
        private const string ShortcutFileExtension = ".mblink";

        private readonly ILibraryMonitor _libraryMonitor;
        private readonly IFileSystem _fileSystem;
        private readonly IServerConfigurationManager _configurationManager;
        private readonly ILibraryRootFolderManager _libraryRootFolderManager;
        private readonly ILibraryOptionsManager _libraryOptionsManager;
        private readonly ITaskManager _taskManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualFolderManager"/> class.
        /// </summary>
        /// <param name="libraryMonitor">the monitor.</param>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="configurationManager">Config manager.</param>
        /// <param name="libraryRootFolderManager">x.</param>
        /// <param name="libraryOptionsManager">cc.</param>
        /// <param name="taskManager">dd.</param>
        public VirtualFolderManager(
            ILibraryMonitor libraryMonitor,
            IFileSystem fileSystem,
            IServerConfigurationManager configurationManager,
            ILibraryRootFolderManager libraryRootFolderManager,
            ILibraryOptionsManager libraryOptionsManager,
            ITaskManager taskManager)
        {
            //TODO: Dangles: Invert this dependency. Use events or event hub.
            _libraryMonitor = libraryMonitor;
            _fileSystem = fileSystem;
            _configurationManager = configurationManager;
            _libraryRootFolderManager = libraryRootFolderManager;
            _libraryOptionsManager = libraryOptionsManager;
            _taskManager = taskManager;
        }

        /// <summary>
        /// thing.
        /// </summary>
        /// <param name="name">name.</param>
        /// <param name="collectionType">type.</param>
        /// <param name="options">opt.</param>
        /// <param name="refreshLibrary">refresh.</param>
        /// <returns>return.</returns>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1114:Parameter list should follow declaration", Justification = "proto")]
        public async Task AddVirtualFolder(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            string name,
            CollectionTypeOptions? collectionType,
            LibraryOptions options,
            bool refreshLibrary)
        {
            throw new NotImplementedException();
        }
    }
}
