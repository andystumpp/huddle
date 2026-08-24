using System;
using System.Diagnostics;
using System.IO;

namespace Huddle.Memory;

/// <summary>
/// The user's learned profile: a single markdown file at
/// <c>%LOCALAPPDATA%\Huddle\profile.md</c>, maintained by the daily reflection and
/// prepended to every scenario prompt. A file (not the database) so the user can open and
/// edit it directly; read fresh on each use so an edit or a new reflection applies without
/// restarting the app. The file's modified time doubles as the reflection's "last run" clock.
/// </summary>
internal static class ProfileStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Huddle", "profile.md");

    /// <summary>The current profile text, or an empty string if none exists yet.</summary>
    public static string Read()
    {
        try { return File.Exists(FilePath) ? File.ReadAllText(FilePath) : string.Empty; }
        catch { return string.Empty; }
    }

    public static void Write(string profile)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, profile);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] ProfileStore write failed: {ex.Message}");
        }
    }

    /// <summary>True when the profile is missing or older than <paramref name="interval"/>.</summary>
    public static bool IsDue(TimeSpan interval)
    {
        try
        {
            if (!File.Exists(FilePath)) return true;
            return DateTime.UtcNow - File.GetLastWriteTimeUtc(FilePath) >= interval;
        }
        catch
        {
            return false; // on error, don't spam reflections
        }
    }
}
