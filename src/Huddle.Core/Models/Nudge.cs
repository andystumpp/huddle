using System;
using System.Collections.Generic;

namespace Huddle.Models;

/// <summary>
/// A scenario-emitted suggestion built from recent moments. Matches the
/// `nudges` table schema.
/// </summary>
public sealed record Nudge(
    string Id,
    DateTimeOffset Ts,
    string Scenario,
    string Title,
    string Body,
    IReadOnlyList<string> Sources)
{
    public bool IsStarred { get; set; }
}
