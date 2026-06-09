using System.Collections.Generic;

namespace Huddle.Models;

/// <summary>
/// Hand-written seed patterns. Replace with a real source once detection lands.
/// </summary>
public static class PatternSeed
{
    public static readonly IReadOnlyList<Pattern> All = new Pattern[]
    {
        new(
            Id: "p1",
            Title: "Heavy context-switching",
            Description: "Bouncing between VS Code and Chrome docs on one task — 14 switches in 6 minutes.",
            SourceApps: new[] { "Code.exe", "Chrome" }),
        new(
            Id: "p2",
            Title: "Wrestling one sentence",
            Description: "The North Star line has been rewritten three times — visible effort worth narrating.",
            SourceApps: new[] { "Code.exe" }),
        new(
            Id: "p3",
            Title: "Repeating yourself",
            Description: "Same import block pasted 3×; a near-identical reply drafted in two apps.",
            SourceApps: new[] { "Code.exe", "Slack" }),
        new(
            Id: "p4",
            Title: "Long-running terminal command",
            Description: "A build has been spinning in the terminal for four minutes; you've checked it twice.",
            SourceApps: new[] { "Windows Terminal" }),
    };
}
