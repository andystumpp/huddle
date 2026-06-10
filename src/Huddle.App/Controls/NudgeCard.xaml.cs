using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Huddle.Models;

namespace Huddle.Controls;

public sealed partial class NudgeCard : UserControl
{
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
        ScenarioTagText.Text = ScenarioDisplayName(Nudge.Scenario);
        // Dot color stays violet for now — when more scenarios land,
        // wire scenario -> color here.
    }

    private static string ScenarioDisplayName(string key) => key switch
    {
        "linkedin-posts" => "LINKEDIN POSTS",
        _ => key.ToUpperInvariant(),
    };
}
