#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CacheManager.Core.Logging;
using EasyCaching.Core.Diagnostics;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Entities.Libraries;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Library;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using TMDbLib.Objects.Changes;
using Person = MediaBrowser.Controller.Entities.Person;

namespace Emby.Server.Implementations.Library
{
    /// <summary>
    /// Service wrapper ItemRepository.
    /// </summary>
    public class ItemService : IItemService
    {
        private readonly ILibraryItemIdGenerator _libraryItemIdGenerator;
        private readonly IItemRepository _itemRepository;
        private readonly ILogger<ItemService> _logger;
        private readonly ConcurrentDictionary<Guid, BaseItem> _cache = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemService"/> class.
        /// </summary>
        /// <param name="libraryItemIdGenerator">The instance of <see cref="ILibraryItemIdGenerator"/> interface.</param>
        /// <param name="itemRepository">The instance of <see cref="IItemRepository"/> interface.</param>
        /// <param name="logger">The logger.</param>
        public ItemService(
            ILibraryItemIdGenerator libraryItemIdGenerator,
            IItemRepository itemRepository,
            ILogger<ItemService> logger)
        {
            _libraryItemIdGenerator = libraryItemIdGenerator;
            _itemRepository = itemRepository;
            _logger = logger;
        }

        /// <summary>
        /// Occurs when [item added].
        /// </summary>
        public event EventHandler<ItemChangeEventArgs> ItemAdded;

        /// <summary>
        /// Occurs when [item updated].
        /// </summary>
        public event EventHandler<ItemChangeEventArgs> ItemUpdated;

        /// <summary>
        /// Occurs when [item removed].
        /// </summary>
        public event EventHandler<ItemRemovedEventArgs> ItemRemoved;

        /// <summary>
        /// Retrieves the item.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>BaseItem.</returns>
        public BaseItem RetrieveItem(Guid id)
        {
            return _itemRepository.RetrieveItem(id);
        }

        /// <inheritdoc/>
        public T CreateItemByName<T>(Func<string, string> getPathFn, string name, DtoOptions options)
            where T : BaseItem, new()
        {
            if (typeof(T) == typeof(MusicArtist))
            {
                // TODO: Dangles: Need to do something with this.
                var existing = _itemRepository.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.MusicArtist },
                    Name = name,
                    DtoOptions = options
                }).Cast<MusicArtist>()
                .OrderBy(i => i.IsAccessedByName ? 1 : 0)
                .Cast<T>()
                .FirstOrDefault();

