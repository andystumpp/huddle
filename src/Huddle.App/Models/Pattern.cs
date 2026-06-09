using System.Collections.Generic;

namespace Huddle.Models;

/// <summary>
/// A scenario-neutral observation Huddle has surfaced about the user's workday.
/// </summary>
public sealed record Pattern(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<string> SourceApps);
