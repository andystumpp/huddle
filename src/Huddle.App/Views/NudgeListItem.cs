using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Huddle.Views;

/// <summary>A day-grouping header row in the nudge list (e.g. "TODAY").</summary>
public sealed record NudgeDayHeader(string Label);

/// <summary>
/// Picks the header template for a <see cref="NudgeDayHeader"/> and the card
/// template for a <see cref="Huddle.Models.Nudge"/>, so a single ItemsRepeater
/// renders a day-grouped, mixed list.
/// </summary>
public sealed partial class NudgeListItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }
    public DataTemplate? NudgeTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
        => item is NudgeDayHeader ? HeaderTemplate : NudgeTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
