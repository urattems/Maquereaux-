using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Maquereaux.App.Controls;

namespace Maquereaux.App.Views;

/// <summary>Galerie du design system (utilisée par le smoke test pour documenter chaque contrôle dans chaque état).</summary>
public partial class DesignSystemView : UserControl
{
    public DesignSystemView()
    {
        InitializeComponent();
        // Planche de toutes les icônes du dictionnaire
        var keys = Application.Current.Resources.MergedDictionaries
            .SelectMany(d => d.Keys.OfType<string>())
            .Where(k => k.StartsWith("Icon.", StringComparison.Ordinal))
            .Distinct()
            .OrderBy(k => k);
        foreach (var key in keys)
        {
            if (Application.Current.TryFindResource(key) is not Geometry g) continue;
            var cell = new StackPanel { Width = 84, Margin = new Thickness(0, 0, 8, 12) };
            cell.Children.Add(new Icon { Glyph = g, Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Center });
            var label = new TextBlock { Text = key[5..], HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0), FontSize = 11 };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Text.Secondary");
            cell.Children.Add(label);
            IconGrid.Children.Add(cell);
        }
    }
}
