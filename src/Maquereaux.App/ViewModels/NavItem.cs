using System.Windows;
using System.Windows.Media;

namespace Maquereaux.App.ViewModels;

/// <summary>Entrée de la navigation latérale.</summary>
public sealed class NavItem
{
    public NavItem(string label, string subtitle, string iconKey, string shortcut = "")
    {
        Label = label;
        Subtitle = subtitle;
        IconKey = iconKey;
        Shortcut = shortcut;
    }

    /// <summary>Raccourci clavier de la page (« Ctrl+1 »).</summary>
    public string Shortcut { get; }

    /// <summary>Info-bulle : « Macro (Ctrl+1) ».</summary>
    public string ToolTip => Shortcut.Length == 0 ? Label : $"{Label} ({Shortcut})";

    public string Label { get; }

    /// <summary>Phrase affichée sous le titre de la page.</summary>
    public string Subtitle { get; }

    public string IconKey { get; }

    /// <summary>Géométrie de l'icône (résolue dans les ressources de l'appli).</summary>
    public Geometry? Glyph => Application.Current?.TryFindResource(IconKey) as Geometry;
}
