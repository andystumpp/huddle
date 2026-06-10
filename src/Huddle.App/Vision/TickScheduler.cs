using System;
using Microsoft.UI.Xaml;

namespace Huddle.Vision;

/// <summary>
/// 3-minute capture-tick scheduler. Fires <see cref="Tick"/> once immediately on
/// <see cref="Start"/>, then every 180 seconds while not paused.
/// </summary>
internal sealed class TickScheduler
{
    public const int PeriodSeconds = 180;

    private readonly DispatcherTimer _timer;
    private int _secondsRemaining = PeriodSeconds;
    private bool _started;

    public TickScheduler()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
    }

    public int SecondsRemaining => _secondsRemaining;
    public bool IsPaused { get; private set; }

    /// <summary>Raised when the countdown rolls over (and once at Start).</summary>
    public event EventHandler? Tick;

    public void Start()
    {
        if (_started) return;
        _started = true;
        _secondsRemaining = PeriodSeconds;
        Tick?.Invoke(this, EventArgs.Empty);
        _timer.Start();
    }

    public void Pause()
    {
        IsPaused = true;
    }

    public void Resume()
    {
        IsPaused = false;
        _secondsRemaining = PeriodSeconds;
    }

    public void Stop()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }

    private void OnTimerTick(object? sender, object e)
    {
        if (IsPaused) return;

        _secondsRemaining--;
        if (_secondsRemaining <= 0)
        {
            _secondsRemaining = PeriodSeconds;
            Tick?.Invoke(this, EventArgs.Empty);
        }
    }
}
