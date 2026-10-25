using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Maquereaux.App.Controls;
using Maquereaux.App.Services;
using Maquereaux.App.ViewModels;
using Maquereaux.Core.Engine;

namespace Maquereaux.App.Views;

/// <summary>
/// Fenêtre principale. Le code-behind ne gère que la fenêtre elle-même (barre de titre maison, Mica, Snap Layouts,
/// mode compact, zone de notification, animations décoratives) : aucune logique métier.
/// </summary>
public partial class MainWindow : Window
{
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int WM_NCLBUTTONUP = 0x00A2;
    private const int WM_NCMOUSELEAVE = 0x02A2;
    private const int HTMAXBUTTON = 9;

    private CompactWindow? _compact;
    private Storyboard? _haloPulse;
    private bool _micaActive;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel old) old.PropertyChanged -= OnVmChanged;
            if (e.NewValue is MainViewModel vm) vm.PropertyChanged += OnVmChanged;
        };
        SourceInitialized += OnSourceInitialized;
        StateChanged += (_, _) => { UpdateMaximizedLayout(); UpdateMotion(); };
        IsVisibleChanged += (_, _) => UpdateMotion();
        Tabs.SelectionChanged += OnPageChanged;
        ThemeEvents.Changed += ApplyBackdrop;
        Motion.Instance.PropertyChanged += (_, _) => UpdateHalo();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    // ---------------------------------------------------------------- Mica & barre de titre

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyBackdrop();
        if (PresentationSource.FromVisual(this) is HwndSource source) source.AddHook(WndProc);
        UpdateMaximizedLayout();
    }

    /// <summary>Mica si dispo (fond transparent + voile teinté), sinon fond uni. Réappliqué à chaque changement de thème.</summary>
    private void ApplyBackdrop()
    {
        bool dark = Vm is not { IsLightTheme: true };
        _micaActive = WindowEffects.Apply(this, dark);
        if (_micaActive)
        {
            Background = Brushes.Transparent;
            Backdrop.SetResourceReference(Border.BackgroundProperty, "Bg.BaseTint");
        }
        else
        {
            SetResourceReference(BackgroundProperty, "Bg.Base");
            Backdrop.SetResourceReference(Border.BackgroundProperty, "Bg.Base");
        }
    }

    /// <summary>
    /// Fenêtre agrandie : avec WindowChrome elle déborde de l'écran de l'épaisseur du cadre → on compense,
    /// sinon la barre de titre et les bords seraient coupés.
    /// </summary>
    private void UpdateMaximizedLayout()
    {
        if (WindowState == WindowState.Maximized)
        {
            var t = SystemParameters.WindowResizeBorderThickness;
            Root.Margin = new Thickness(t.Left + 4, t.Top + 4, t.Right + 4, t.Bottom + 4);
            Ui.SetIcon(MaxButton, (Geometry)FindResource("Icon.Restore"));
            MaxButton.ToolTip = "Restaurer";
            AutomationPropertiesHelper.SetName(MaxButton, "Restaurer");
        }
        else
        {
            Root.Margin = new Thickness(0);
            Ui.SetIcon(MaxButton, (Geometry)FindResource("Icon.Maximize"));
            MaxButton.ToolTip = "Agrandir";
            AutomationPropertiesHelper.SetName(MaxButton, "Agrandir");
        }
    }

    /// <summary>
    /// Snap Layouts (Windows 11) : Windows n'affiche le menu de disposition que si le survol du bouton Agrandir est
    /// déclaré comme zone HTMAXBUTTON. On répond donc nous-mêmes au hit-test, et on pilote l'aspect du bouton.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
                if (IsOverMaxButton(lParam))
                {
                    Ui.SetForceHover(MaxButton, true);
                    handled = true;
                    return HTMAXBUTTON;
                }
                Ui.SetForceHover(MaxButton, false);
                Ui.SetForcePressed(MaxButton, false);
                break;
            case WM_NCLBUTTONDOWN when wParam.ToInt32() == HTMAXBUTTON:
                Ui.SetForcePressed(MaxButton, true);
                handled = true;
                break;
            case WM_NCLBUTTONUP when wParam.ToInt32() == HTMAXBUTTON:
                Ui.SetForcePressed(MaxButton, false);
                ToggleMaximize();
                handled = true;
                break;
            case WM_NCMOUSELEAVE:
                Ui.SetForceHover(MaxButton, false);
                Ui.SetForcePressed(MaxButton, false);
                break;
        }
        return IntPtr.Zero;
    }

    private bool IsOverMaxButton(IntPtr lParam)
    {
        if (!MaxButton.IsVisible) return false;
        int x = (short)(lParam.ToInt64() & 0xFFFF);
        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        try
        {
            var p = MaxButton.PointFromScreen(new Point(x, y));
            return p.X >= 0 && p.Y >= 0 && p.X <= MaxButton.ActualWidth && p.Y <= MaxButton.ActualHeight;
        }
        catch (InvalidOperationException)
        {
            return false; // pas encore connecté à une source de présentation
        }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximize(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    // ---------------------------------------------------------------- Animations décoratives

    private void UpdateMotion() => Motion.Instance.SetWindowActive(IsVisible && WindowState != WindowState.Minimized);

    /// <summary>Transition douce entre les pages (fondu + glissement de 8 px, 200 ms).</summary>
    private void OnPageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource != Tabs || !Motion.Instance.Enabled) return;
        var translate = new TranslateTransform(0, 8);
        Tabs.RenderTransform = translate;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(200);
        Tabs.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, duration) { EasingFunction = ease });
    }

    /// <summary>Halo pulsé autour de la pastille d'état quand la macro tourne (coupé si animations désactivées ou fenêtre cachée).</summary>
    private void UpdateHalo()
    {
        bool shouldPulse = Vm?.EngineState == EngineState.Running && Motion.Instance.Enabled;
        if (shouldPulse && _haloPulse is null)
        {
            _haloPulse = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var t = TimeSpan.FromMilliseconds(1600);
            var fade = new DoubleAnimation(0.55, 0, t) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(fade, Halo);
            Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
            var sx = new DoubleAnimation(1, 2.6, t) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(sx, Halo);
            Storyboard.SetTargetProperty(sx, new PropertyPath("RenderTransform.ScaleX"));
            var sy = sx.Clone();
            Storyboard.SetTargetProperty(sy, new PropertyPath("RenderTransform.ScaleY"));
            _haloPulse.Children.Add(fade);
            _haloPulse.Children.Add(sx);
            _haloPulse.Children.Add(sy);
            _haloPulse.Begin(this, true);
        }
        else if (!shouldPulse && _haloPulse is not null)
        {
            _haloPulse.Stop(this);
            _haloPulse = null;
            Halo.Opacity = 0;
        }
    }

    // ---------------------------------------------------------------- Placement, compact, zone de notification

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
        else if (e.PropertyName == nameof(MainViewModel.EngineState)) UpdateHalo();
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
        ThemeEvents.Changed -= ApplyBackdrop;
        _compact?.Close();
        base.OnClosed(e);
    }
}

internal static class AutomationPropertiesHelper
{
    public static void SetName(DependencyObject d, string name) => System.Windows.Automation.AutomationProperties.SetName(d, name);
}
