using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Huddle.Models;

namespace Huddle.Storage;

public static class NudgeStore
{
    public static async Task AddAsync(Nudge nudge)
    {
        await using var connection = await Database.OpenAsync().ConfigureAwait(false);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                INSERT INTO nudges (id, ts, scenario, title, body, sources, is_starred)
                VALUES ($id, $ts, $scenario, $title, $body, $sources, $starred);";
            command.Parameters.AddWithValue("$id", nudge.Id);
            command.Parameters.AddWithValue("$ts", nudge.Ts.ToUniversalTime().ToString("o"));
            command.Parameters.AddWithValue("$scenario", nudge.Scenario);
            command.Parameters.AddWithValue("$title", nudge.Title);
            command.Parameters.AddWithValue("$body", nudge.Body);
            command.Parameters.AddWithValue("$sources",
                nudge.Sources.Count == 0 ? (object)DBNull.Value : JsonSerializer.Serialize(nudge.Sources));
            command.Parameters.AddWithValue("$starred", nudge.IsStarred ? 1 : 0);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        using (var checkpoint = connection.CreateCommand())
        {
            checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await checkpoint.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public static async Task SetStarredAsync(string id, bool starred)
    {
        await using var connection = await Database.OpenAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE nudges SET is_starred = $starred WHERE id = $id;";
        command.Parameters.AddWithValue("$starred", starred ? 1 : 0);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<Nudge>> RecentAsync(int limit)
    {
        var list = new List<Nudge>(limit);
        await using var connection = await Database.OpenAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, ts, scenario, title, body, sources, is_starred
              FROM nudges
             ORDER BY ts DESC
             LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(Read(reader));
        }
        return list;
    }

    /// <summary>All nudges emitted at or after <paramref name="cutoff"/>, newest first.</summary>
    public static async Task<IReadOnlyList<Nudge>> SinceAsync(DateTimeOffset cutoff)
    {
        var list = new List<Nudge>();
        await using var connection = await Database.OpenAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, ts, scenario, title, body, sources, is_starred
              FROM nudges
             WHERE ts >= $cutoff
             ORDER BY ts DESC;";
        command.Parameters.AddWithValue("$cutoff", cutoff.ToUniversalTime().ToString("o"));
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(Read(reader));
        }
        return list;
    }

    /// <summary>
    /// Read-only. Nudges emitted at or after <paramref name="cutoff"/>, optionally
    /// filtered to one scenario key, newest first, capped at <paramref name="limit"/>.
    /// </summary>
    public static async Task<IReadOnlyList<Nudge>> SinceByScenarioAsync(string? scenario, DateTimeOffset cutoff, int limit)
    {
        var list = new List<Nudge>();
        await using var connection = await Database.OpenReadOnlyAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, ts, scenario, title, body, sources, is_starred
              FROM nudges
             WHERE ts >= $cutoff" + (scenario is null ? "" : " AND scenario = $scenario") + @"
             ORDER BY ts DESC
             LIMIT $limit;";
        command.Parameters.AddWithValue("$cutoff", cutoff.ToUniversalTime().ToString("o"));
        if (scenario is not null) command.Parameters.AddWithValue("$scenario", scenario);
        command.Parameters.AddWithValue("$limit", limit);
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false)) list.Add(Read(reader));
        return list;
    }

    /// <summary>Read-only. Nudges with ts in [startUtc, endUtc), newest first.</summary>
    public static async Task<IReadOnlyList<Nudge>> BetweenAsync(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var list = new List<Nudge>();
        await using var connection = await Database.OpenReadOnlyAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, ts, scenario, title, body, sources, is_starred
              FROM nudges
             WHERE ts >= $start AND ts < $end
             ORDER BY ts DESC;";
        command.Parameters.AddWithValue("$start", startUtc.ToUniversalTime().ToString("o"));
        command.Parameters.AddWithValue("$end", endUtc.ToUniversalTime().ToString("o"));
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false)) list.Add(Read(reader));
        return list;
    }

    public static async Task<IReadOnlyList<Nudge>> RecentByScenarioAsync(string scenario, int limit)
    {
        var list = new List<Nudge>(limit);
        await using var connection = await Database.OpenAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, ts, scenario, title, body, sources, is_starred
              FROM nudges
             WHERE scenario = $scenario
             ORDER BY ts DESC
             LIMIT $limit;";
        command.Parameters.AddWithValue("$scenario", scenario);
        command.Parameters.AddWithValue("$limit", limit);
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(Read(reader));
        }
        return list;
    }

    public static async Task<int> CountAsync()
    {
        await using var connection = await Database.OpenAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM nudges;";
        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Nudge Read(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        string? sourcesJson = reader.IsDBNull(5) ? null : reader.GetString(5);
        IReadOnlyList<string> sources = sourcesJson is null
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<List<string>>(sourcesJson) ?? new List<string>();

        return new Nudge(
            Id: reader.GetString(0),
            Ts: DateTimeOffset.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture),
            Scenario: reader.GetString(2),
            Title: reader.GetString(3),
            Body: reader.GetString(4),
            Sources: sources)
        {
            IsStarred = reader.GetInt32(6) != 0,
        };
    }
}
