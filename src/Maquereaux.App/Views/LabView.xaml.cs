using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Maquereaux.App.Services;
using Maquereaux.App.ViewModels;

namespace Maquereaux.App.Views;

public partial class LabView : UserControl
{
    private LabViewModel? _vm;
    private Storyboard? _recPulse;

    public LabView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as LabViewModel);
        Loaded += (_, _) => Motion.Instance.PropertyChanged += OnMotionChanged;
        Unloaded += (_, _) => Motion.Instance.PropertyChanged -= OnMotionChanged;
    }

    private void Attach(LabViewModel? vm)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = vm;
        if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
        UpdateRecPulse();
    }

    private void OnMotionChanged(object? sender, PropertyChangedEventArgs e) => UpdateRecPulse();

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LabViewModel.IsRecording):
                UpdateRecPulse();
                break;
            case nameof(LabViewModel.Countdown) when _vm is { IsCountingDown: true }:
                PopCountdown();
                break;
        }
    }

    /// <summary>Chaque chiffre du 3-2-1 « tombe » : grossi puis ramené à sa taille, en fondu (250 ms).</summary>
    private void PopCountdown()
    {
        if (!Motion.Instance.Enabled) return;
        var d = TimeSpan.FromMilliseconds(250);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        CountdownScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(1.6, 1, d) { EasingFunction = ease });
        CountdownScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(1.6, 1, d) { EasingFunction = ease });
        CountdownText.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, d) { EasingFunction = ease });
    }

    /// <summary>Point REC pulsé pendant l'enregistrement (fixe si les animations sont coupées ou la fenêtre cachée).</summary>
    private void UpdateRecPulse()
    {
        bool pulse = _vm?.IsRecording == true && Motion.Instance.Enabled;
        if (pulse && _recPulse is null)
        {
            _recPulse = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = true };
            var fade = new DoubleAnimation(1, 0.25, TimeSpan.FromMilliseconds(700)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            Storyboard.SetTarget(fade, RecDot);
            Storyboard.SetTargetProperty(fade, new PropertyPath(OpacityProperty));
            _recPulse.Children.Add(fade);
            _recPulse.Begin(this, true);
        }
        else if (!pulse && _recPulse is not null)
        {
            _recPulse.Stop(this);
            _recPulse = null;
            RecDot.Opacity = 1;
        }
    }
}
