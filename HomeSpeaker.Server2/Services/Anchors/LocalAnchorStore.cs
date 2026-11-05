using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared.Anchors;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>Anchors kept in this device's database, with an external feed (if one is configured) merged in.</summary>
public sealed class LocalAnchorStore : IAnchorStore
{
    /// <summary>The longest range a board covers, in days.</summary>
    public const int MaxRangeDays = 92;

    private readonly MusicContext db;
    private readonly TimeProvider timeProvider;
    private readonly AnchorChangeNotifier notifier;
    private readonly HttpAnchorFeed? feed;
    private readonly AnchorFeedSignal? feedSignal;

    public LocalAnchorStore(MusicContext db, TimeProvider timeProvider, AnchorChangeNotifier notifier, HttpAnchorFeed? feed = null, AnchorFeedSignal? feedSignal = null)
    {
        this.db = db;
        this.timeProvider = timeProvider;
        this.notifier = notifier;
        this.feed = feed;
        this.feedSignal = feedSignal;
    }

    private DateOnly Today => AnchorClock.Today(timeProvider);

    public async Task<AnchorBoardDto> GetBoardAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        var (start, end) = Range(from, to, Today);
        var (people, feedStatus) = await loadAsync(cancellationToken);
        return new AnchorBoardDto(Today, start, end, [.. people.Select(p => AnchorBoardBuilder.Person(p, start, end, Today))], feedStatus);
    }

    public async Task<AnchorPersonDto?> GetDayAsync(string personId, DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var day = date ?? Today;
        var (people, _) = await loadAsync(cancellationToken);
        var person = people.FirstOrDefault(p => string.Equals(p.Id, personId, StringComparison.Ordinal));
        return person is null ? null : AnchorBoardBuilder.Person(person, day, day, Today);
    }

    /// <summary>Everyone here, with the feed's anchors merged in when a feed is configured and has answered.</summary>
    private async Task<(IReadOnlyList<SourcePerson> People, AnchorFeedStatusDto? Feed)> loadAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SourcePerson> local = [.. (await loadPeopleAsync(null, cancellationToken)).Select(AnchorBoardBuilder.FromEntity)];
        if (feed is not { IsConfigured: true } || await feed.GetAsync(Today, cancellationToken) is not { } snapshot)
        {
            return (local, null);
        }

        var status = snapshot.Status with { People = snapshot.Payload?.People.Select(p => new AnchorFeedPersonDto(p.Id, p.Name)).ToList() };
        if (snapshot.Payload is not { } payload)
        {
            return (local, status);
        }

        var pending = await AnchorFeedOutbox.PendingAsync(db, cancellationToken);
        return (FeedAnchorMerge.Merge(local, payload, feed.Name, pending), status);
    }

    /// <summary>The board's range: defaults to today, never longer than <see cref="MaxRangeDays"/> (the start moves up).</summary>
    public static (DateOnly From, DateOnly To) Range(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var end = to ?? today;
        var start = from ?? end;
        if (start > end)
        {
            (start, end) = (end, start);
        }

        var earliest = end.AddDays(-(MaxRangeDays - 1));
        return (start < earliest ? earliest : start, end);
    }

    public async Task<AnchorOutcome<AnchorPersonDto>> CreatePersonAsync(AnchorPersonRequest request, CancellationToken cancellationToken = default)
    {
        if (!AnchorInput.TryName(request.Name, out var name))
        {
            return new(AnchorResult.Invalid, Message: $"A name needs 1 to {AnchorInput.MaxNameLength} characters.");
        }

        if (request.Color is not null && !AnchorColors.IsValid(request.Color))
        {
            return new(AnchorResult.Invalid, Message: "Unknown colour.");
        }

        if (!validEmoji(request.Emoji))
        {
            return new(AnchorResult.Invalid, Message: "That emoji is too long.");
        }

        var count = await db.AnchorPeople.CountAsync(cancellationToken);
        var usedColors = await db.AnchorPeople.Where(p => p.IsActive).Select(p => p.Color).ToListAsync(cancellationToken);
        var person = new AnchorPersonEntity
        {
            Name = name,
            Color = request.Color ?? AnchorColors.Palette.FirstOrDefault(c => !usedColors.Contains(c)) ?? AnchorColors.ForIndex(count),
            Emoji = string.IsNullOrWhiteSpace(request.Emoji) ? null : request.Emoji.Trim(),
            SortOrder = count == 0 ? 0 : await db.AnchorPeople.MaxAsync(p => p.SortOrder, cancellationToken) + 1,
            CreatedAt = timeProvider.GetUtcNow(),
        };
        db.AnchorPeople.Add(person);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        notifier.NotifyChanged();
        return new(AnchorResult.Ok, AnchorBoardBuilder.Person(person, Today, Today, Today));
    }

    public async Task<AnchorResult> UpdatePersonAsync(string personId, AnchorPersonRequest request, CancellationToken cancellationToken = default)
    {
        if (isFeedId(personId))
        {
            return AnchorResult.ReadOnly;
        }

        var person = await findPersonAsync(personId, cancellationToken);
        if (person is null)
        {
            return AnchorResult.NotFound;
        }

        if (!AnchorInput.TryName(request.Name, out var name) || (request.Color is not null && !AnchorColors.IsValid(request.Color)) || !validEmoji(request.Emoji))
        {
            return AnchorResult.Invalid;
        }

        person.Name = name;
        person.Color = request.Color ?? person.Color;
        if (request.Emoji is not null)
        {
            person.Emoji = string.IsNullOrWhiteSpace(request.Emoji) ? null : request.Emoji.Trim();
        }

        if (request.FeedPersonId is { } link)
        {
            person.FeedPersonId = string.IsNullOrWhiteSpace(link) ? null : link.Trim();
            if (person.FeedPersonId is { } linked)
            {
                // A feed person's anchors show with one person only.
                foreach (var other in await db.AnchorPeople.Where(p => p.FeedPersonId == linked && p.Id != person.Id).ToListAsync(cancellationToken))
                {
                    other.FeedPersonId = null;
                }
            }
        }

        return await saveAsync(cancellationToken);
    }

    public async Task<AnchorResult> OrderPeopleAsync(AnchorOrderRequest request, CancellationToken cancellationToken = default)
    {
        var people = await db.AnchorPeople.ToListAsync(cancellationToken);
        applyOrder(people, request.Ids, p => AnchorInput.Id(p.Id), p => p.SortOrder, (p, order) => p.SortOrder = order);
        return await saveAsync(cancellationToken);
    }

    public async Task<AnchorResult> ArchivePersonAsync(string personId, CancellationToken cancellationToken = default)
    {
        if (isFeedId(personId))
        {
            return AnchorResult.ReadOnly;
        }

        var person = await findPersonAsync(personId, cancellationToken);
        if (person is null)
        {
            return AnchorResult.NotFound;
        }

        person.IsActive = false;
        return await saveAsync(cancellationToken);
    }

    public async Task<AnchorOutcome<AnchorDto>> CreateAnchorAsync(string personId, AnchorRequest request, CancellationToken cancellationToken = default)
    {
        if (isFeedId(personId))
        {
            return new(AnchorResult.ReadOnly, Message: "Add this person's anchors in the app they come from.");
        }

        var person = await findPersonAsync(personId, cancellationToken);
        if (person is null)
        {
            return new(AnchorResult.NotFound);
        }

        if (!AnchorInput.TryTitle(request.Title, out var title))
        {
            return new(AnchorResult.Invalid, Message: $"A title needs 1 to {AnchorInput.MaxTitleLength} characters.");
        }

        var slot = AnchorSlot.Day;
        if (request.Slot is not null && !AnchorInput.TrySlot(request.Slot, out slot))
        {
            return new(AnchorResult.Invalid, Message: "Slot must be morning, day or evening.");
        }

        if (!AnchorInput.TryTargetTime(request.TargetTime, out var target))
        {
            return new(AnchorResult.Invalid, Message: "Target time must look like 06:30.");
        }

        var siblings = await db.Anchors.Where(a => a.PersonId == person.Id).Select(a => a.SortOrder).ToListAsync(cancellationToken);
        var anchor = new AnchorEntity
        {
            PersonId = person.Id,
            Title = title,
            Slot = slot,
            TargetTime = target,
            SortOrder = siblings.Count == 0 ? 0 : siblings.Max() + 1,
            CreatedAt = timeProvider.GetUtcNow(),
            Periods = [.. AnchorPeriods.Start(Today, DayMask.Normalize(request.Days)).Select(toEntity)],
        };
        db.Anchors.Add(anchor);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        notifier.NotifyChanged();

        var dto = AnchorBoardBuilder.Person(new AnchorPersonEntity { Id = person.Id, Name = person.Name, Color = person.Color, Anchors = [anchor] }, Today, Today, Today).Anchors[0];
        return new(AnchorResult.Ok, dto);
    }

    public async Task<AnchorResult> UpdateAnchorAsync(string anchorId, AnchorRequest request, CancellationToken cancellationToken = default)
    {
        if (isFeedId(anchorId))
        {
            return AnchorResult.ReadOnly;
        }

        var anchor = await findAnchorAsync(anchorId, cancellationToken);
        if (anchor is null)
        {
            return AnchorResult.NotFound;
        }

        var slot = anchor.Slot;
        if (!AnchorInput.TryTitle(request.Title, out var title)
            || (request.Slot is not null && !AnchorInput.TrySlot(request.Slot, out slot))
            || !AnchorInput.TryTargetTime(request.TargetTime, out var target))
        {
            return AnchorResult.Invalid;
        }

        anchor.Title = title;
        anchor.Slot = slot;
        if (request.TargetTime is not null)
        {
            anchor.TargetTime = target;
        }

        if (request.Days is { } days)
        {
            replacePeriods(anchor, AnchorPeriods.ChangeDays(periodsOf(anchor), days, Today));
        }

        return await saveAsync(cancellationToken);
    }

    public async Task<AnchorResult> OrderAnchorsAsync(string personId, AnchorOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (isFeedId(personId))
        {
            return AnchorResult.ReadOnly;
        }

        var person = await findPersonAsync(personId, cancellationToken);
        if (person is null)
        {
            return AnchorResult.NotFound;
        }

        var anchors = await db.Anchors.Where(a => a.PersonId == person.Id).ToListAsync(cancellationToken);
        applyOrder(anchors, request.Ids, a => AnchorInput.Id(a.Id), a => a.SortOrder, (a, order) => a.SortOrder = order);
        return await saveAsync(cancellationToken);
    }

    public Task<AnchorResult> RetireAnchorAsync(string anchorId, CancellationToken cancellationToken = default) =>
        editPeriodsAsync(anchorId, periods => AnchorPeriods.Retire(periods, Today), cancellationToken);

    public Task<AnchorResult> RestoreAnchorAsync(string anchorId, CancellationToken cancellationToken = default) =>
        editPeriodsAsync(anchorId, periods => AnchorPeriods.Restore(periods, Today), cancellationToken);

    public async Task<AnchorResult> SetDoneAsync(string anchorId, DateOnly date, AnchorLogRequest request, CancellationToken cancellationToken = default)
    {
        if (FeedAnchorMerge.TryFeedId(anchorId, out var feedAnchorId))
        {
            return await setFeedDoneAsync(feedAnchorId, date, request, cancellationToken);
        }

        if (!AnchorInput.TryLocalId(anchorId, out var id) || !await db.Anchors.AnyAsync(a => a.Id == id, cancellationToken))
        {
            return AnchorResult.NotFound;
        }

        if (date > Today.AddDays(1))
        {
            return AnchorResult.Invalid;
        }

        var now = timeProvider.GetUtcNow();
        var clientUpdatedAt = request.ClientUpdatedAt ?? now;
        var log = await db.AnchorLogs.FirstOrDefaultAsync(l => l.AnchorId == id && l.Date == date, cancellationToken);
        if (log is null)
        {
            log = new AnchorLogEntity { AnchorId = id, Date = date };
            db.AnchorLogs.Add(log);
        }
        else if (log.ClientUpdatedAt > clientUpdatedAt)
        {
            // A newer change already landed (e.g. a queued tick replayed late): keep it.
            return AnchorResult.Ok;
        }

        log.DoneAt = request.Done ? (log.Done ? log.DoneAt : now) : null;
        log.Done = request.Done;
        log.ClientUpdatedAt = clientUpdatedAt;
        log.UpdatedAt = now;
        return await saveAsync(cancellationToken);
    }

    /// <summary>A tick on a feed's anchor: queued for the feed (shown as pending until it lands), never stored here.</summary>
    private async Task<AnchorResult> setFeedDoneAsync(string feedAnchorId, DateOnly date, AnchorLogRequest request, CancellationToken cancellationToken)
    {
        if (feed is not { IsConfigured: true })
        {
            return AnchorResult.NotFound;
        }

        if (date > Today.AddDays(1))
        {
            return AnchorResult.Invalid;
        }

        var snapshot = await feed.GetAsync(Today, cancellationToken);
        if (snapshot?.Payload is not { } payload)
        {
            return AnchorResult.Unavailable;
        }

        var anchor = payload.Anchors.FirstOrDefault(a => string.Equals(a.Id, feedAnchorId, StringComparison.Ordinal));
        if (anchor is null)
        {
            return AnchorResult.NotFound;
        }

        if (!anchor.Writable)
        {
            return AnchorResult.ReadOnly;
        }

        await AnchorFeedOutbox.EnqueueAsync(db, feedAnchorId, date, request.Done, request.ClientUpdatedAt ?? timeProvider.GetUtcNow(), timeProvider, cancellationToken);
        db.ChangeTracker.Clear();
        feedSignal?.Wake();
        notifier.NotifyChanged();
        return AnchorResult.Ok;
    }

    private static bool isFeedId(string id) => FeedAnchorMerge.TryFeedId(id, out _);

    private async Task<AnchorResult> editPeriodsAsync(string anchorId, Func<IReadOnlyList<AnchorPeriod>, IReadOnlyList<AnchorPeriod>> edit, CancellationToken cancellationToken)
    {
        if (isFeedId(anchorId))
        {
            return AnchorResult.ReadOnly;
        }

        var anchor = await findAnchorAsync(anchorId, cancellationToken);
        if (anchor is null)
        {
            return AnchorResult.NotFound;
        }

        replacePeriods(anchor, edit(periodsOf(anchor)));
        return await saveAsync(cancellationToken);
    }

    private void replacePeriods(AnchorEntity anchor, IReadOnlyList<AnchorPeriod> periods)
    {
        db.AnchorPeriods.RemoveRange(anchor.Periods);
        anchor.Periods = [.. periods.Select(toEntity)];
    }

    private static AnchorPeriodEntity toEntity(AnchorPeriod period) => new() { From = period.From, To = period.To, Days = period.Days };

    private static IReadOnlyList<AnchorPeriod> periodsOf(AnchorEntity anchor) =>
        [.. anchor.Periods.OrderBy(p => p.From).Select(p => new AnchorPeriod(p.From, p.To, p.Days))];

    private static bool validEmoji(string? emoji) => emoji is null || emoji.Trim().Length <= AnchorInput.MaxEmojiLength;

    private static void applyOrder<T>(List<T> items, IReadOnlyList<string> ids, Func<T, string> idOf, Func<T, int> currentOrder, Action<T, int> setOrder)
    {
        var ordered = items
            .OrderBy(item => indexOf(ids, idOf(item)) is var index and >= 0 ? index : ids.Count)
            .ThenBy(currentOrder)
            .ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            setOrder(ordered[i], i);
        }
    }

    private static int indexOf(IReadOnlyList<string> ids, string id)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            if (string.Equals(ids[i], id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private async Task<AnchorResult> saveAsync(CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);

        // A page keeps its DbContext for as long as it is open, and other pages change anchors meanwhile; forgetting
        // what this one loaded makes its next edit read the current rows instead of its own stale copies.
        db.ChangeTracker.Clear();
        notifier.NotifyChanged();
        return AnchorResult.Ok;
    }

    private async Task<AnchorPersonEntity?> findPersonAsync(string personId, CancellationToken cancellationToken)
    {
        // Start every edit from the database, not from rows an earlier, rejected edit left tracked.
        db.ChangeTracker.Clear();
        return AnchorInput.TryLocalId(personId, out var id)
            ? await db.AnchorPeople.FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken)
            : null;
    }

    private async Task<AnchorEntity?> findAnchorAsync(string anchorId, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        return AnchorInput.TryLocalId(anchorId, out var id)
            ? await db.Anchors.Include(a => a.Periods).FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            : null;
    }

    private async Task<List<AnchorPersonEntity>> loadPeopleAsync(int? onlyId, CancellationToken cancellationToken)
    {
        var since = Today.AddDays(-AnchorBoardBuilder.StreakLookbackDays);
        var query = db.AnchorPeople
            .AsNoTracking()
            .Where(p => p.IsActive && (onlyId == null || p.Id == onlyId))
            .Include(p => p.Anchors).ThenInclude(a => a.Periods)
            .Include(p => p.Anchors).ThenInclude(a => a.Logs.Where(l => l.Date >= since))
            .AsSplitQuery();
        var people = await query.ToListAsync(cancellationToken);
        return [.. people.OrderBy(p => p.SortOrder).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)];
    }
}
