using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Library.Validators
{
    /// <summary>
    /// Class CollectionPostScanTask.
    /// </summary>
    public class CollectionPostScanTask : ILibraryPostScanTask
    {
        private readonly ICollectionManager _collectionManager;
        private readonly ILogger<CollectionPostScanTask> _logger;
        private readonly ILibraryRootFolderManager _libraryRootFolderManager;
        private readonly ILibraryOptionsManager _libraryOptionsManager;
        private readonly IItemQueryService _itemQueryService;

        /// <summary>
        /// Initializes a new instance of the <see cref="CollectionPostScanTask" /> class.
        /// </summary>
        /// <param name="collectionManager">The collection manager.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="libraryRootFolderManager">The library root folder manager.</param>
        /// <param name="libraryOptionsManager">The library options manager.</param>
        /// <param name="itemQueryService">The item query service.</param>
        public CollectionPostScanTask(
            ICollectionManager collectionManager,
            ILogger<CollectionPostScanTask> logger,
            ILibraryRootFolderManager libraryRootFolderManager,
            ILibraryOptionsManager libraryOptionsManager,
            IItemQueryService itemQueryService)
        {
            _collectionManager = collectionManager;
            _logger = logger;
            _libraryRootFolderManager = libraryRootFolderManager;
            _libraryOptionsManager = libraryOptionsManager;
            _itemQueryService = itemQueryService;
        }

        /// <summary>
        /// Runs the specified progress.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var collectionNameMoviesMap = new Dictionary<string, HashSet<Guid>>();

            foreach (var library in _libraryRootFolderManager.GetRootFolder().Children)
            {
                if (!_libraryOptionsManager.GetLibraryOptions(library).AutomaticallyAddToCollection)
                {
                    continue;
                }

                var startIndex = 0;
                var pagesize = 1000;

                while (true)
                {
                    var movies = _itemQueryService.GetItemList(new InternalItemsQuery
                    {
                        MediaTypes = new string[] { MediaType.Video },
                        IncludeItemTypes = new[] { BaseItemKind.Movie },
                        IsVirtualItem = false,
                        OrderBy = new[] { (ItemSortBy.SortName, SortOrder.Ascending) },
                        Parent = library,
                        StartIndex = startIndex,
                        Limit = pagesize,
                        Recursive = true
                    });

                    foreach (var m in movies)
                    {
                        if (m is Movie movie && !string.IsNullOrEmpty(movie.CollectionName))
                        {
                            if (collectionNameMoviesMap.TryGetValue(movie.CollectionName, out var movieList))
                            {
                                movieList.Add(movie.Id);
                            }
                            else
                            {
                                collectionNameMoviesMap[movie.CollectionName] = new HashSet<Guid> { movie.Id };
                            }
                        }
                    }

                    if (movies.Count < pagesize)
                    {
                        break;
                    }

                    startIndex += pagesize;
                }
            }

            var numComplete = 0;
            var count = collectionNameMoviesMap.Count;

            if (count == 0)
            {
                progress.Report(100);
                return;
            }

            var boxSets = _itemQueryService.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.BoxSet },
                CollapseBoxSetItems = false,
                Recursive = true
            });

            foreach (var (collectionName, movieIds) in collectionNameMoviesMap)
            {
                try
                {
                    var boxSet = boxSets.FirstOrDefault(b => b?.Name == collectionName) as BoxSet;
                    if (boxSet is null)
                    {
                        // won't automatically create collection if only one movie in it
                        if (movieIds.Count >= 2)
                        {
                            boxSet = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
                            {
                                Name = collectionName,
                                IsLocked = true
                            });

                            await _collectionManager.AddToCollectionAsync(boxSet.Id, movieIds);
                        }
                    }
                    else
                    {
                        await _collectionManager.AddToCollectionAsync(boxSet.Id, movieIds);
                    }

                    numComplete++;
                    double percent = numComplete;
                    percent /= count;
                    percent *= 100;

                    progress.Report(percent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing {CollectionName} with {@MovieIds}", collectionName, movieIds);
                }
            }

            progress.Report(100);
        }
    }
}
