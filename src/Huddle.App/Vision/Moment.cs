using System;

namespace Huddle.Vision;

/// <summary>
/// A successful observation from a single capture. Matches the ADR 0001 moment schema.
/// </summary>
public sealed record Moment(
    string Id,
    DateTimeOffset Ts,
    string App,
    string WindowTitle,
    string Summary);
