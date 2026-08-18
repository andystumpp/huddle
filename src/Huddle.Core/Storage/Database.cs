using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Huddle.Storage;

/// <summary>
/// Opens the Huddle SQLite database and applies pending migrations on startup.
/// </summary>
public static class Database
{
    private const string FileName = "huddle.db";
    private const string MigrationResourcePrefix = "Huddle.Storage.Migrations.";

    public static string DatabasePath
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Huddle");
            return Path.Combine(dir, FileName);
        }
    }

    private static string ConnectionString =>
        $"Data Source={DatabasePath};Pooling=True;Cache=Shared";

    private static string ReadOnlyConnectionString =>
        $"Data Source={DatabasePath};Mode=ReadOnly";

    /// <summary>
    /// Open a pooled read-write connection. Caller disposes.
    /// </summary>
    public static async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        return connection;
    }

    /// <summary>
    /// Open a read-only connection — for query-only callers such as the MCP
    /// server that must never write or migrate. Safe alongside the app (WAL).
    /// </summary>
    public static async Task<SqliteConnection> OpenReadOnlyAsync()
    {
        var connection = new SqliteConnection(ReadOnlyConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        return connection;
    }

    /// <summary>
    /// Idempotent: creates the directory, opens the database, sets pragmas,
    /// and applies any pending migrations.
    /// </summary>
    public static async Task InitializeAsync()
    {
        string? dir = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await using var connection = await OpenAsync().ConfigureAwait(false);

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
            await pragma.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        using (var ensureTable = connection.CreateCommand())
        {
            ensureTable.CommandText = "CREATE TABLE IF NOT EXISTS __migrations (name TEXT PRIMARY KEY, applied_at TEXT NOT NULL);";
            await ensureTable.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        var applied = new System.Collections.Generic.HashSet<string>();
        using (var query = connection.CreateCommand())
        {
            query.CommandText = "SELECT name FROM __migrations";
            using var reader = await query.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                applied.Add(reader.GetString(0));
            }
        }

        var assembly = typeof(Database).Assembly;
        var migrationNames = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(MigrationResourcePrefix, StringComparison.Ordinal)
                        && n.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal);

        foreach (var resourceName in migrationNames)
        {
            string shortName = resourceName.Substring(MigrationResourcePrefix.Length);
            if (applied.Contains(shortName)) continue;

            string sql = ReadResource(assembly, resourceName);
            await using var tx = await connection.BeginTransactionAsync().ConfigureAwait(false);

            using (var run = connection.CreateCommand())
            {
                run.Transaction = (SqliteTransaction)tx;
                run.CommandText = sql;
                await run.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            using (var record = connection.CreateCommand())
            {
                record.Transaction = (SqliteTransaction)tx;
                record.CommandText = "INSERT INTO __migrations (name, applied_at) VALUES ($name, $ts)";
                record.Parameters.AddWithValue("$name", shortName);
                record.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToString("o"));
                await record.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await tx.CommitAsync().ConfigureAwait(false);
        }
    }

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Migration resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