                if (existing is not null)
                {
                    return existing;
                }
            }

            var path = getPathFn(name);
            var id = GetItemIdFromPath<T>(path);
            var item = GetItemById(id) as T;
            if (item is null)
            {
                item = new T
                {
                    Name = name,
                    Id = id,
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow,
                    Path = path
                };

                CreateItem(item, null!);
            }

            return item;
        }

        /// <inheritdoc/>
        public Guid GetItemIdFromPath<T>(string path)
              where T : BaseItem, new()
        {
            return _libraryItemIdGenerator.Generate(path, typeof(T));
        }

        /// <summary>
        /// Gets the item by id.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>BaseItem.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
        public BaseItem GetItemById(Guid id)
        {
            if (id.Equals(Guid.Empty))
            {
                throw new ArgumentException("Guid can't be empty", nameof(id));
            }

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            if (_cache.TryGetValue(id, out BaseItem item))
            {
                return item;
            }
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

            item = RetrieveItem(id);

            if (item is not null)
            {
                RegisterItemInCache(item);
            }

            return item;
        }

        /// <inheritdoc/>
        public void DeleteItem(BaseItem item, DeleteOptions deleteOptions)
        {
            if (_itemRepository.RetrieveItem(item.Id) is null)
            {
                return;
            }

            _itemRepository.DeleteItem(item.Id);
            OnItemDeleted(item, item.GetParent(), deleteOptions);
        }

        /// <inheritdoc/>
        public void DeleteItem(BaseItem item, BaseItem parent, DeleteOptions deleteOptions)
        {
            if (_itemRepository.RetrieveItem(item.Id) is null)
            {
                return;
            }

            _itemRepository.DeleteItem(item.Id);
            OnItemDeleted(item, parent, deleteOptions);
        }

        /// <inheritdoc/>
        public void DeleteItem(BaseItem item)
        {
            _itemRepository.DeleteItem(item.Id);
            OnItemDeleted(item, item.GetParent(), null);
        }

        private void OnItemDeleted(BaseItem item, BaseItem parent, DeleteOptions deleteOptions)
        {
            ItemRemoved?.Invoke(this, new ItemRemovedEventArgs(deleteOptions ?? new())
                {
                    Item = item,
                    Parent = parent
                });
        }

        /// <inheritdoc/>
        public Studio GetStudio(string name)
        {
            return CreateItemByName<Studio>(Studio.GetPath, name, new DtoOptions(true));
        }

        /// <inheritdoc/>
        public void UpdatePeople(Guid itemId, List<PersonInfo> people)
        {
            _itemRepository.UpdatePeople(itemId, people);
        }

        /// <inheritdoc/>
        public void RegisterItemInCache(BaseItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (item is IItemByName)
            {
                if (item is not MusicArtist)
                {
                    return;
                }
            }
            else if (!item.IsFolder && item is not Video && item is not LiveTvChannel)
            {
                return;
            }

            _cache[item.Id] = item;
        }

        /// <summary>
        /// Gets a Genre.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{Genre}.</returns>
        public MusicArtist GetArtist(string name)
        {
            return GetArtist(name, new DtoOptions(true));
        }

        /// <inheritdoc/>
        public MusicArtist GetArtist(string name, DtoOptions options)
        {
            return CreateItemByName<MusicArtist>(MusicArtist.GetPath, name, options);
        }

        /// <inheritdoc/>
        public List<string> GetAllArtistNames()
        {
            return _itemRepository.GetAllArtistNames();
        }

        /// <inheritdoc/>
        public List<string> GetStudioNames()
        {
            return _itemRepository.GetStudioNames();
        }

        /// <inheritdoc/>
        public List<ChapterInfo> GetChapters(BaseItem item)
        {
            return _itemRepository.GetChapters(item);
        }

        /// <inheritdoc/>
        public ChapterInfo GetChapter(BaseItem item, int index)
        {
            return _itemRepository.GetChapter(item, index);
        }

        /// <inheritdoc/>
        public List<PersonInfo> GetPeople(InternalPeopleQuery query)
        {
            return _itemRepository.GetPeople(query);
        }

        /// <inheritdoc/>
        public List<PersonInfo> GetPeople(BaseItem item)
        {
            if (item.SupportsPeople)
            {
                var people = GetPeople(new InternalPeopleQuery
                {
                    ItemId = item.Id
                });

                if (people.Count > 0)
                {
                    return people;
                }
            }

            return new List<PersonInfo>();
        }

        /// <inheritdoc/>
        public List<string> GetPeopleNames(InternalPeopleQuery query)
        {
            return _itemRepository.GetPeopleNames(query);
        }

        /// <inheritdoc/>
        public void SaveItems(IReadOnlyList<BaseItem> items, CancellationToken cancellationToken)
        {
            if (items is null || !items.Any())
            {
                return;
            }

            _itemRepository.SaveItems(items, cancellationToken);
            foreach (var item in items)
            {
                RegisterItemInCache(item);

                // With the live tv guide this just creates too much noise
                if (item.SourceType == SourceType.Library)
                {
                    continue;
                }

                ItemAdded?.Invoke(
                    this,
                    new ItemChangeEventArgs
                    {
                        Item = item,
                        Parent = item.GetParent()
                    });
            }
        }

        /// <inheritdoc/>
        public Task UpdateItemAsync(BaseItem item, BaseItem parent, ItemUpdateType updateReason, CancellationToken cancellationToken)
        {
            UpdateItems(new BaseItem[] { item }, parent, updateReason, cancellationToken);
            // NOTE: Dangles: this is hacky but I'm staring to get lazy now.
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void UpdateItems(IReadOnlyList<BaseItem> items, BaseItem parent, ItemUpdateType updateReason, CancellationToken cancellationToken)
        {
            _itemRepository.SaveItems(items, cancellationToken);

            foreach (var item in items)
            {
                // With the live tv guide this just creates too much noise
                if (item.SourceType != SourceType.Library)
                {
                    continue;
                }

                ItemUpdated?.Invoke(
                    this,
                    new ItemChangeEventArgs
                    {
                        Item = item,
                        Parent = parent,
                        UpdateReason = updateReason
                    });
            }
        }

        /// <summary>
        /// Creates the item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="parent">The parent item.</param>
        public void CreateItem(BaseItem item, BaseItem parent)
        {
            if (parent is Folder parentFolder)
            {
                item.SetParent(parentFolder);
            }

            if (item.Id.Equals(Guid.Empty))
            {
                item.Id = _libraryItemIdGenerator.Generate(item.Path, item.GetType());
            }

            if (item.DateCreated == DateTime.MinValue)
            {
                item.DateCreated = DateTime.UtcNow;
            }

            if (item.DateModified == DateTime.MinValue)
            {
                item.DateModified = DateTime.UtcNow;
            }

            CreateItems(new[] { item }, parent, CancellationToken.None);
        }

        /// <summary>
        /// Creates the items.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="parent">The parent item.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public void CreateItems(IReadOnlyList<BaseItem> items, BaseItem parent, CancellationToken cancellationToken)
        {
            _itemRepository.SaveItems(items, cancellationToken);

            foreach (var item in items)
            {
                RegisterItemInCache(item);
            }

            if (ItemAdded is not null)
            {
                foreach (var item in items)
                {
                    // With the live tv guide this just creates too much noise
                    if (item.SourceType != SourceType.Library)
                    {
                        continue;
                    }

                    try
                    {
                        ItemAdded(
                            this,
                            new ItemChangeEventArgs
                            {
                                Item = item,
                                Parent = parent ?? item.GetParent()
                            });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in ItemAdded event handler");
                    }
                }
            }
        }

        /// <summary>
        /// Gets the person.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{Person}.</returns>
        public Person GetPerson(string name)
        {
            var path = Person.GetPath(name);
            var id = GetItemIdFromPath<Person>(path);
            if (GetItemById(id) is not Person item)
            {
                item = new Person
                {
                    Name = name,
                    Id = id,
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow,
                    Path = path
                };
            }

            return item;
        }

        /// <inheritdoc/>
        public void UpdateInheritedValues()
        {
            _itemRepository.UpdateInheritedValues();
        }

        /// <inheritdoc/>
        public void SaveImages(BaseItem item)
        {
            _itemRepository.SaveImages(item);
        }
    }
}
