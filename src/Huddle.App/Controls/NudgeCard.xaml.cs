using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Huddle.Models;
using Huddle.Scenarios;

namespace Huddle.Controls;

public sealed partial class NudgeCard : UserControl
{
    private static readonly Color s_fallbackDot = Color.FromArgb(0xFF, 0xC5, 0x8B, 0xFF);

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
