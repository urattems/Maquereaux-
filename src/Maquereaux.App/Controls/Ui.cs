using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Maquereaux.App.Controls;

/// <summary>
/// Propriétés attachées du design system, lues par les templates de <c>Themes/Controls.xaml</c>.
/// <list type="bullet">
/// <item><c>Ui.Icon</c> : icône vectorielle affichée avant le contenu d'un bouton.</item>
/// <item><c>Ui.Placeholder</c> : texte d'aide affiché dans un champ vide.</item>
/// <item><c>Ui.ForceHover</c> / <c>Ui.ForcePressed</c> : forcent l'état visuel (galerie du design system pour les
/// captures, et bouton Agrandir piloté par les messages Win32 pour les Snap Layouts).</item>
/// </list>
/// </summary>
public static class Ui
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon", typeof(Geometry), typeof(Ui), new PropertyMetadata(null));

    public static Geometry? GetIcon(DependencyObject d) => (Geometry?)d.GetValue(IconProperty);
    public static void SetIcon(DependencyObject d, Geometry? value) => d.SetValue(IconProperty, value);

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
        "Placeholder", typeof(string), typeof(Ui), new PropertyMetadata(null));

    public static string? GetPlaceholder(DependencyObject d) => (string?)d.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject d, string? value) => d.SetValue(PlaceholderProperty, value);

    public static readonly DependencyProperty ForceHoverProperty = DependencyProperty.RegisterAttached(
        "ForceHover", typeof(bool), typeof(Ui), new PropertyMetadata(false));

    public static bool GetForceHover(DependencyObject d) => (bool)d.GetValue(ForceHoverProperty);
    public static void SetForceHover(DependencyObject d, bool value) => d.SetValue(ForceHoverProperty, value);

    public static readonly DependencyProperty ForcePressedProperty = DependencyProperty.RegisterAttached(
        "ForcePressed", typeof(bool), typeof(Ui), new PropertyMetadata(false));

    public static bool GetForcePressed(DependencyObject d) => (bool)d.GetValue(ForcePressedProperty);
    public static void SetForcePressed(DependencyObject d, bool value) => d.SetValue(ForcePressedProperty, value);

    /// <summary>Force l'aspect « focus clavier » (galerie du design system uniquement).</summary>
    public static readonly DependencyProperty ForceFocusProperty = DependencyProperty.RegisterAttached(
        "ForceFocus", typeof(bool), typeof(Ui), new PropertyMetadata(false));

    public static bool GetForceFocus(DependencyObject d) => (bool)d.GetValue(ForceFocusProperty);
    public static void SetForceFocus(DependencyObject d, bool value) => d.SetValue(ForceFocusProperty, value);

    /// <summary>Marque un champ en erreur (bordure corail) sans passer par la validation WPF.</summary>
    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.RegisterAttached(
        "HasError", typeof(bool), typeof(Ui), new PropertyMetadata(false));

    public static bool GetHasError(DependencyObject d) => (bool)d.GetValue(HasErrorProperty);
    public static void SetHasError(DependencyObject d, bool value) => d.SetValue(HasErrorProperty, value);
}

/// <summary>
/// Icône vectorielle (géométrie Fluent 20×20) colorée par le <see cref="Control.Foreground"/> hérité.
/// Dessinée directement (pas de template) : un <c>Style</c> local ne peut pas la « casser », et c'est plus léger
/// qu'un Viewbox + Canvas + Path par icône.
/// </summary>
public sealed class Icon : Control
{
    private const double GlyphSize = 20;

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(Geometry), typeof(Icon), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    static Icon()
    {
        FocusableProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(false));
        WidthProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(16.0));
        HeightProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(16.0));
        ForegroundProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(
            SystemColors.ControlTextBrush, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
    }

    public Geometry? Glyph
    {
        get => (Geometry?)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Glyph is not { } glyph || ActualWidth <= 0 || ActualHeight <= 0) return;
        double scale = Math.Min(ActualWidth, ActualHeight) / GlyphSize;
        dc.PushTransform(new TranslateTransform((ActualWidth - GlyphSize * scale) / 2, (ActualHeight - GlyphSize * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawGeometry(Foreground, null, glyph);
        dc.Pop();
        dc.Pop();
    }
}
