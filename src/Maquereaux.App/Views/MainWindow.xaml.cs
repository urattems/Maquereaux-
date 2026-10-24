using System.ComponentModel;
using System.Windows;
using Maquereaux.App.ViewModels;

namespace Maquereaux.App.Views;

/// <summary>
/// Fenêtre principale. Le code-behind ne contient que de la gestion de fenêtres (compact, zone de
/// notification) : aucune logique métier.
/// </summary>
public partial class MainWindow : Window
{
    private CompactWindow? _compact;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel old) old.PropertyChanged -= OnVmChanged;
            if (e.NewValue is MainViewModel vm) vm.PropertyChanged += OnVmChanged;
        };
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    /// <summary>Restaure la position/taille mémorisée si elle est encore visible sur un écran.</summary>
    public void RestorePlacement()
    {
        if (Vm?.WindowPlacement is not { } p) return;
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                              SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var r = new Rect(p.Left, p.Top, Math.Max(MinWidth, p.Width), Math.Max(MinHeight, p.Height));
        // Écran débranché depuis ? → on garde la position centrée par défaut
        if (!screen.IntersectsWith(new Rect(r.Left + 40, r.Top + 10, 100, 40))) return;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = r.Left;
        Top = r.Top;
        Width = r.Width;
        Height = r.Height;
        if (p.Maximized) WindowState = WindowState.Maximized;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (Vm is { } vm)
        {
            var b = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            if (!b.IsEmpty) vm.WindowPlacement = new Core.Models.WindowPlacement(b.Left, b.Top, b.Width, b.Height, WindowState == WindowState.Maximized);
        }
        base.OnClosing(e);
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsCompact)) ApplyCompact();
    }

    private void ApplyCompact()
    {
        if (Vm is not { } vm) return;
        if (vm.IsCompact)
        {
            _compact ??= new CompactWindow { DataContext = vm };
            // Position par défaut : coin supérieur droit de l'écran de travail
            var area = SystemParameters.WorkArea;
            _compact.Left = area.Right - _compact.Width - 24;
            _compact.Top = area.Top + 24;
            _compact.Show();
            Hide();
        }
        else
        {
            _compact?.Hide();
            ShowFromTray();
        }
    }

    /// <summary>Réaffiche la fenêtre (depuis la zone de notification ou le mode compact).</summary>
    public void ShowFromTray()
    {
        if (Vm is { IsCompact: true } vm) vm.IsCompact = false;
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized && Vm is { MinimizeToTray: true }) Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _compact?.Close();
        base.OnClosed(e);
    }
}
