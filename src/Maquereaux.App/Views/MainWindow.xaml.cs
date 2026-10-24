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
