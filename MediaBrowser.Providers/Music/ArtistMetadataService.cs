#pragma warning disable CS1591

using System.Collections.Generic;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Manager;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Music
{
    public class ArtistMetadataService : MetadataService<MusicArtist, ArtistInfo>
    {
        public ArtistMetadataService(
            IServerConfigurationManager serverConfigurationManager,
            ILogger<ArtistMetadataService> logger,
            IProviderManager providerManager,
            IFileSystem fileSystem,
            ILibraryManager libraryManager,
            ILibraryOptionsManager libraryOptionsManager)
            : base(serverConfigurationManager, logger, providerManager, fileSystem, libraryManager, libraryOptionsManager)
        {
        }

        /// <inheritdoc />
        protected override bool EnableUpdatingGenresFromChildren => true;

        /// <inheritdoc />
        protected override IList<BaseItem> GetChildrenForMetadataUpdates(MusicArtist item)
        {
            return item.IsAccessedByName
                ? item.GetTaggedItems(new InternalItemsQuery
                {
                    Recursive = true,
                    IsFolder = false
                })
                : item.GetRecursiveChildren(i => i is IHasArtist && !i.IsFolder);
        }
    }
}
