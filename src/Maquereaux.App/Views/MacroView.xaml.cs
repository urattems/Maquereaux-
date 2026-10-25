using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Maquereaux.App.Services;
using Maquereaux.App.ViewModels;
using Maquereaux.Core.Engine;

namespace Maquereaux.App.Views;

public partial class MacroView : UserControl
{
    /// <summary>En dessous de cette largeur, la mini courbe de la carte héros est masquée (les stats passent avant).</summary>
    private const double HeroCurveMinWidth = 760;

    private Storyboard? _glowPulse;
    private MainViewModel? _vm;

    public MacroView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as MainViewModel);
        Loaded += (_, _) => Motion.Instance.PropertyChanged += OnMotionChanged;
        Unloaded += (_, _) => Motion.Instance.PropertyChanged -= OnMotionChanged;
        SizeChanged += (_, e) => HeroCurve.Visibility = e.NewSize.Width >= HeroCurveMinWidth ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Attach(MainViewModel? vm)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = vm;
        if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
        UpdateGlow();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.EngineState)) UpdateGlow();
    }

    private void OnMotionChanged(object? sender, PropertyChangedEventArgs e) => UpdateGlow();

    /// <summary>
    /// Halo du disque Play/Stop : visible quand la macro tourne, pulsé seulement si les animations sont permises
    /// (réglage Windows) et la fenêtre visible — zéro animation en arrière-plan.
    /// </summary>
    private void UpdateGlow()
    {
        bool running = _vm?.EngineState == EngineState.Running;
        bool pulse = running && Motion.Instance.Enabled;

        if (!pulse && _glowPulse is not null)
        {
            _glowPulse.Stop(this);
            _glowPulse = null;
        }
        if (pulse && _glowPulse is null)
        {
            var t = TimeSpan.FromMilliseconds(1200);
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
            _glowPulse = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = true };
            var fade = new DoubleAnimation(0.25, 0.6, t) { EasingFunction = ease };
            Storyboard.SetTarget(fade, HeroGlow);
            Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
            var sx = new DoubleAnimation(1.0, 1.25, t) { EasingFunction = ease };
            Storyboard.SetTarget(sx, HeroGlow);
            Storyboard.SetTargetProperty(sx, new PropertyPath("RenderTransform.ScaleX"));
            var sy = sx.Clone();
            Storyboard.SetTargetProperty(sy, new PropertyPath("RenderTransform.ScaleY"));
            _glowPulse.Children.Add(fade);
            _glowPulse.Children.Add(sx);
            _glowPulse.Children.Add(sy);
            _glowPulse.Begin(this, true);
        }
        if (!pulse) HeroGlow.Opacity = running ? 0.4 : 0;
    }
}
