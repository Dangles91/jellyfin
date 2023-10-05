#pragma warning disable CS1591

using System.Collections.Generic;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Querying;

namespace Emby.Server.Implementations.Images
{
    public abstract class BaseFolderImageProvider<T> : BaseDynamicImageProvider<T>
        where T : Folder, new()
    {
        private readonly IItemQueryService _itemQueryService;

        protected BaseFolderImageProvider(
            IFileSystem fileSystem,
            IProviderManager providerManager,
            IApplicationPaths applicationPaths,
            IImageProcessor imageProcessor,
            IItemQueryService itemQueryService)
            : base(fileSystem, providerManager, applicationPaths, imageProcessor)
        {
            _itemQueryService = itemQueryService;
        }

        protected override IReadOnlyList<BaseItem> GetItemsWithImages(BaseItem item)
        {
            return _itemQueryService.GetItemList(new InternalItemsQuery
            {
                Parent = item,
                Recursive = true,
                DtoOptions = new DtoOptions(true),
                ImageTypes = new ImageType[] { ImageType.Primary },
                OrderBy = new (string, SortOrder)[]
                {
                    (ItemSortBy.IsFolder, SortOrder.Ascending),
                    (ItemSortBy.SortName, SortOrder.Ascending)
                },
                Limit = 1
            });
        }

        protected override string CreateImage(BaseItem item, IReadOnlyCollection<BaseItem> itemsWithImages, string outputPathWithoutExtension, ImageType imageType, int imageIndex)
        {
            return CreateSingleImage(itemsWithImages, outputPathWithoutExtension, ImageType.Primary);
        }

        protected override bool Supports(BaseItem item)
        {
            return item is T;
        }

        protected override bool HasChangedByDate(BaseItem item, ItemImageInfo image)
        {
            if (item is MusicAlbum)
            {
                return false;
            }

            return base.HasChangedByDate(item, image);
        }
    }
}
