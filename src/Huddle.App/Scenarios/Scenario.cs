using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Anthropic.Models.Messages;
using Huddle.Models;

namespace Huddle.Scenarios;

/// <summary>
/// Base class for all scenarios. Owns the throttle clock, the concurrent-run gate,
/// and the call template. Subclasses implement <see cref="ExecuteAsync"/> with
/// the actual Claude call.
/// </summary>
internal abstract class Scenario
{
    public abstract string Key { get; }
    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public abstract string AccentColorHex { get; }
    public abstract TimeSpan Cadence { get; }
    public abstract int TrailSize { get; }
    public virtual int PriorNudgesSize => 10;

    /// <summary>The Claude model used for this scenario's call. Sonnet by default.</summary>
    public virtual Model ModelId => Model.ClaudeSonnet4_6;

    /// <summary>
    /// The Claude backend for this scenario's call — API (metered) or CLI
    /// (subscription), resolved once from config. Subclasses call
    /// <c>Backend.CompleteAsync(...)</c> rather than the SDK directly.
    /// </summary>
    protected IScenarioBackend Backend { get; } = ScenarioBackendFactory.Resolve();

    private DateTimeOffset _lastRun = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsDue(DateTimeOffset now) => now - _lastRun >= Cadence;

    public async Task<ScenarioResult> RunAsync(
        IReadOnlyList<Moment> trail,
        IReadOnlyList<Nudge> priorNudges,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _lastRun = DateTimeOffset.UtcNow;
            return await ExecuteAsync(trail, priorNudges, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] scenario {Key} failed: {ex.GetType().Name}: {ex.Message}");
            return new ScenarioResult(null, null);
        }
        finally
        {
            _gate.Release();
        }
    }

    protected abstract Task<ScenarioResult> ExecuteAsync(
        IReadOnlyList<Moment> trail,
        IReadOnlyList<Nudge> priorNudges,
        CancellationToken ct);
}
