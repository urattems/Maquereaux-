using System.ComponentModel;
using System.Windows;

namespace Maquereaux.App.Services;

/// <summary>
/// Faut-il animer ? Combine le réglage Windows « Afficher les animations » (<see cref="SystemParameters.ClientAreaAnimation"/>)
/// et la visibilité de la fenêtre : aucune animation ne tourne quand la fenêtre est cachée ou réduite
/// (zéro coût CPU pendant qu'une macro tourne en arrière-plan).
/// Exposé comme ressource <c>Motion</c> pour les DataTriggers XAML.
/// </summary>
public sealed class Motion : INotifyPropertyChanged
{
    public static Motion Instance { get; } = new();

    private bool _windowActive = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Le réglage Windows autorise les animations.</summary>
    public bool SystemAllows => SystemParameters.ClientAreaAnimation;

    /// <summary>true si les animations décoratives (pulsations, transitions) doivent tourner maintenant.</summary>
    public bool Enabled => SystemAllows && _windowActive;

    /// <summary>Appelé par la fenêtre principale (visible et non réduite ?).</summary>
    public void SetWindowActive(bool active)
    {
        if (_windowActive == active) return;
        _windowActive = active;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
    }
}
