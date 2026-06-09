using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Huddle.Vision;

/// <summary>
/// Appends moments and capture failures to <c>%LOCALAPPDATA%\Huddle\moments.log</c>
/// as JSON-Lines. Success and failure entries share the same file; failure entries
/// carry a non-null <c>error</c> field.
/// </summary>
internal static class MomentLog
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = false,
    };

    public static Task AppendSuccessAsync(Moment moment)
    {
        var entry = new
        {
            id = moment.Id,
            ts = moment.Ts.ToString("o"),
            app = moment.App,
            window_title = moment.WindowTitle,
            summary = moment.Summary,
            error = (string?)null,
        };
        return WriteLineAsync(entry);
    }

    public static Task AppendFailureAsync(string app, string windowTitle, string errorMessage)
    {
        var entry = new
        {
            id = UlidGenerator.Generate(),
            ts = DateTimeOffset.UtcNow.ToString("o"),
            app = app,
            window_title = windowTitle,
            summary = (string?)null,
            error = errorMessage,
        };
        return WriteLineAsync(entry);
    }

    private static async Task WriteLineAsync(object entry)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Huddle");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "moments.log");
            string line = JsonSerializer.Serialize(entry, s_options) + "\n";
            await File.AppendAllTextAsync(path, line).ConfigureAwait(false);
        }
        catch
        {
            // The log is best-effort. Don't take the app down if disk is unavailable.
        }
    }
}
