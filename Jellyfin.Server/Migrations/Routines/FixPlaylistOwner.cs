using System;
using System.Linq;
using System.Threading;

using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Migrations.Routines;

/// <summary>
/// Properly set playlist owner.
/// </summary>
internal class FixPlaylistOwner : IMigrationRoutine
{
    private readonly ILogger<RemoveDuplicateExtras> _logger;
    private readonly IPlaylistManager _playlistManager;
    private readonly IItemQueryService _itemQueryService;

    public FixPlaylistOwner(
        ILogger<RemoveDuplicateExtras> logger,
        IPlaylistManager playlistManager,
        IItemQueryService itemQueryService)
    {
        _logger = logger;
        _playlistManager = playlistManager;
        _itemQueryService = itemQueryService;
    }

    /// <inheritdoc/>
    public Guid Id => Guid.Parse("{615DFA9E-2497-4DBB-A472-61938B752C5B}");

    /// <inheritdoc/>
    public string Name => "FixPlaylistOwner";

    /// <inheritdoc/>
    public bool PerformOnNewInstall => false;

    /// <inheritdoc/>
    public void Perform()
    {
        var playlists = _itemQueryService.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Playlist }
        })
        .Cast<Playlist>()
        .Where(x => x.OwnerUserId.Equals(Guid.Empty))
        .ToArray();

        if (playlists.Length > 0)
        {
            foreach (var playlist in playlists)
            {
                var shares = playlist.Shares;
                if (shares.Length > 0)
                {
                    var firstEditShare = shares.First(x => x.CanEdit);
                    if (firstEditShare is not null && Guid.TryParse(firstEditShare.UserId, out var guid))
                    {
                        playlist.OwnerUserId = guid;
                        playlist.Shares = shares.Where(x => x != firstEditShare).ToArray();
                        playlist.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).GetAwaiter().GetResult();
                        _playlistManager.SavePlaylistFile(playlist);
                    }
                }
                else
                {
                    playlist.OpenAccess = true;
                    playlist.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).GetAwaiter().GetResult();
                }
            }
        }
    }
}
