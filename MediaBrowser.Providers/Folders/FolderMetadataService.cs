#pragma warning disable CS1591

using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Manager;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Folders
{
    public class FolderMetadataService : MetadataService<Folder, ItemLookupInfo>
    {
        public FolderMetadataService(
            IServerConfigurationManager serverConfigurationManager,
            ILogger<FolderMetadataService> logger,
            IProviderManager providerManager,
            IFileSystem fileSystem,
            ILibraryManager libraryManager,
            ILibraryOptionsManager libraryOptionsManager)
            : base(serverConfigurationManager, logger, providerManager, fileSystem, libraryManager, libraryOptionsManager)
        {
        }

        /// <inheritdoc />
        // Make sure the type-specific services get picked first
        public override int Order => 10;
    }
}
