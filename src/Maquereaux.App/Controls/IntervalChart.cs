using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Maquereaux.Core.Lab;
using Maquereaux.Core.Stats;

namespace Maquereaux.App.Controls;

/// <summary>
/// Graphique « intervalle entre deux appuis au fil du temps », un nuage de points par source.
/// <para>
/// Fait maison (~150 lignes, dessin direct via <see cref="DrawingContext"/>) plutôt qu'une lib (ScottPlot,
/// LiveCharts) : quelques milliers de points suffisent, zéro dépendance, et l'exe single-file ne grossit pas.
/// </para>
/// </summary>
public sealed class IntervalChart : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<IntervalPoint>), typeof(IntervalChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DurationMsProperty = DependencyProperty.Register(
        nameof(DurationMs), typeof(double), typeof(IntervalChart),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(IntervalChart),
        new FrameworkPropertyMetadata("Aucune donnée", FrameworkPropertyMetadataOptions.AffectsRender));

    private const double Left = 52, Bottom = 24, Top = 22, Right = 10;

    public IntervalChart()
    {
        // Redessine quand le thème change (les couleurs viennent des ressources)
        ThemeEvents.Changed += InvalidateVisual;
        Unloaded += (_, _) => ThemeEvents.Changed -= InvalidateVisual;
        Loaded += (_, _) => { ThemeEvents.Changed -= InvalidateVisual; ThemeEvents.Changed += InvalidateVisual; };
    }

    public IReadOnlyList<IntervalPoint>? Points
    {
        get => (IReadOnlyList<IntervalPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public double DurationMs
    {
        get => (double)GetValue(DurationMsProperty);
        set => SetValue(DurationMsProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    private Brush Res(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 80 || h < 60) return;
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var bg = Res("Brush.Background", Brushes.Black);
        var grid = new Pen(Res("Brush.Chart.Grid", Brushes.DimGray), 1);
        var text = Res("Brush.TextMuted", Brushes.Gray);
        dc.DrawRoundedRectangle(bg, null, new Rect(0, 0, w, h), 6, 6);

        var plot = new Rect(Left, Top, Math.Max(1, w - Left - Right), Math.Max(1, h - Top - Bottom));
        var pts = Points;
        if (pts is null || pts.Count == 0)
        {
            DrawText(dc, EmptyText, new Point(w / 2, h / 2), text, 13, pixelsPerDip, TextAlignment.Center);
            return;
        }

        // Échelle Y : jusqu'au p99 × 1,3 (les gros pics isolés ne écrasent pas le reste) ; les points au-delà sont posés en haut.
        var p99 = TimingStats.Percentile(pts.Select(p => p.IntervalMs).OrderBy(v => v).ToArray(), 99);
        double yMax = NiceCeiling(Math.Max(1, p99 * 1.3));
        double xMax = Math.Max(DurationMs, pts[^1].TimeMs);
        if (xMax <= 0) xMax = 1;

        // Grille horizontale + graduations ms
        for (int i = 0; i <= 4; i++)
        {
            double v = yMax * i / 4;
            double y = plot.Bottom - plot.Height * i / 4;
            dc.DrawLine(grid, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(dc, $"{v.ToString(v < 10 ? "0.#" : "0", CultureInfo.CurrentCulture)} ms", new Point(plot.Left - 6, y - 7), text, 10, pixelsPerDip, TextAlignment.Right);
        }
        // Graduations temps (s)
        for (int i = 0; i <= 5; i++)
        {
            double t = xMax * i / 5;
            double x = plot.Left + plot.Width * i / 5;
            DrawText(dc, (t / 1000).ToString("0.#", CultureInfo.CurrentCulture) + " s", new Point(x, plot.Bottom + 5), text, 10, pixelsPerDip, TextAlignment.Center);
        }

        // Points, une couleur par source
        var brushes = new Dictionary<InputSource, Brush>
        {
            [InputSource.Physical] = Res("Brush.Chart.Physical", Brushes.DodgerBlue),
            [InputSource.Maquereaux] = Res("Brush.Chart.Injected", Brushes.MediumSeaGreen),
            [InputSource.OtherInjected] = Res("Brush.Paused", Brushes.Orange),
        };
        double size = pts.Count > 3000 ? 2 : 3;
        foreach (var group in pts.GroupBy(p => p.Source))
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                foreach (var p in group)
                {
                    double x = plot.Left + plot.Width * Math.Clamp(p.TimeMs / xMax, 0, 1);
                    double y = plot.Bottom - plot.Height * Math.Min(p.IntervalMs / yMax, 1);
                    ctx.BeginFigure(new Point(x - size / 2, y - size / 2), true, true);
                    ctx.PolyLineTo(new[] { new Point(x + size / 2, y - size / 2), new Point(x + size / 2, y + size / 2), new Point(x - size / 2, y + size / 2) }, false, false);
                }
            }
            geo.Freeze();
            dc.DrawGeometry(brushes[group.Key], null, geo);
        }

        // Légende
        double lx = plot.Right;
        foreach (var src in pts.Select(p => p.Source).Distinct().OrderByDescending(s => s))
        {
            var label = SessionAnalyzer.Describe(src);
            var ft = MakeText(label, text, 11, pixelsPerDip);
            lx -= ft.Width + 22;
            dc.DrawEllipse(brushes[src], null, new Point(lx + 5, Top / 2), 4, 4);
            dc.DrawText(ft, new Point(lx + 12, Top / 2 - ft.Height / 2));
        }
    }

    /// <summary>Arrondit vers le haut à une valeur « ronde » (1, 2, 2.5, 5 × 10^n) pour des graduations lisibles.</summary>
    private static double NiceCeiling(double v)
    {
        double exp = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (var m in new[] { 1, 2, 2.5, 5, 10 })
            if (m * exp >= v) return m * exp;
        return 10 * exp;
    }

    private FormattedText MakeText(string s, Brush brush, double size, double pixelsPerDip) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, pixelsPerDip);

    private void DrawText(DrawingContext dc, string s, Point at, Brush brush, double size, double pixelsPerDip, TextAlignment align)
    {
        var ft = MakeText(s, brush, size, pixelsPerDip);
        ft.TextAlignment = align;
        dc.DrawText(ft, at);
    }
}

/// <summary>Signal « le thème vient de changer » (pour les contrôles qui dessinent eux-mêmes).</summary>
public static class ThemeEvents
{
    public static event Action? Changed;
    public static void Raise() => Changed?.Invoke();
}
