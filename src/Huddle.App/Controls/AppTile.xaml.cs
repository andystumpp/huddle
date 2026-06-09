using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Huddle.Controls;

public sealed partial class AppTile : UserControl
{
    // Monogram + tint mirror design/huddle/project/huddle/data.jsx APP_META.
    private static readonly Dictionary<string, (string Monogram, Color Tint)> AppMeta = new()
    {
        ["Code.exe"] = ("VS", FromHex("#3C9DF0")),
        ["Chrome"] = ("Cr", FromHex("#E8534B")),
        ["Notepad"] = ("Nt", FromHex("#8AA0B4")),
        ["Slack"] = ("Sl", FromHex("#C4A1E8")),
        ["Windows Terminal"] = (">_", FromHex("#4ED6A8")),
    };

    public static readonly DependencyProperty AppKeyProperty = DependencyProperty.Register(
        nameof(AppKey),
        typeof(string),
        typeof(AppTile),
        new PropertyMetadata(string.Empty, (d, _) => ((AppTile)d).Apply()));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size),
        typeof(int),
        typeof(AppTile),
        new PropertyMetadata(22, (d, _) => ((AppTile)d).Apply()));

    public string AppKey
    {
        get => (string)GetValue(AppKeyProperty);
        set => SetValue(AppKeyProperty, value);
    }

    public int Size
    {
        get => (int)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public AppTile()
    {
        InitializeComponent();
        Apply();
    }

    private void Apply()
    {
        Width = Size;
        Height = Size;
        TileBorder.Width = Size;
        TileBorder.Height = Size;
        MonogramText.FontSize = Size * 0.42;

        if (AppMeta.TryGetValue(AppKey ?? string.Empty, out var meta))
        {
            MonogramText.Text = meta.Monogram;
            TileBorder.Background = new SolidColorBrush(Blend(meta.Tint, FromHex("#2A2D33"), 0.78));
        }
        else
        {
            MonogramText.Text = "?";
            TileBorder.Background = new SolidColorBrush(FromHex("#2A2D33"));
        }
    }

    private static Color Blend(Color over, Color under, double overFraction)
    {
        var f = (float)overFraction;
        var g = 1f - f;
        return Color.FromArgb(
            0xFF,
            (byte)(over.R * f + under.R * g),
            (byte)(over.G * f + under.G * g),
            (byte)(over.B * f + under.B * g));
    }

    private static Color FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
        byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
        byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
        return Color.FromArgb(0xFF, r, g, b);
    }
}
