using System;
using Microsoft.UI.Xaml;

namespace Huddle.Time;

/// <summary>
/// Shared relative-time formatting plus a single once-a-minute clock the cards
/// subscribe to. One timer for the whole panel — cards attach on load and
/// detach on unload, so virtualized-away cards leave no dangling handlers.
/// </summary>
public static class RelativeTime
{
    /// <summary>
    /// Maps the age of <paramref name="ts"/> relative to <paramref name="now"/>
    /// to a compact label: "just now" / "{m}min ago" / "{h}h ago" / "{d}d ago".
    /// </summary>
    public static string Format(DateTimeOffset ts, DateTimeOffset now)
    {
        var age = now - ts;
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;

        if (age.TotalSeconds < 60) return "just now";
        if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes}min ago";
        if (age.TotalHours < 24) return $"{(int)age.TotalHours}h ago";
        return $"{(int)age.TotalDays}d ago";
    }

    /// <summary>Raised roughly once per minute while the clock is running.</summary>
    public static event EventHandler? Ticked;

    private static DispatcherTimer? s_timer;

    /// <summary>Start the shared minute clock. Idempotent; call from the UI thread.</summary>
    public static void Start()
    {
        if (s_timer is not null) return;
        s_timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        s_timer.Tick += (_, _) => Ticked?.Invoke(null, EventArgs.Empty);
        s_timer.Start();
    }

    /// <summary>Stop the shared minute clock. Idempotent.</summary>
    public static void Stop()
    {
        s_timer?.Stop();
        s_timer = null;
    }
}
