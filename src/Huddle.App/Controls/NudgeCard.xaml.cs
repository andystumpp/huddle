using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Huddle.Models;
using Huddle.Scenarios;
using Huddle.Storage;

namespace Huddle.Controls;

public sealed partial class NudgeCard : UserControl
{
    private static readonly string StarOutlineGlyph = char.ConvertFromUtf32(0xE1CE);
    private static readonly string StarFilledGlyph = char.ConvertFromUtf32(0xE1CF);
    private static readonly string CopyGlyph = char.ConvertFromUtf32(0xE8C8);
    private static readonly string CheckGlyph = char.ConvertFromUtf32(0xE73E);

    private static readonly Color s_fallbackDot = Color.FromArgb(0xFF, 0xC5, 0x8B, 0xFF);
    private static readonly SolidColorBrush s_starOn =
        new(Color.FromArgb(0xFF, 0xFF, 0xD4, 0x6B));
    private static readonly SolidColorBrush s_actionFg =
        new(Color.FromArgb(0xA8, 0xFF, 0xFF, 0xFF));

    public static readonly DependencyProperty NudgeProperty = DependencyProperty.Register(
        nameof(Nudge),
        typeof(Nudge),
        typeof(NudgeCard),
        new PropertyMetadata(null, (d, _) => ((NudgeCard)d).Apply()));

    public Nudge? Nudge
    {
        get => (Nudge?)GetValue(NudgeProperty);
        set => SetValue(NudgeProperty, value);
    }

    public NudgeCard()
    {
        InitializeComponent();
    }

    private void Apply()
    {
        if (Nudge is null) return;
        TitleText.Text = Nudge.Title;
        BodyText.Text = Nudge.Body;

        var scenario = ScenarioRegistry.GetByKey(Nudge.Scenario);
        ScenarioTagText.Text = scenario?.DisplayName ?? Nudge.Scenario.ToUpperInvariant();
        ScenarioDot.Fill = new SolidColorBrush(
            scenario is null ? s_fallbackDot : ParseHex(scenario.AccentColorHex));

        ApplyStarVisual(Nudge.IsStarred);
    }

    private void ApplyStarVisual(bool starred)
    {
        StarIcon.Glyph = starred ? StarFilledGlyph : StarOutlineGlyph;
        StarIcon.Foreground = starred ? s_starOn : s_actionFg;
    }

    private async void OnStarClick(object sender, RoutedEventArgs e)
    {
        if (Nudge is null) return;
        Nudge.IsStarred = !Nudge.IsStarred;
        ApplyStarVisual(Nudge.IsStarred);
        try
        {
            await NudgeStore.SetStarredAsync(Nudge.Id, Nudge.IsStarred);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] star toggle failed: {ex.Message}");
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (Nudge is null) return;
        var pkg = new DataPackage();
        pkg.SetText($"{Nudge.Title}\r\n\r\n{Nudge.Body}");
        Clipboard.SetContent(pkg);

        CopyIcon.Glyph = CheckGlyph;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            CopyIcon.Glyph = CopyGlyph;
        };
        timer.Start();
    }

    private static Color ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return s_fallbackDot;
        try
        {
            byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            return Color.FromArgb(0xFF, r, g, b);
        }
        catch
        {
            return s_fallbackDot;
        }
    }
}
