using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.BaseItemManager;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Book = MediaBrowser.Controller.Entities.Book;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;
using Movie = MediaBrowser.Controller.Entities.Movies.Movie;
using MusicAlbum = MediaBrowser.Controller.Entities.Audio.MusicAlbum;
using Season = MediaBrowser.Controller.Entities.TV.Season;
using Series = MediaBrowser.Controller.Entities.TV.Series;

namespace MediaBrowser.Providers.Manager
{
    /// <summary>
    /// Class ProviderManager.
    /// </summary>
    public class ProviderManager : IProviderManager
    {
        private readonly ILogger<ProviderManager> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly IFileSystem _fileSystem;
        private readonly IServerApplicationPaths _appPaths;
        private readonly ISubtitleManager _subtitleManager;
        private readonly IServerConfigurationManager _configurationManager;
        private readonly IBaseItemManager _baseItemManager;
        private readonly IVirtualFolderManager _virtualFolderManager;
        private readonly IItemService _itemService;

        private IImageProvider[] _imageProviders = Array.Empty<IImageProvider>();
        private IMetadataService[] _metadataServices = Array.Empty<IMetadataService>();
        private IMetadataProvider[] _metadataProviders = Array.Empty<IMetadataProvider>();
        private IMetadataSaver[] _savers = Array.Empty<IMetadataSaver>();
        private IExternalId[] _externalIds = Array.Empty<IExternalId>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderManager"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The Http client factory.</param>
        /// <param name="subtitleManager">The subtitle manager.</param>
        /// <param name="configurationManager">The configuration manager.</param>
        /// <param name="libraryMonitor">The library monitor.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="fileSystem">The filesystem.</param>
        /// <param name="appPaths">The server application paths.</param>
        /// <param name="baseItemManager">The BaseItem manager.</param>
        /// <param name="virtualFolderManager">The instance of <see cref="IVirtualFolderManager"/> interface.</param>
        /// <param name="itemService">The instance of <see cref="IItemService"/> interface.</param>
        public ProviderManager(
            IHttpClientFactory httpClientFactory,
            ISubtitleManager subtitleManager,
            IServerConfigurationManager configurationManager,
            ILibraryMonitor libraryMonitor,
            ILogger<ProviderManager> logger,
            IFileSystem fileSystem,
            IServerApplicationPaths appPaths,
            IBaseItemManager baseItemManager,
            IVirtualFolderManager virtualFolderManager,
            IItemService itemService)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configurationManager = configurationManager;
            _libraryMonitor = libraryMonitor;
            _fileSystem = fileSystem;
            _appPaths = appPaths;
            _subtitleManager = subtitleManager;
            _baseItemManager = baseItemManager;
            _virtualFolderManager = virtualFolderManager;
            _itemService = itemService;
        }

        /// <inheritdoc/>
        public void AddParts(
            IEnumerable<IImageProvider> imageProviders,
            IEnumerable<IMetadataService> metadataServices,
            IEnumerable<IMetadataProvider> metadataProviders,
            IEnumerable<IMetadataSaver> metadataSavers,
            IEnumerable<IExternalId> externalIds)
        {
            _imageProviders = imageProviders.ToArray();
            _metadataServices = metadataServices.OrderBy(i => i.Order).ToArray();
            _metadataProviders = metadataProviders.ToArray();
            _externalIds = externalIds.OrderBy(i => i.ProviderName).ToArray();

            _savers = metadataSavers.ToArray();
        }

        /// <inheritdoc/>
        public async Task SaveImage(BaseItem item, string url, ImageType type, int? imageIndex, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);
            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new HttpRequestException("Invalid image received.", null, response.StatusCode);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;

