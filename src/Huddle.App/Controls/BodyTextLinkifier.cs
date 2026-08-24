using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Huddle.Controls;

/// <summary>
/// Turns a nudge body string into inline runs, wrapping URLs in clickable
/// <see cref="Hyperlink"/>s. Pure with respect to the string — a body with no URL
/// yields a single <see cref="Run"/>, identical to plain text. Only absolute
/// http/https URIs are ever wrapped; nothing else is launchable.
/// </summary>
internal static class BodyTextLinkifier
{
    // Curated web-TLD allowlist for *bare* hosts (no scheme). Kept to real web TLDs so
    // dotted code tokens (profile.md, Huddle.App, 2.1.3) are not mistaken for links.
    // `.sh` is intentionally excluded — it collides with shell scripts (build.sh); an
    // explicit https://x.sh still links via the explicit tier.
    private static readonly HashSet<string> WebTlds = new(StringComparer.OrdinalIgnoreCase)
    {
        "com", "org", "net", "io", "dev", "ai", "co", "app", "xyz", "info", "news",
        "me", "tv", "gg", "so", "edu", "gov", "de", "uk", "ca", "fr", "eu",
    };

    // Tier 1 (explicit): a full http/https URL. Tier 2 (bare): an optional www. plus a
    // dotted host and optional path. Both stop at whitespace and a few delimiters;
    // trailing sentence punctuation is trimmed after matching.
    private static readonly Regex Candidate = new(
        @"(?<explicit>https?://[^\s<>""]+)|(?<bare>(?:www\.)?[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+(?:/[^\s<>""]*)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const string TrailingTrim = ".,;:!?)]}'\"";

    // A light accent that reads as a link against the card's translucent-white body text.
    private static readonly SolidColorBrush s_linkBrush =
        new(Color.FromArgb(0xFF, 0x8A, 0xB4, 0xF8));

    public static IReadOnlyList<Inline> BuildInlines(string? body)
    {
        body ??= string.Empty;
        var inlines = new List<Inline>();
        int pos = 0;

        foreach (Match m in Candidate.Matches(body))
        {
            if (m.Index < pos) continue; // matches are non-overlapping; guard anyway

            if (m.Index > pos)
            {
                inlines.Add(PlainRun(body.Substring(pos, m.Index - pos)));
            }

            string raw = m.Value;
            int end = raw.Length;
            while (end > 0 && TrailingTrim.IndexOf(raw[end - 1]) >= 0) end--;
            string candidate = raw.Substring(0, end);
            string trailing = raw.Substring(end);

            if (TryMakeUri(m, candidate, out Uri? uri))
            {
                inlines.Add(LinkRun(candidate, uri!));
            }
            else
            {
                inlines.Add(PlainRun(candidate));
            }

            if (trailing.Length > 0) inlines.Add(PlainRun(trailing));
            pos = m.Index + raw.Length;
        }

        if (pos < body.Length) inlines.Add(PlainRun(body.Substring(pos)));
        if (inlines.Count == 0) inlines.Add(PlainRun(body)); // empty body → single empty run

        return inlines;
    }

    private static bool TryMakeUri(Match m, string candidate, out Uri? uri)
    {
        uri = null;
        if (candidate.Length == 0) return false;

        string urlText = candidate;
        bool bareOnly = m.Groups["bare"].Success && !m.Groups["explicit"].Success;
        if (bareOnly)
        {
            // Require the final host label to be a known web TLD, then treat it as https.
            string host = candidate;
            int slash = host.IndexOf('/');
            if (slash >= 0) host = host.Substring(0, slash);
            // Real bare hosts are lowercase; PascalCase (e.g. Huddle.App, Microsoft.UI.Xaml)
            // is a code token, not a domain — even when the last label (.App) is a real TLD.
            if (host != host.ToLowerInvariant()) return false;
            int lastDot = host.LastIndexOf('.');
            if (lastDot < 0) return false;
            if (!WebTlds.Contains(host.Substring(lastDot + 1))) return false;
            urlText = "https://" + candidate;
        }

        return Uri.TryCreate(urlText, UriKind.Absolute, out uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static Run PlainRun(string text) => new() { Text = text };

    private static Hyperlink LinkRun(string text, Uri uri)
    {
        var link = new Hyperlink { NavigateUri = uri, Foreground = s_linkBrush };
        link.Inlines.Add(new Run { Text = text });
        return link;
    }
}
