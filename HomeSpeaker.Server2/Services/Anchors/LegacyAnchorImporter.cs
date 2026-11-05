using System.Data.Common;
using System.Globalization;
using HomeSpeaker.Server2.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>
/// One-time move from the old anchor tables (AnchorDefinitions, UserAnchors, DailyAnchors) into people, anchors, periods
/// and logs. Runs after migrations at startup; once it has dropped the old tables it does nothing.
/// </summary>
public static class LegacyAnchorImporter
{
    /// <summary>Returns how many people it created.</summary>
    public static async Task<int> ImportAsync(MusicContext db, DateOnly today, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        if (!await tableExistsAsync(connection, "DailyAnchors", cancellationToken))
        {
            return 0;
        }

        var assignments = await readAssignmentsAsync(connection, cancellationToken);
        var rows = await readDailyRowsAsync(connection, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var created = 0;
        if (!await db.AnchorPeople.AnyAsync(cancellationToken))
        {
            created = await insertAsync(db, LegacyAnchorPlan.Plan(assignments, rows, today), cancellationToken);
        }

        await db.Database.ExecuteSqlRawAsync(
            "DROP TABLE IF EXISTS DailyAnchors; DROP TABLE IF EXISTS UserAnchors; DROP TABLE IF EXISTS AnchorDefinitions;",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    private static async Task<int> insertAsync(MusicContext db, IReadOnlyList<PlannedPerson> plan, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        for (var p = 0; p < plan.Count; p++)
        {
            var person = new AnchorPersonEntity
            {
                Name = plan[p].Name,
                Color = AnchorColors.ForIndex(p),
                SortOrder = p,
                CreatedAt = now,
            };
            for (var a = 0; a < plan[p].Anchors.Count; a++)
            {
                var planned = plan[p].Anchors[a];
                person.Anchors.Add(new AnchorEntity
                {
                    Title = planned.Title,
                    Slot = AnchorSlot.Day,
                    SortOrder = a,
                    CreatedAt = now,
                    Periods = [.. planned.Periods.Select(period => new AnchorPeriodEntity { From = period.From, To = period.To, Days = period.Days })],
                    Logs = [.. planned.Completed.Select(c => new AnchorLogEntity
                    {
                        Date = c.Date,
                        Done = true,
                        DoneAt = c.CompletedAt is { } at ? new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)) : null,
                        UpdatedAt = now,
                    })],
                });
            }

            db.AnchorPeople.Add(person);
        }

        await db.SaveChangesAsync(cancellationToken);
        return plan.Count;
    }

    private static async Task<bool> tableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = table;
        command.Parameters.Add(parameter);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<List<LegacyAssignment>> readAssignmentsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var result = new List<LegacyAssignment>();
        if (!await tableExistsAsync(connection, "UserAnchors", cancellationToken) || !await tableExistsAsync(connection, "AnchorDefinitions", cancellationToken))
        {
            return result;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ua.UserId, ua.AnchorDefinitionId, ad.Name, ad.IsActive, ua.CreatedAt
            FROM UserAnchors ua JOIN AnchorDefinitions ad ON ad.Id = ua.AnchorDefinitionId
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new LegacyAssignment(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                parseDateTime(reader.GetString(4)) ?? DateTime.UtcNow));
        }

        return result;
    }

    private static async Task<List<LegacyAnchorRow>> readDailyRowsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var result = new List<LegacyAnchorRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT UserId, AnchorDefinitionId, AnchorName, Date, IsCompleted, CompletedAt FROM DailyAnchors";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!DateOnly.TryParse(reader.GetString(3), CultureInfo.InvariantCulture, out var date))
            {
                continue;
            }

            result.Add(new LegacyAnchorRow(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2),
                date,
                reader.GetBoolean(4),
                await reader.IsDBNullAsync(5, cancellationToken) ? null : parseDateTime(reader.GetString(5))));
        }

        return result;
    }

    private static DateTime? parseDateTime(string text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var value) ? value : null;
}
