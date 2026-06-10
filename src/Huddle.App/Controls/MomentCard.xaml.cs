using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Huddle.Models;

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
    }

    private void Apply()
    {
        if (Moment is null) return;
        SummaryText.Text = Moment.Summary;
        SourceTile.AppKey = Moment.App;
        WindowTitleText.Text = Moment.WindowTitle;
    }
}
