#nullable disable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Naming.Common;
using MediaBrowser.Controller.Chapters;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.MediaInfo
{
    /// <summary>
    /// The probe provider.
    /// </summary>
    public class ProbeProvider : ICustomMetadataProvider<Episode>,
        ICustomMetadataProvider<MusicVideo>,
        ICustomMetadataProvider<Movie>,
        ICustomMetadataProvider<Trailer>,
        ICustomMetadataProvider<Video>,
        ICustomMetadataProvider<Audio>,
        ICustomMetadataProvider<AudioBook>,
        IHasOrder,
        IForcedProvider,
        IPreRefreshProvider,
        IHasItemChangeMonitor
    {
        private readonly ILogger<ProbeProvider> _logger;
        private readonly AudioResolver _audioResolver;
        private readonly SubtitleResolver _subtitleResolver;
        private readonly FFProbeVideoInfo _videoProber;
        private readonly AudioFileProber _audioProber;
        private readonly Task<ItemUpdateType> _cachedTask = Task.FromResult(ItemUpdateType.None);

        /// <summary>
        /// Initializes a new instance of the <see cref="ProbeProvider"/> class.
        /// </summary>
        /// <param name="mediaSourceManager">Instance of the <see cref="IMediaSourceManager"/> interface.</param>
        /// <param name="mediaEncoder">Instance of the <see cref="IMediaEncoder"/> interface.</param>
        /// <param name="itemRepo">Instance of the <see cref="IItemRepository"/> interface.</param>
        /// <param name="blurayExaminer">Instance of the <see cref="IBlurayExaminer"/> interface.</param>
        /// <param name="localization">Instance of the <see cref="ILocalizationManager"/> interface.</param>
        /// <param name="encodingManager">Instance of the <see cref="IEncodingManager"/> interface.</param>
        /// <param name="config">Instance of the <see cref="IServerConfigurationManager"/> interface.</param>
        /// <param name="subtitleManager">Instance of the <see cref="ISubtitleManager"/> interface.</param>
        /// <param name="chapterManager">Instance of the <see cref="IChapterManager"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        /// <param name="itemService">Instance of the <see cref="IItemService"/> interface.</param>
        /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/>.</param>
        /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
        /// <param name="namingOptions">The <see cref="NamingOptions"/>.</param>
        /// <param name="virtualFolderManager">Instance of the <see cref="IVirtualFolderManager"/> interface.</param>
        public ProbeProvider(
            IMediaSourceManager mediaSourceManager,
            IMediaEncoder mediaEncoder,
            IItemRepository itemRepo,
            IBlurayExaminer blurayExaminer,
            ILocalizationManager localization,
            IEncodingManager encodingManager,
            IServerConfigurationManager config,
            ISubtitleManager subtitleManager,
            IChapterManager chapterManager,
            ILibraryManager libraryManager,
            IItemService itemService,
            IFileSystem fileSystem,
            ILoggerFactory loggerFactory,
            NamingOptions namingOptions,
            IVirtualFolderManager virtualFolderManager)
        {
            _logger = loggerFactory.CreateLogger<ProbeProvider>();
            _audioProber = new AudioFileProber(loggerFactory.CreateLogger<AudioFileProber>(), mediaSourceManager, mediaEncoder, itemRepo, libraryManager, virtualFolderManager);
            _audioResolver = new AudioResolver(loggerFactory.CreateLogger<AudioResolver>(), localization, mediaEncoder, fileSystem, namingOptions);
            _subtitleResolver = new SubtitleResolver(loggerFactory.CreateLogger<SubtitleResolver>(), localization, mediaEncoder, fileSystem, namingOptions);
            _videoProber = new FFProbeVideoInfo(
                loggerFactory.CreateLogger<FFProbeVideoInfo>(),
                mediaSourceManager,
                mediaEncoder,
                itemRepo,
                blurayExaminer,
                localization,
                encodingManager,
                config,
                subtitleManager,
                chapterManager,
                libraryManager,
                itemService,
                _audioResolver,
                _subtitleResolver,
                virtualFolderManager);
        }

        /// <inheritdoc />
        public string Name => "Probe Provider";

        /// <inheritdoc />
        public int Order => 100;

        /// <inheritdoc />
        public bool HasChanged(BaseItem item, IDirectoryService directoryService)
        {
            var video = item as Video;
            if (video is null || video.VideoType == VideoType.VideoFile || video.VideoType == VideoType.Iso)
            {
                var path = item.Path;

                if (!string.IsNullOrWhiteSpace(path) && item.IsFileProtocol)
                {
                    var file = directoryService.GetFile(path);
                    if (file is not null && file.LastWriteTimeUtc != item.DateModified)
                    {
                        _logger.LogDebug("Refreshing {ItemPath} due to date modified timestamp change.", path);
                        return true;
                    }
                }
            }

            if (item.SupportsLocalMetadata && video is not null && !video.IsPlaceHolder
                && !video.SubtitleFiles.SequenceEqual(
                    _subtitleResolver.GetExternalFiles(video, directoryService, false)
                    .Select(info => info.Path).ToList(),
                    StringComparer.Ordinal))
            {
                _logger.LogDebug("Refreshing {ItemPath} due to external subtitles change.", item.Path);
                return true;
            }

            if (item.SupportsLocalMetadata && video is not null && !video.IsPlaceHolder
                && !video.AudioFiles.SequenceEqual(
                    _audioResolver.GetExternalFiles(video, directoryService, false)
                    .Select(info => info.Path).ToList(),
                    StringComparer.Ordinal))
            {
                _logger.LogDebug("Refreshing {ItemPath} due to external audio change.", item.Path);
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public Task<ItemUpdateType> FetchAsync(Episode item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            return FetchVideoInfo(item, options, cancellationToken);
        }

        /// <inheritdoc />
        public Task<ItemUpdateType> FetchAsync(MusicVideo item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            return FetchVideoInfo(item, options, cancellationToken);
        }

        /// <inheritdoc />
        public Task<ItemUpdateType> FetchAsync(Movie item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            return FetchVideoInfo(item, options, cancellationToken);
        }

        /// <inheritdoc />
        public Task<ItemUpdateType> FetchAsync(Trailer item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            return FetchVideoInfo(item, options, cancellationToken);
        }

        /// <inheritdoc />
        public Task<ItemUpdateType> FetchAsync(Video item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            return FetchVideoInfo(item, options, cancellationToken);
        }

        /// <inheritdoc />
        public Task<ItemUpdateType> FetchAsync(Audio item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            return FetchAudioInfo(item, options, cancellationToken);
        }

        /// <inheritdoc />
        public Task<ItemUpdateType> FetchAsync(AudioBook item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            return FetchAudioInfo(item, options, cancellationToken);
        }

        /// <summary>
        /// Fetches video information for an item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="options">The <see cref="MetadataRefreshOptions"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <typeparam name="T">The type of item to resolve.</typeparam>
        /// <returns>A <see cref="Task"/> fetching the <see cref="ItemUpdateType"/> for an item.</returns>
        public Task<ItemUpdateType> FetchVideoInfo<T>(T item, MetadataRefreshOptions options, CancellationToken cancellationToken)
            where T : Video
        {
            if (item.IsPlaceHolder)
            {
                return _cachedTask;
            }

            if (!item.IsCompleteMedia)
            {
                return _cachedTask;
            }

            if (item.IsVirtualItem)
            {
                return _cachedTask;
            }

            if (!options.EnableRemoteContentProbe && !item.IsFileProtocol)
            {
                return _cachedTask;
            }

            if (item.IsShortcut)
            {
                FetchShortcutInfo(item);
            }

            return _videoProber.ProbeVideo(item, options, cancellationToken);
        }

        private string NormalizeStrmLine(string line)
        {
            return line.Replace("\t", string.Empty, StringComparison.Ordinal)
                .Replace("\r", string.Empty, StringComparison.Ordinal)
                .Replace("\n", string.Empty, StringComparison.Ordinal)
                .Trim();
        }

        private void FetchShortcutInfo(BaseItem item)
        {
            item.ShortcutPath = File.ReadAllLines(item.Path)
                .Select(NormalizeStrmLine)
                .FirstOrDefault(i => !string.IsNullOrWhiteSpace(i) && !i.StartsWith('#'));
        }

        /// <summary>
        /// Fetches audio information for an item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="options">The <see cref="MetadataRefreshOptions"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <typeparam name="T">The type of item to resolve.</typeparam>
        /// <returns>A <see cref="Task"/> fetching the <see cref="ItemUpdateType"/> for an item.</returns>
        public Task<ItemUpdateType> FetchAudioInfo<T>(T item, MetadataRefreshOptions options, CancellationToken cancellationToken)
            where T : Audio
        {
            if (item.IsVirtualItem)
            {
                return _cachedTask;
            }

            if (!options.EnableRemoteContentProbe && !item.IsFileProtocol)
            {
                return _cachedTask;
            }

            if (item.IsShortcut)
            {
                FetchShortcutInfo(item);
            }

            return _audioProber.Probe(item, options, cancellationToken);
        }
    }
}
