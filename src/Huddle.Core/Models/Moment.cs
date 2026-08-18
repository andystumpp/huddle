using System;

namespace Huddle.Models;

/// <summary>
/// A scenario-neutral observation produced by one capture + Claude vision call.
/// Schema matches ADR 0001's moment row.
/// </summary>
public sealed record Moment(
    string Id,
    DateTimeOffset Ts,
    string App,
    string WindowTitle,
    string Summary);