            // Workaround for tvheadend channel icons
            // TODO: Isolate this hack into the tvh plugin
            if (string.IsNullOrEmpty(contentType))
            {
                if (url.Contains("/imagecache/", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = "image/png";
                }
                else
                {
                    throw new HttpRequestException("Invalid image received: contentType not set.", null, response.StatusCode);
                }
            }

            // TVDb will sometimes serve a rubbish 404 html page with a 200 OK code, because reasons...
            if (contentType.Equals(MediaTypeNames.Text.Html, StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpRequestException("Invalid image received.", null, HttpStatusCode.NotFound);
            }

            // some iptv/epg providers don't correctly report media type, extract from url if no extension found
            if (string.IsNullOrWhiteSpace(MimeTypes.ToExtension(contentType)))
            {
                contentType = MimeTypes.GetMimeType(url);
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                await SaveImage(
                    item,
                    stream,
                    contentType,
                    type,
                    imageIndex,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task SaveImage(BaseItem item, Stream source, string mimeType, ImageType type, int? imageIndex, CancellationToken cancellationToken)
        {
            return new ImageSaver(_configurationManager, _libraryMonitor, _fileSystem, _logger).SaveImage(item, source, mimeType, type, imageIndex, cancellationToken);
        }

        /// <inheritdoc/>
        public Task SaveImage(BaseItem item, string source, string mimeType, ImageType type, int? imageIndex, bool? saveLocallyWithMedia, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentNullException(nameof(source));
            }

            var fileStream = AsyncFile.OpenRead(source);
            return new ImageSaver(_configurationManager, _libraryMonitor, _fileSystem, _logger).SaveImage(item, fileStream, mimeType, type, imageIndex, saveLocallyWithMedia, cancellationToken);
        }

        /// <inheritdoc/>
        public Task SaveImage(Stream source, string mimeType, string path)
        {
            return new ImageSaver(_configurationManager, _libraryMonitor, _fileSystem, _logger)
                .SaveImage(source, path);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<RemoteImageInfo>> GetAvailableRemoteImages(BaseItem item, RemoteImageQuery query, CancellationToken cancellationToken)
        {
            var providers = GetRemoteImageProviders(item, query.IncludeDisabledProviders);

            if (!string.IsNullOrEmpty(query.ProviderName))
            {
                var providerName = query.ProviderName;

                providers = providers.Where(i => string.Equals(i.Name, providerName, StringComparison.OrdinalIgnoreCase));
            }

            if (query.ImageType is not null)
            {
                providers = providers.Where(i => i.GetSupportedImages(item).Contains(query.ImageType.Value));
            }

            var preferredLanguage = item.GetPreferredMetadataLanguage();

            var tasks = providers.Select(i => GetImages(item, i, preferredLanguage, query.IncludeAllLanguages, cancellationToken, query.ImageType));

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            return results.SelectMany(i => i.ToList());
        }

        /// <summary>
        /// Gets the images.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="provider">The provider.</param>
        /// <param name="preferredLanguage">The preferred language.</param>
        /// <param name="includeAllLanguages">Whether to include all languages in results.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="type">The type.</param>
        /// <returns>Task{IEnumerable{RemoteImageInfo}}.</returns>
        private async Task<IEnumerable<RemoteImageInfo>> GetImages(
            BaseItem item,
            IRemoteImageProvider provider,
            string preferredLanguage,
            bool includeAllLanguages,
            CancellationToken cancellationToken,
            ImageType? type = null)
        {
            bool hasPreferredLanguage = !string.IsNullOrWhiteSpace(preferredLanguage);

            try
            {
                var result = await provider.GetImages(item, cancellationToken).ConfigureAwait(false);

                if (type.HasValue)
                {
                    result = result.Where(i => i.Type == type.Value);
                }

                if (!includeAllLanguages && hasPreferredLanguage)
                {
                    // Filter out languages that do not match the preferred languages.
                    //
                    // TODO: should exception case of "en" (English) eventually be removed?
                    result = result.Where(i => string.IsNullOrWhiteSpace(i.Language) ||
                                               string.Equals(preferredLanguage, i.Language, StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(i.Language, "en", StringComparison.OrdinalIgnoreCase));
                }

                return result.OrderByLanguageDescending(preferredLanguage);
            }
            catch (OperationCanceledException)
            {
                return Enumerable.Empty<RemoteImageInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ProviderName} failed in GetImageInfos for type {ItemType} at {ItemPath}", provider.GetType().Name, item.GetType().Name, item.Path);
                return Enumerable.Empty<RemoteImageInfo>();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<ImageProviderInfo> GetRemoteImageProviderInfo(BaseItem item)
        {
            return GetRemoteImageProviders(item, true).Select(i => new ImageProviderInfo(i.Name, i.GetSupportedImages(item).ToArray()));
        }

        private IEnumerable<IRemoteImageProvider> GetRemoteImageProviders(BaseItem item, bool includeDisabled)
        {
            var options = GetMetadataOptions(item);
            var libraryOptions = _virtualFolderManager.GetLibraryOptions(item);

            return GetImageProvidersInternal(
                item,
                libraryOptions,
                options,
                new ImageRefreshOptions(new DirectoryService(_fileSystem)),
                includeDisabled).OfType<IRemoteImageProvider>();
        }

        /// <inheritdoc/>
        public IEnumerable<IImageProvider> GetImageProviders(BaseItem item, ImageRefreshOptions refreshOptions)
        {
            return GetImageProvidersInternal(item, _virtualFolderManager.GetLibraryOptions(item), GetMetadataOptions(item), refreshOptions, false);
        }

        private IEnumerable<IImageProvider> GetImageProvidersInternal(BaseItem item, LibraryOptions libraryOptions, MetadataOptions options, ImageRefreshOptions refreshOptions, bool includeDisabled)
        {
            var typeOptions = libraryOptions.GetTypeOptions(item.GetType().Name);
            var fetcherOrder = typeOptions?.ImageFetcherOrder ?? options.ImageFetcherOrder;

            return _imageProviders.Where(i => CanRefreshImages(i, item, typeOptions, refreshOptions, includeDisabled))
                .OrderBy(i => GetConfiguredOrder(fetcherOrder, i.Name))
                .ThenBy(GetDefaultOrder);
        }

        private bool CanRefreshImages(
            IImageProvider provider,
            BaseItem item,
            TypeOptions? libraryTypeOptions,
            ImageRefreshOptions refreshOptions,
            bool includeDisabled)
        {
            try
            {
                if (!provider.Supports(item))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ProviderName} failed in Supports for type {ItemType} at {ItemPath}", provider.GetType().Name, item.GetType().Name, item.Path);
                return false;
            }

            if (includeDisabled || provider is ILocalImageProvider)
            {
                return true;
            }

            if (item.IsLocked && refreshOptions.ImageRefreshMode != MetadataRefreshMode.FullRefresh)
            {
                return false;
            }

            return _baseItemManager.IsImageFetcherEnabled(item, libraryTypeOptions, provider.Name);
        }

        /// <inheritdoc />
        public IEnumerable<IMetadataProvider<T>> GetMetadataProviders<T>(BaseItem item, LibraryOptions libraryOptions)
            where T : BaseItem
        {
            var globalMetadataOptions = GetMetadataOptions(item);

            return GetMetadataProvidersInternal<T>(item, libraryOptions, globalMetadataOptions, false, false);
        }

        private IEnumerable<IMetadataProvider<T>> GetMetadataProvidersInternal<T>(BaseItem item, LibraryOptions libraryOptions, MetadataOptions globalMetadataOptions, bool includeDisabled, bool forceEnableInternetMetadata)
            where T : BaseItem
        {
            var localMetadataReaderOrder = libraryOptions.LocalMetadataReaderOrder ?? globalMetadataOptions.LocalMetadataReaderOrder;
            var typeOptions = libraryOptions.GetTypeOptions(item.GetType().Name);
            var metadataFetcherOrder = typeOptions?.MetadataFetcherOrder ?? globalMetadataOptions.MetadataFetcherOrder;

            return _metadataProviders.OfType<IMetadataProvider<T>>()
                .Where(i => CanRefreshMetadata(i, item, typeOptions, includeDisabled, forceEnableInternetMetadata))
                .OrderBy(i =>
                    // local and remote providers will be interleaved in the final order
                    // only relative order within a type matters: consumers of the list filter to one or the other
                    i switch
                    {
                        ILocalMetadataProvider => GetConfiguredOrder(localMetadataReaderOrder, i.Name),
                        IRemoteMetadataProvider => GetConfiguredOrder(metadataFetcherOrder, i.Name),
                        // Default to end
                        _ => int.MaxValue
                    })
                .ThenBy(GetDefaultOrder);
        }

        /// <inheritdoc />
        public IMetadataService? GetMetadataServiceFor<T>(T item)
            where T : BaseItem
        {
            return _metadataServices.FirstOrDefault(current => current.CanRefreshPrimary(typeof(T)))
                ?? _metadataServices.FirstOrDefault(current => current.CanRefresh(item));
        }

        private bool CanRefreshMetadata(
            IMetadataProvider provider,
            BaseItem item,
            TypeOptions? libraryTypeOptions,
            bool includeDisabled,
            bool forceEnableInternetMetadata)
        {
            if (!item.SupportsLocalMetadata && provider is ILocalMetadataProvider)
            {
                return false;
            }

            if (includeDisabled)
            {
                return true;
            }

            // If locked only allow local providers
            if (item.IsLocked && provider is not ILocalMetadataProvider && provider is not IForcedProvider)
            {
                return false;
            }

            if (forceEnableInternetMetadata || provider is not IRemoteMetadataProvider)
            {
                return true;
            }

            return _baseItemManager.IsMetadataFetcherEnabled(item, libraryTypeOptions, provider.Name);
        }

        private static int GetConfiguredOrder(string[] order, string providerName)
        {
            var index = Array.IndexOf(order, providerName);

            if (index != -1)
            {
                return index;
            }

            // default to end
            return int.MaxValue;
        }

        private static int GetDefaultOrder(object provider)
        {
            if (provider is IHasOrder hasOrder)
            {
                return hasOrder.Order;
            }

            // after items that want to be first (~0) but before items that want to be last (~100)
            return 50;
        }

        /// <inheritdoc/>
        public MetadataPluginSummary[] GetAllMetadataPlugins()
        {
            return new[]
            {
                GetPluginSummary<Movie>(),
                GetPluginSummary<BoxSet>(),
                GetPluginSummary<Book>(),
                GetPluginSummary<Series>(),
                GetPluginSummary<Season>(),
                GetPluginSummary<Episode>(),
                GetPluginSummary<MusicAlbum>(),
                GetPluginSummary<MusicArtist>(),
                GetPluginSummary<Audio>(),
                GetPluginSummary<AudioBook>(),
                GetPluginSummary<Studio>(),
                GetPluginSummary<MusicVideo>(),
                GetPluginSummary<Video>()
            };
        }

        private MetadataPluginSummary GetPluginSummary<T>()
            where T : BaseItem, new()
        {
            // Give it a dummy path just so that it looks like a file system item
            var dummy = new T
            {
                Path = Path.Combine(_appPaths.InternalMetadataPath, "dummy"),
                ParentId = Guid.NewGuid()
            };

            var options = GetMetadataOptions(dummy);

            var summary = new MetadataPluginSummary
            {
                ItemType = typeof(T).Name
            };

            var libraryOptions = new LibraryOptions();

            var imageProviders = GetImageProvidersInternal(
                dummy,
                libraryOptions,
                options,
                new ImageRefreshOptions(new DirectoryService(_fileSystem)),
                true).ToList();

            var pluginList = summary.Plugins.ToList();

            AddMetadataPlugins(pluginList, dummy, libraryOptions, options);
            AddImagePlugins(pluginList, imageProviders);

            var subtitleProviders = _subtitleManager.GetSupportedProviders(dummy);

            // Subtitle fetchers
            pluginList.AddRange(subtitleProviders.Select(i => new MetadataPlugin
            {
                Name = i.Name,
                Type = MetadataPluginType.SubtitleFetcher
            }));

            summary.Plugins = pluginList.ToArray();

            var supportedImageTypes = imageProviders.OfType<IRemoteImageProvider>()
                .SelectMany(i => i.GetSupportedImages(dummy))
                .ToList();

            supportedImageTypes.AddRange(imageProviders.OfType<IDynamicImageProvider>()
                .SelectMany(i => i.GetSupportedImages(dummy)));

            summary.SupportedImageTypes = supportedImageTypes.Distinct().ToArray();

            return summary;
        }

        private void AddMetadataPlugins<T>(List<MetadataPlugin> list, T item, LibraryOptions libraryOptions, MetadataOptions options)
            where T : BaseItem
        {
            var providers = GetMetadataProvidersInternal<T>(item, libraryOptions, options, true, true).ToList();

            // Locals
            list.AddRange(providers.Where(i => i is ILocalMetadataProvider).Select(i => new MetadataPlugin
            {
                Name = i.Name,
                Type = MetadataPluginType.LocalMetadataProvider
            }));

            // Fetchers
            list.AddRange(providers.Where(i => i is IRemoteMetadataProvider).Select(i => new MetadataPlugin
            {
                Name = i.Name,
                Type = MetadataPluginType.MetadataFetcher
            }));

            // Savers
            list.AddRange(_savers.Where(i => IsSaverEnabledForItem(i, item, libraryOptions, ItemUpdateType.MetadataEdit, true)).OrderBy(i => i.Name).Select(i => new MetadataPlugin
            {
                Name = i.Name,
                Type = MetadataPluginType.MetadataSaver
            }));
        }

        private void AddImagePlugins(List<MetadataPlugin> list, List<IImageProvider> imageProviders)
        {
            // Locals
            list.AddRange(imageProviders.Where(i => i is ILocalImageProvider).Select(i => new MetadataPlugin
            {
                Name = i.Name,
                Type = MetadataPluginType.LocalImageProvider
            }));

            // Fetchers
            list.AddRange(imageProviders.Where(i => i is IDynamicImageProvider || (i is IRemoteImageProvider)).Select(i => new MetadataPlugin
            {
                Name = i.Name,
                Type = MetadataPluginType.ImageFetcher
            }));
        }

        /// <inheritdoc/>
        public MetadataOptions GetMetadataOptions(BaseItem item)
            => _configurationManager.GetMetadataOptionsForType(item.GetType().Name) ?? new MetadataOptions();

        /// <inheritdoc/>
        public Task SaveMetadataAsync(BaseItem item, ItemUpdateType updateType)
            => SaveMetadataAsync(item, updateType, _savers);

        /// <inheritdoc/>
        public Task SaveMetadataAsync(BaseItem item, ItemUpdateType updateType, IEnumerable<string> savers)
            => SaveMetadataAsync(item, updateType, _savers.Where(i => savers.Contains(i.Name, StringComparison.OrdinalIgnoreCase)));

        /// <summary>
        /// Saves the metadata.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="updateType">Type of the update.</param>
        /// <param name="savers">The savers.</param>
        private async Task SaveMetadataAsync(BaseItem item, ItemUpdateType updateType, IEnumerable<IMetadataSaver> savers)
        {
            var libraryOptions = _virtualFolderManager.GetLibraryOptions(item);

            foreach (var saver in savers.Where(i => IsSaverEnabledForItem(i, item, libraryOptions, updateType, false)))
            {
                _logger.LogDebug("Saving {Item} to {Saver}", item.Path ?? item.Name, saver.Name);

                if (saver is IMetadataFileSaver fileSaver)
                {
                    string path;

                    try
                    {
                        path = fileSaver.GetSavePath(item);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in {Saver} GetSavePath", saver.Name);
                        continue;
                    }

                    try
                    {
                        _libraryMonitor.ReportFileSystemChangeBeginning(path);
                        await saver.SaveAsync(item, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in metadata saver");
                    }
                    finally
                    {
                        _libraryMonitor.ReportFileSystemChangeComplete(path, false);
                    }
                }
                else
                {
                    try
                    {
                        await saver.SaveAsync(item, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in metadata saver");
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether [is saver enabled for item] [the specified saver].
        /// </summary>
        private bool IsSaverEnabledForItem(IMetadataSaver saver, BaseItem item, LibraryOptions libraryOptions, ItemUpdateType updateType, bool includeDisabled)
        {
            var options = GetMetadataOptions(item);

            try
            {
                if (!saver.IsEnabledFor(item, updateType))
                {
                    return false;
                }

                if (!includeDisabled)
                {
                    if (libraryOptions.MetadataSavers is null)
                    {
                        if (options.DisabledMetadataSavers.Contains(saver.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }

                        if (!item.IsSaveLocalMetadataEnabled())
                        {
                            if (updateType >= ItemUpdateType.MetadataEdit)
                            {
                                // Manual edit occurred
                                // Even if save local is off, save locally anyway if the metadata file already exists
                                if (saver is not IMetadataFileSaver fileSaver || !File.Exists(fileSaver.GetSavePath(item)))
                                {
                                    return false;
                                }
                            }
                            else
                            {
                                // Manual edit did not occur
                                // Since local metadata saving is disabled, consider it disabled
                                return false;
                            }
                        }
                    }
                    else
                    {
                        if (!libraryOptions.MetadataSavers.Contains(saver.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in {Saver}.IsEnabledFor", saver.Name);
                return false;
            }
        }

        /// <inheritdoc/>
        public Task<IEnumerable<RemoteSearchResult>> GetRemoteSearchResults<TItemType, TLookupType>(RemoteSearchQuery<TLookupType> searchInfo, CancellationToken cancellationToken)
            where TItemType : BaseItem, new()
            where TLookupType : ItemLookupInfo
        {
            BaseItem? referenceItem = null;

            if (!searchInfo.ItemId.Equals(default))
            {
                referenceItem = _itemService.GetItemById(searchInfo.ItemId);
            }

            return GetRemoteSearchResults<TItemType, TLookupType>(searchInfo, referenceItem, cancellationToken);
        }

        private async Task<IEnumerable<RemoteSearchResult>> GetRemoteSearchResults<TItemType, TLookupType>(RemoteSearchQuery<TLookupType> searchInfo, BaseItem? referenceItem, CancellationToken cancellationToken)
            where TItemType : BaseItem, new()
            where TLookupType : ItemLookupInfo
        {
            LibraryOptions libraryOptions;

            if (referenceItem is null)
            {
                // Give it a dummy path just so that it looks like a file system item
                var dummy = new TItemType
                {
                    Path = Path.Combine(_appPaths.InternalMetadataPath, "dummy"),
                    ParentId = Guid.NewGuid()
                };

                dummy.SetParent(new Folder());

                referenceItem = dummy;
                libraryOptions = new LibraryOptions();
            }
            else
            {
                libraryOptions = _virtualFolderManager.GetLibraryOptions(referenceItem);
            }

            var options = GetMetadataOptions(referenceItem);

            var providers = GetMetadataProvidersInternal<TItemType>(referenceItem, libraryOptions, options, searchInfo.IncludeDisabledProviders, false)
                .OfType<IRemoteSearchProvider<TLookupType>>();

            if (!string.IsNullOrEmpty(searchInfo.SearchProviderName))
            {
                providers = providers.Where(i => string.Equals(i.Name, searchInfo.SearchProviderName, StringComparison.OrdinalIgnoreCase));
            }

            if (string.IsNullOrWhiteSpace(searchInfo.SearchInfo.MetadataLanguage))
            {
                searchInfo.SearchInfo.MetadataLanguage = _configurationManager.Configuration.PreferredMetadataLanguage;
            }

            if (string.IsNullOrWhiteSpace(searchInfo.SearchInfo.MetadataCountryCode))
            {
                searchInfo.SearchInfo.MetadataCountryCode = _configurationManager.Configuration.MetadataCountryCode;
            }

            var resultList = new List<RemoteSearchResult>();

            foreach (var provider in providers)
            {
                try
                {
                    var results = await provider.GetSearchResults(searchInfo.SearchInfo, cancellationToken).ConfigureAwait(false);

                    foreach (var result in results)
                    {
                        result.SearchProviderName = provider.Name;

                        var existingMatch = resultList.FirstOrDefault(i => i.ProviderIds.Any(p => string.Equals(result.GetProviderId(p.Key), p.Value, StringComparison.OrdinalIgnoreCase)));

                        if (existingMatch is null)
                        {
                            resultList.Add(result);
                        }
                        else
                        {
                            foreach (var providerId in result.ProviderIds)
                            {
                                existingMatch.ProviderIds.TryAdd(providerId.Key, providerId.Value);
                            }

                            if (string.IsNullOrWhiteSpace(existingMatch.ImageUrl))
                            {
                                existingMatch.ImageUrl = result.ImageUrl;
                            }
                        }
                    }
                }
#pragma warning disable CA1031 // do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // do not catch general exception types
                {
                    _logger.LogError(ex, "Provider {ProviderName} failed to retrieve search results", provider.Name);
                }
            }

            return resultList;
        }

        private IEnumerable<IExternalId> GetExternalIds(IHasProviderIds item)
        {
            return _externalIds.Where(i =>
            {
                try
                {
                    return i.Supports(item);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in {Type}.Supports", i.GetType().Name);
                    return false;
                }
            });
        }

        /// <inheritdoc/>
        public IEnumerable<ExternalUrl> GetExternalUrls(BaseItem item)
        {
            return GetExternalIds(item)
                .Select(i =>
            {
                if (string.IsNullOrEmpty(i.UrlFormatString))
                {
                    return null;
                }

                var value = item.GetProviderId(i.Key);

                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                return new ExternalUrl
                {
                    Name = i.ProviderName,
                    Url = string.Format(
                        CultureInfo.InvariantCulture,
                        i.UrlFormatString,
                        value)
                };
            }).Where(i => i is not null)
                .Concat(item.GetRelatedUrls())!; // We just filtered out all the nulls
        }

        /// <inheritdoc/>
        public IEnumerable<ExternalIdInfo> GetExternalIdInfos(IHasProviderIds item)
        {
            return GetExternalIds(item)
                .Select(i => new ExternalIdInfo(
                    name: i.ProviderName,
                    key: i.Key,
                    type: i.Type,
                    urlFormatString: i.UrlFormatString));
        }
    }
}
