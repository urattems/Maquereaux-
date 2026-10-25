using System.Windows;
using System.Windows.Media;

namespace Maquereaux.App.Controls;

/// <summary>
/// Mini courbe des intervalles récents (carte héros) : aire dégradée + ligne + repère pointillé de l'intervalle cible.
/// Dessin direct (StreamGeometry), pas de lib : ~120 points rafraîchis 10×/s pour un coût négligeable.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.Turquoise, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TargetBrushProperty = DependencyProperty.Register(
        nameof(TargetBrush), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values { get => (IReadOnlyList<double>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public double Target { get => (double)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public Brush TargetBrush { get => (Brush)GetValue(TargetBrushProperty); set => SetValue(TargetBrushProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 4 || h < 4) return;
        var values = Values;
        double target = Target;

        // Échelle verticale centrée sur la cible : ±50 % de l'intervalle (ou l'écart réel s'il est plus grand)
        double lo = target > 0 ? target * 0.5 : 0, hi = target > 0 ? target * 1.5 : 1;
        if (values is { Count: > 0 })
        {
            lo = Math.Min(lo, values.Min());
            hi = Math.Max(hi, values.Max());
        }
        if (hi - lo < 1e-6) hi = lo + 1;
        double Y(double v) => h - 2 - (v - lo) / (hi - lo) * (h - 4);

        // Repère de l'intervalle cible
        if (target > 0)
        {
            var pen = new Pen(TargetBrush, 1) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) };
            pen.Freeze();
            dc.DrawLine(pen, new Point(0, Y(target)), new Point(w, Y(target)));
        }
        if (values is not { Count: > 1 }) return;

        var line = new StreamGeometry();
        var area = new StreamGeometry();
        double step = w / (values.Count - 1);
        using (var l = line.Open())
        using (var a = area.Open())
        {
            l.BeginFigure(new Point(0, Y(values[0])), false, false);
            a.BeginFigure(new Point(0, h), true, true);
            a.LineTo(new Point(0, Y(values[0])), false, false);
            for (int i = 1; i < values.Count; i++)
            {
                var p = new Point(i * step, Y(values[i]));
                l.LineTo(p, true, true);
                a.LineTo(p, false, false);
            }
            a.LineTo(new Point(w, h), false, false);
        }
        line.Freeze();
        area.Freeze();

        var color = (Stroke as SolidColorBrush)?.Color ?? Colors.Turquoise;
        var fill = new LinearGradientBrush(Color.FromArgb(70, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B), 90);
        fill.Freeze();
        dc.DrawGeometry(fill, null, area);
        var stroke = new Pen(Stroke, 1.6) { LineJoin = PenLineJoin.Round };
        stroke.Freeze();
        dc.DrawGeometry(null, stroke, line);
    }
}
