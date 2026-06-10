using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Huddle.Models;

namespace Huddle.Storage;

internal static class MomentStore
{
    public static async Task AddAsync(Moment moment)
    {
        await using var connection = await Database.OpenAsync().ConfigureAwait(false);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                INSERT INTO moments (id, ts, app, window_title, summary)
                VALUES ($id, $ts, $app, $title, $summary);";
            command.Parameters.AddWithValue("$id", moment.Id);
            command.Parameters.AddWithValue("$ts", moment.Ts.ToUniversalTime().ToString("o"));
            command.Parameters.AddWithValue("$app", moment.App);
            command.Parameters.AddWithValue("$title", moment.WindowTitle);
            command.Parameters.AddWithValue("$summary", moment.Summary);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // Move the new row from WAL into the main .db file immediately. At one
        // write per 3 min the cost is unmeasurable; the WAL never holds anything
        // we can't afford to lose if the app is force-killed before a checkpoint.
        using (var checkpoint = connection.CreateCommand())
        {
            checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await checkpoint.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public static async Task<IReadOnlyList<Moment>> RecentAsync(int limit)
    {
        var list = new List<Moment>(limit);
        await using var connection = await Database.OpenAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, ts, app, window_title, summary
              FROM moments
             ORDER BY ts DESC
             LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            list.Add(new Moment(
                Id: reader.GetString(0),
                Ts: DateTimeOffset.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture),
                App: reader.GetString(2),
                WindowTitle: reader.GetString(3),
                Summary: reader.GetString(4)));
        }
        return list;
    }

    public static async Task<int> CountAsync()
    {
        await using var connection = await Database.OpenAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM moments;";
        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
