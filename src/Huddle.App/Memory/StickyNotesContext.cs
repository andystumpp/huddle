using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using Huddle.Config;
using Huddle.Scenarios;

namespace Huddle.Memory;

/// <summary>
/// Opt-in dynamic context from the user's Windows Sticky Notes. When
/// <see cref="HuddleConfig.StickyNotesContext"/> is on, reads the packaged Sticky Notes
/// SQLite store read-only and returns a labelled block of the note text for injection into
/// scenario prompts. Every failure path (flag off, missing DB, lock, schema mismatch)
/// returns an empty string and never throws — same contract as <see cref="ProfileStore"/>.
/// Legacy pre-1607 <c>.snt</c> storage is not supported.
/// </summary>
internal static class StickyNotesContext
{
    private static string DbPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Packages", "Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe", "LocalState", "plum.sqlite");

    private const int MaxChars = 4000; // keep the injected block bounded

    /// <summary>The current notes as a labelled block, or an empty string.</summary>
    public static string Read()
    {
        if (!HuddleConfig.Current.StickyNotesContext) return string.Empty;

        try
        {
            string path = DbPath;
            if (!File.Exists(path)) return string.Empty;

            var notes = ReadNotes(path);
            if (notes.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("The user's current sticky notes (their live work plan / reminders):");
            foreach (var n in notes)
            {
                sb.Append("- ").AppendLine(n);
                if (sb.Length >= MaxChars) break;
            }
            string block = sb.ToString().TrimEnd();
            return block.Length > MaxChars ? block.Substring(0, MaxChars).TrimEnd() + " …" : block;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] sticky notes read failed: {ex.GetType().Name}: {ex.Message}");
            return string.Empty;
        }
    }

    private static List<string> ReadNotes(string path)
    {
        var notes = new List<string>();
        // Read-only so the live app's DB is never modified; the DB is WAL, so any read
        // error is caught by the caller and treated as "no context this run".
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
        };
        using var connection = new SqliteConnection(csb.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT text FROM note";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.IsDBNull(0)) continue;
            string t = ScenarioPromptHelpers.NormalizeWhitespace(reader.GetString(0));
            if (t.Length > 0) notes.Add(t);
        }
        return notes;
    }
}
