using HomeSpeaker.Server2.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>Remembers groups the admin hid from the review list, by <see cref="AlbumArtGroup.Key"/>. Uses its own DbContext per call so a long-lived page never shares one.</summary>
public sealed class AlbumArtDismissalStore
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly TimeProvider timeProvider;

    public AlbumArtDismissalStore(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
    {
        this.scopeFactory = scopeFactory;
        this.timeProvider = timeProvider;
    }

    public async Task<IReadOnlySet<string>> GetHiddenKeysAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicContext>();
        var keys = await db.AlbumArtDismissals.AsNoTracking().Select(d => d.GroupKey).ToListAsync(cancellationToken).ConfigureAwait(false);
        return keys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task HideAsync(AlbumArtGroup group, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicContext>();
        if (await db.AlbumArtDismissals.AnyAsync(d => d.GroupKey == group.Key, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        db.AlbumArtDismissals.Add(new AlbumArtDismissal
        {
            GroupKey = group.Key,
            Artist = group.Artist,
            Name = group.DisplayName,
            DismissedUtc = timeProvider.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UnhideAsync(string groupKey, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicContext>();
        await db.AlbumArtDismissals.Where(d => d.GroupKey == groupKey).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
