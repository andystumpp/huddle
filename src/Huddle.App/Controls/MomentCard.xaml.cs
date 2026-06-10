using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Huddle.Models;
using Huddle.Time;

namespace Huddle.Controls;

public sealed partial class MomentCard : UserControl
{
    public static readonly DependencyProperty MomentProperty = DependencyProperty.Register(
        nameof(Moment),
        typeof(Moment),
        typeof(MomentCard),
        new PropertyMetadata(null, (d, _) => ((MomentCard)d).Apply()));

    public Moment? Moment
    {
        get => (Moment?)GetValue(MomentProperty);
        set => SetValue(MomentProperty, value);
    }

    public MomentCard()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RelativeTime.Ticked += OnClockTick;
        UpdateTimestamp();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        RelativeTime.Ticked -= OnClockTick;
    }

    private void OnClockTick(object? sender, EventArgs e) => UpdateTimestamp();

    private void UpdateTimestamp()
    {
        if (Moment is null) return;
        TimestampText.Text = RelativeTime.Format(Moment.Ts, DateTimeOffset.Now);
    }

    private void Apply()
    {
        if (Moment is null) return;
        SummaryText.Text = Moment.Summary;
        SourceTile.AppKey = Moment.App;
        WindowTitleText.Text = Moment.WindowTitle;
        UpdateTimestamp();
    }
}
