#pragma warning disable CS1591

using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Manager;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.LiveTv
{
    public class LiveTvMetadataService : MetadataService<LiveTvChannel, ItemLookupInfo>
    {
        public LiveTvMetadataService(
            IServerConfigurationManager serverConfigurationManager,
            ILogger<LiveTvMetadataService> logger,
            IProviderManager providerManager,
            IFileSystem fileSystem,
            ILibraryManager libraryManager,
            ILibraryOptionsManager libraryOptionsManager)
            : base(serverConfigurationManager, logger, providerManager, fileSystem, libraryManager, libraryOptionsManager)
        {
        }
    }
}
