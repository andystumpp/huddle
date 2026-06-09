using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Huddle.Models;

namespace Huddle.Controls;

public sealed partial class PatternCard : UserControl
{
    public static readonly DependencyProperty PatternProperty = DependencyProperty.Register(
        nameof(Pattern),
        typeof(Pattern),
        typeof(PatternCard),
        new PropertyMetadata(null, (d, _) => ((PatternCard)d).Apply()));

    public Pattern? Pattern
    {
        get => (Pattern?)GetValue(PatternProperty);
        set => SetValue(PatternProperty, value);
    }

    public PatternCard()
    {
        InitializeComponent();
    }

    private void Apply()
    {
        if (Pattern is null) return;
        TitleText.Text = Pattern.Title;
        DescriptionText.Text = Pattern.Description;

        SourceApps.Children.Clear();
        foreach (var app in Pattern.SourceApps)
        {
            SourceApps.Children.Add(new AppTile { AppKey = app, Size = 22 });
        }
    }
}
