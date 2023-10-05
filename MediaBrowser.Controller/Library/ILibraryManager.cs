#nullable disable

#pragma warning disable CA1002, CS1591

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Controller.Sorting;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Querying;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;
using Genre = MediaBrowser.Controller.Entities.Genre;
using Person = MediaBrowser.Controller.Entities.Person;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Interface ILibraryManager.
    /// </summary>
    public interface ILibraryManager
    {
        bool IsScanRunning { get; }

        /// <summary>
        /// Gets a Genre.
        /// </summary>
        /// <param name="name">The name of the genre.</param>
        /// <returns>Task{Genre}.</returns>
        Genre GetGenre(string name);

        /// <summary>
        /// Gets the genre.
        /// </summary>
        /// <param name="name">The name of the music genre.</param>
        /// <returns>Task{MusicGenre}.</returns>
        MusicGenre GetMusicGenre(string name);

        /// <summary>
        /// Gets a Year.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>Task{Year}.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Throws if year is invalid.</exception>
        Year GetYear(int value);

        /// <summary>
        /// Reloads the root media folder.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        Task ValidateMediaLibrary(IProgress<double> progress, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the item by id.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>BaseItem.</returns>
        BaseItem GetItemById(Guid id);

        /// <summary>
        /// Gets the intros.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="user">The user.</param>
        /// <returns>IEnumerable{System.String}.</returns>
        Task<IEnumerable<Video>> GetIntros(BaseItem item, User user);

        /// <summary>
        /// Adds the parts.
        /// </summary>
        /// <param name="rules">The rules.</param>
        /// <param name="introProviders">The intro providers.</param>
        /// <param name="itemComparers">The item comparers.</param>
        /// <param name="postscanTasks">The postscan tasks.</param>
        void AddParts(
            IEnumerable<IResolverIgnoreRule> rules,
            IEnumerable<IIntroProvider> introProviders,
            IEnumerable<IBaseItemComparer> itemComparers,
            IEnumerable<ILibraryPostScanTask> postscanTasks);

        /// <summary>
        /// Sorts the specified items.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="user">The user.</param>
        /// <param name="sortBy">The sort by.</param>
        /// <param name="sortOrder">The sort order.</param>
        /// <returns>IEnumerable{BaseItem}.</returns>
        IEnumerable<BaseItem> Sort(IEnumerable<BaseItem> items, User user, IEnumerable<string> sortBy, SortOrder sortOrder);

        IEnumerable<BaseItem> Sort(IEnumerable<BaseItem> items, User user, IEnumerable<(string OrderBy, SortOrder SortOrder)> orderBy);

        /// <summary>
        /// Gets the user root folder.
        /// </summary>
        /// <returns>UserRootFolder.</returns>
        Folder GetUserRootFolder();

        /// <summary>
        /// Gets the season number from path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>System.Nullable&lt;System.Int32&gt;.</returns>
        int? GetSeasonNumberFromPath(string path);

        /// <summary>
        /// Fills the missing episode numbers from path.
        /// </summary>
        /// <param name="episode">Episode to use.</param>
        /// <param name="forceRefresh">Option to force refresh of episode numbers.</param>
        /// <returns>True if successful.</returns>
        bool FillMissingEpisodeNumbersFromPath(Episode episode, bool forceRefresh);

        /// <summary>
        /// Parses the name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>ItemInfo.</returns>
        ItemLookupInfo ParseName(string name);

        /// <summary>
        /// Finds the extras.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="fileSystemChildren">The file system children.</param>
        /// <param name="directoryService">An instance of <see cref="IDirectoryService"/>.</param>
        /// <returns>IEnumerable&lt;BaseItem&gt;.</returns>
        IEnumerable<BaseItem> FindExtras(BaseItem owner, IReadOnlyList<FileSystemMetadata> fileSystemChildren, IDirectoryService directoryService);

        /// <summary>
        /// Gets the people items.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>List&lt;Person&gt;.</returns>
        List<Person> GetPeopleItems(InternalPeopleQuery query);

        Task UpdateImagesAsync(BaseItem item, bool forceUpdate = false);

        /// <summary>
        /// Updates the people.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="people">The people.</param>
        void UpdatePeople(BaseItem item, List<PersonInfo> people);

        /// <summary>
        /// Asynchronously updates the people.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="people">The people.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The async task.</returns>
        Task UpdatePeopleAsync(BaseItem item, List<PersonInfo> people, CancellationToken cancellationToken);

        /// <summary>
        /// Converts the image to local.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="image">The image.</param>
        /// <param name="imageIndex">Index of the image.</param>
        /// <returns>Task.</returns>
        Task<ItemImageInfo> ConvertImageToLocal(BaseItem item, ItemImageInfo image, int imageIndex);

        Guid GetStudioId(string name);

        Guid GetGenreId(string name);

        Guid GetMusicGenreId(string name);

        Task RunMetadataSavers(BaseItem item, ItemUpdateType updateReason);

        BaseItem GetParentItem(Guid? parentId, Guid? userId);

        /// <summary>
        /// Queue a library scan.
        /// </summary>
        /// <remarks>
        /// This exists so plugins can trigger a library scan.
        /// </remarks>
        void QueueLibraryScan();
    }
}
