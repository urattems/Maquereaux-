using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Maquereaux.Core.Lab;
using Maquereaux.Core.Stats;

namespace Maquereaux.App.Controls;

/// <summary>Vue du graphique du Labo.</summary>
public enum ChartMode
{
    /// <summary>Intervalle entre deux appuis au fil du temps (un point par appui).</summary>
    Scatter,

    /// <summary>Répartition des intervalles (combien d'appuis à 10 ms, 11 ms…).</summary>
    Histogram,
}

/// <summary>
/// Graphique du Labo, fait maison (dessin direct via <see cref="DrawingContext"/>, zéro dépendance) :
/// <list type="bullet">
/// <item>nuage de points par source, ligne de l'intervalle cible, bande p5–p95 (là où tombent 90 % des appuis) ;</item>
/// <item>info-bulle au survol (point le plus proche) ;</item>
/// <item>zoom Ctrl+molette autour du curseur, glisser pour défiler, double-clic pour tout revoir ;</item>
/// <item>mode histogramme.</item>
/// </list>
/// Le fond (grille + points) est mis en cache : survoler ne redessine que l'info-bulle.
/// </summary>
public sealed class IntervalChart : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<IntervalPoint>), typeof(IntervalChart),
        new FrameworkPropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty DurationMsProperty = DependencyProperty.Register(
        nameof(DurationMs), typeof(double), typeof(IntervalChart), new FrameworkPropertyMetadata(0.0, OnDataChanged));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(IntervalChart), new FrameworkPropertyMetadata("Aucune donnée", OnVisualChanged));

    /// <summary>Intervalle visé (ms), 0 = pas de ligne cible.</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target), typeof(double), typeof(IntervalChart), new FrameworkPropertyMetadata(0.0, OnVisualChanged));

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(ChartMode), typeof(IntervalChart), new FrameworkPropertyMetadata(ChartMode.Scatter, OnDataChanged));

    /// <summary>Zoom, défilement et info-bulle (désactivés pour les petits graphiques de comparaison).</summary>
    public static readonly DependencyProperty InteractiveProperty = DependencyProperty.Register(
        nameof(Interactive), typeof(bool), typeof(IntervalChart), new FrameworkPropertyMetadata(true));

    private const double Left = 56, Bottom = 26, Top = 26, Right = 12;

    // Fenêtre de temps visible (zoom) ; null = toute la session
    private double? _viewStart, _viewEnd;
    private Point? _hover;
    private Point _dragFrom;
    private double _dragViewStart, _dragViewEnd;
    private bool _dragging;
    private Drawing? _cache;
    private Size _cacheSize;
    private (double yMax, double xStart, double xEnd, Rect plot) _frame;

    public IntervalChart()
    {
        ClipToBounds = true;
        Focusable = false;
        Cursor = Cursors.Cross;
        // Redessine quand le thème change (les couleurs viennent des ressources)
        Loaded += (_, _) => { ThemeEvents.Changed -= Invalidate; ThemeEvents.Changed += Invalidate; };
        Unloaded += (_, _) => ThemeEvents.Changed -= Invalidate;
    }

    public IReadOnlyList<IntervalPoint>? Points { get => (IReadOnlyList<IntervalPoint>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public double DurationMs { get => (double)GetValue(DurationMsProperty); set => SetValue(DurationMsProperty, value); }
    public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    public double Target { get => (double)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public ChartMode Mode { get => (ChartMode)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public bool Interactive { get => (bool)GetValue(InteractiveProperty); set => SetValue(InteractiveProperty, value); }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (IntervalChart)d;
        c._viewStart = c._viewEnd = null;
        c.Invalidate();
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((IntervalChart)d).Invalidate();

    private void Invalidate()
    {
        _cache = null;
        InvalidateVisual();
    }

    private bool IsZoomed => _viewStart is not null;

    // ------------------------------------------------------------------ Souris : zoom, défilement, survol

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        // Sans Ctrl, la molette fait défiler la page (on ne la confisque pas)
        if (!Interactive || Mode != ChartMode.Scatter || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Points is not { Count: > 1 }) return;
        var (_, xs, xe, plot) = _frame;
        if (plot.Width <= 0) return;
        double at = xs + (xe - xs) * Math.Clamp((e.GetPosition(this).X - plot.Left) / plot.Width, 0, 1);
        double factor = e.Delta > 0 ? 0.75 : 1 / 0.75;
        double full = FullEnd();
        double span = Math.Clamp((xe - xs) * factor, Math.Min(200, full), full);
        double start = at - (at - xs) * span / (xe - xs);
        SetView(start, start + span);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!Interactive) return;
        if (e.ClickCount == 2)
        {
            _viewStart = _viewEnd = null;
            Invalidate();
            return;
        }
        if (!IsZoomed) return;
        _dragging = CaptureMouse();
        _dragFrom = e.GetPosition(this);
        (_dragViewStart, _dragViewEnd) = (_viewStart!.Value, _viewEnd!.Value);
        Cursor = Cursors.SizeWE;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        Cursor = Cursors.Cross;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!Interactive) return;
        var pos = e.GetPosition(this);
        if (_dragging && _frame.plot.Width > 0)
        {
            double msPerPx = (_dragViewEnd - _dragViewStart) / _frame.plot.Width;
            double shift = (_dragFrom.X - pos.X) * msPerPx;
            SetView(_dragViewStart + shift, _dragViewEnd + shift);
            return;
        }
        _hover = pos;
        InvalidateVisual(); // le fond reste en cache : seule l'info-bulle est redessinée
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        InvalidateVisual();
    }

    private double FullEnd()
    {
        var pts = Points;
        double end = Math.Max(DurationMs, pts is { Count: > 0 } ? pts[^1].TimeMs : 0);
        return end <= 0 ? 1 : end;
    }

    private void SetView(double start, double end)
    {
        double full = FullEnd();
        double span = end - start;
        if (span >= full - 1e-6)
        {
            _viewStart = _viewEnd = null;
        }
        else
        {
            start = Math.Clamp(start, 0, full - span);
            (_viewStart, _viewEnd) = (start, start + span);
        }
        Invalidate();
    }

    // ------------------------------------------------------------------ Dessin

    private Brush Res(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    private Brush SourceBrush(InputSource s) => s switch
    {
        InputSource.Physical => Res("Source.Physical", Brushes.DodgerBlue),
        InputSource.Maquereaux => Res("Source.Maquereaux", Brushes.Turquoise),
        _ => Res("Source.Other", Brushes.Orange),
    };

    protected override void OnRender(DrawingContext dc)
    {
        var size = new Size(ActualWidth, ActualHeight);
        if (size.Width < 80 || size.Height < 60) return;
        if (_cache is null || _cacheSize != size)
        {
            var group = new DrawingGroup();
            using (var ctx = group.Open()) RenderBase(ctx, size);
            group.Freeze();
            _cache = group;
            _cacheSize = size;
        }
        dc.DrawDrawing(_cache);
        if (_hover is { } h && !_dragging) RenderHover(dc, h);
    }

    private void RenderBase(DrawingContext dc, Size size)
    {
        double w = size.Width, h = size.Height;
        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var text = Res("Text.Tertiary", Brushes.Gray);
        var gridPen = new Pen(Res("Chart.Grid", Brushes.DimGray), 1);
        gridPen.Freeze();
        dc.DrawRoundedRectangle(Res("Surface.Sunken", Brushes.Black), null, new Rect(0, 0, w, h), 8, 8);

        var plot = new Rect(Left, Top, Math.Max(1, w - Left - Right), Math.Max(1, h - Top - Bottom));
        var pts = Points;
        if (pts is null || pts.Count == 0)
        {
            _frame = default;
            DrawText(dc, EmptyText, new Point(w / 2, h / 2 - 8), text, 13, ppd, TextAlignment.Center, mono: false);
            return;
        }

        var sorted = pts.Select(p => p.IntervalMs).OrderBy(v => v).ToArray();
        double p5 = TimingStats.Percentile(sorted, 5), p95 = TimingStats.Percentile(sorted, 95), p99 = TimingStats.Percentile(sorted, 99);
        // Échelle : jusqu'au p99 × 1,3 (un pic isolé n'écrase pas le reste) ; au-delà, les points sont posés en haut
        double yMax = NiceCeiling(Math.Max(Math.Max(1, p99 * 1.3), Target * 1.3));

        if (Mode == ChartMode.Histogram)
            RenderHistogram(dc, plot, pts, yMax, text, gridPen, ppd);
        else
            RenderScatter(dc, plot, pts, yMax, p5, p95, text, gridPen, ppd);
        RenderLegend(dc, plot, pts, text, ppd);
    }

    private void RenderScatter(DrawingContext dc, Rect plot, IReadOnlyList<IntervalPoint> pts, double yMax, double p5, double p95,
                               Brush text, Pen gridPen, double ppd)
    {
        double xs = _viewStart ?? 0, xe = _viewEnd ?? FullEnd();
        _frame = (yMax, xs, xe, plot);
        double Y(double v) => plot.Bottom - plot.Height * Math.Min(v / yMax, 1);

        // Bande p5–p95 : 90 % des appuis tombent dedans (plus elle est fine, plus le rythme est régulier)
        var band = new Rect(plot.Left, Y(p95), plot.Width, Math.Max(1, Y(p5) - Y(p95)));
        dc.DrawRectangle(Res("Chart.Band", Brushes.DarkSlateGray), null, band);
        DrawText(dc, "p5–p95", new Point(plot.Right - 4, band.Top + 2), text, 10, ppd, TextAlignment.Right);

        for (int i = 0; i <= 4; i++)
        {
            double v = yMax * i / 4, y = plot.Bottom - plot.Height * i / 4;
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(dc, Ms(v), new Point(plot.Left - 8, y - 7), text, 10, ppd, TextAlignment.Right);
        }
        for (int i = 0; i <= 5; i++)
        {
            double t = xs + (xe - xs) * i / 5, x = plot.Left + plot.Width * i / 5;
            DrawText(dc, (t / 1000).ToString(xe - xs < 5000 ? "0.00" : "0.#", CultureInfo.CurrentCulture) + " s",
                     new Point(x, plot.Bottom + 6), text, 10, ppd, TextAlignment.Center);
        }

        RenderTarget(dc, plot, Y(Target), text, ppd);

        double dot = pts.Count > 3000 && !IsZoomed ? 2.2 : 3.2;
        dc.PushClip(new RectangleGeometry(new Rect(plot.Left - dot, plot.Top - dot, plot.Width + 2 * dot, plot.Height + 2 * dot)));
        foreach (var group in pts.GroupBy(p => p.Source))
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                foreach (var p in group)
                {
                    if (p.TimeMs < xs || p.TimeMs > xe) continue;
                    double x = plot.Left + plot.Width * (p.TimeMs - xs) / (xe - xs);
                    double y = Y(p.IntervalMs);
                    ctx.BeginFigure(new Point(x - dot / 2, y - dot / 2), true, true);
                    ctx.PolyLineTo(new[] { new Point(x + dot / 2, y - dot / 2), new Point(x + dot / 2, y + dot / 2), new Point(x - dot / 2, y + dot / 2) }, false, false);
                }
            }
            geo.Freeze();
            dc.DrawGeometry(SourceBrush(group.Key), null, geo);
        }
        dc.Pop();

        if (IsZoomed)
            DrawText(dc, "Zoom · glisser pour défiler · double-clic pour tout voir", new Point(plot.Left + 6, plot.Top + 4), text, 10, ppd, TextAlignment.Left, mono: false);
    }

    private void RenderHistogram(DrawingContext dc, Rect plot, IReadOnlyList<IntervalPoint> pts, double xMaxMs,
                                 Brush text, Pen gridPen, double ppd)
    {
        // Axe X = intervalle (ms), axe Y = nombre d'appuis ; une série de barres par source, côte à côte
        const int bins = 40;
        var sources = pts.Select(p => p.Source).Distinct().OrderBy(s => s).ToList();
        var counts = sources.ToDictionary(s => s, _ => new int[bins]);
        foreach (var p in pts)
            counts[p.Source][Math.Clamp((int)(p.IntervalMs / xMaxMs * bins), 0, bins - 1)]++;
        int maxCount = Math.Max(1, counts.Values.SelectMany(c => c).Max());
        double yMax = NiceCeiling(maxCount * 1.1);
        _frame = (yMax, 0, xMaxMs, plot);

        for (int i = 0; i <= 4; i++)
        {
            double v = yMax * i / 4, y = plot.Bottom - plot.Height * i / 4;
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(dc, v.ToString("0", CultureInfo.CurrentCulture), new Point(plot.Left - 8, y - 7), text, 10, ppd, TextAlignment.Right);
        }
        for (int i = 0; i <= 5; i++)
            DrawText(dc, Ms(xMaxMs * i / 5), new Point(plot.Left + plot.Width * i / 5, plot.Bottom + 6), text, 10, ppd, TextAlignment.Center);

        double binW = plot.Width / bins;
        double barW = Math.Max(1, (binW - 2) / sources.Count);
        for (int si = 0; si < sources.Count; si++)
        {
            var brush = SourceBrush(sources[si]);
            var c = counts[sources[si]];
            for (int b = 0; b < bins; b++)
            {
                if (c[b] == 0) continue;
                double hgt = plot.Height * c[b] / yMax;
                dc.DrawRoundedRectangle(brush, null, new Rect(plot.Left + b * binW + 1 + si * barW, plot.Bottom - hgt, barW, hgt), 1.5, 1.5);
            }
        }

        // Cible : verticale dans ce mode
        if (Target > 0 && Target <= xMaxMs)
        {
            double x = plot.Left + plot.Width * Target / xMaxMs;
            var pen = new Pen(Res("Chart.Target", Brushes.White), 1.5) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
            pen.Freeze();
            dc.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            DrawText(dc, "cible " + Ms(Target), new Point(x + 4, plot.Top + 2), Res("Chart.Target", text), 10, ppd, TextAlignment.Left);
        }
    }

    private void RenderTarget(DrawingContext dc, Rect plot, double y, Brush text, double ppd)
    {
        if (Target <= 0) return;
        var brush = Res("Chart.Target", Brushes.White);
        var pen = new Pen(brush, 1.5) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
        pen.Freeze();
        dc.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
        DrawText(dc, "cible " + Ms(Target), new Point(plot.Left + 6, y - 15), brush, 10, ppd, TextAlignment.Left);
    }

    private void RenderLegend(DrawingContext dc, Rect plot, IReadOnlyList<IntervalPoint> pts, Brush text, double ppd)
    {
        double lx = plot.Right;
        foreach (var src in pts.Select(p => p.Source).Distinct().OrderByDescending(s => s))
        {
            var ft = MakeText(SessionAnalyzer.Describe(src), Res("Text.Secondary", text), 11, ppd, mono: false);
            lx -= ft.Width + 24;
            dc.DrawEllipse(SourceBrush(src), null, new Point(lx + 5, Top / 2), 4, 4);
            dc.DrawText(ft, new Point(lx + 13, Top / 2 - ft.Height / 2));
        }
    }

    /// <summary>Info-bulle du point le plus proche (mode nuage) ou de la barre survolée (histogramme).</summary>
    private void RenderHover(DrawingContext dc, Point h)
    {
        var pts = Points;
        var (yMax, xs, xe, plot) = _frame;
        if (pts is not { Count: > 0 } || !plot.Contains(h) || xe <= xs) return;
        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        string line1, line2;
        Point anchor;
        Brush accent;

        if (Mode == ChartMode.Scatter)
        {
            IntervalPoint? best = null;
            double bestD = 18 * 18;
            foreach (var p in pts)
            {
                if (p.TimeMs < xs || p.TimeMs > xe) continue;
                double x = plot.Left + plot.Width * (p.TimeMs - xs) / (xe - xs);
                double y = plot.Bottom - plot.Height * Math.Min(p.IntervalMs / yMax, 1);
                double d = (x - h.X) * (x - h.X) + (y - h.Y) * (y - h.Y);
                if (d < bestD) { bestD = d; best = p; anchor = new Point(x, y); }
            }
            if (best is not { } bp) return;
            anchor = new Point(plot.Left + plot.Width * (bp.TimeMs - xs) / (xe - xs), plot.Bottom - plot.Height * Math.Min(bp.IntervalMs / yMax, 1));
            accent = SourceBrush(bp.Source);
            line1 = $"{bp.IntervalMs.ToString("0.000", CultureInfo.CurrentCulture)} ms";
            line2 = $"{SessionAnalyzer.Describe(bp.Source)} · à {(bp.TimeMs / 1000).ToString("0.000", CultureInfo.CurrentCulture)} s";
            dc.DrawEllipse(null, new Pen(accent, 2), anchor, 5, 5);
        }
        else
        {
            const int bins = 40;
            int b = Math.Clamp((int)((h.X - plot.Left) / plot.Width * bins), 0, bins - 1);
            double lo = xe * b / bins, hi = xe * (b + 1) / bins;
            int n = pts.Count(p => p.IntervalMs >= lo && p.IntervalMs < hi);
            accent = Res("Accent", Brushes.Turquoise);
            line1 = $"{n} appui{(n > 1 ? "s" : "")}";
            line2 = $"entre {Ms(lo)} et {Ms(hi)}";
            anchor = h;
        }

        var t1 = MakeText(line1, Res("Text.Primary", Brushes.White), 12, ppd, mono: true);
        t1.SetFontWeight(FontWeights.SemiBold);
        var t2 = MakeText(line2, Res("Text.Secondary", Brushes.Gray), 11, ppd, mono: false);
        double bw = Math.Max(t1.Width, t2.Width) + 20, bh = t1.Height + t2.Height + 14;
        double bx = anchor.X + 12, by = anchor.Y - bh - 8;
        if (bx + bw > ActualWidth - 4) bx = anchor.X - bw - 12;
        if (by < 4) by = anchor.Y + 12;
        var pen = new Pen(Res("Border.Default", Brushes.Gray), 1);
        dc.DrawRoundedRectangle(Res("Surface.Elevated", Brushes.Black), pen, new Rect(bx, by, bw, bh), 8, 8);
        dc.DrawRoundedRectangle(accent, null, new Rect(bx, by + 8, 3, bh - 16), 1.5, 1.5);
        dc.DrawText(t1, new Point(bx + 12, by + 6));
        dc.DrawText(t2, new Point(bx + 12, by + 8 + t1.Height));
    }

    private static string Ms(double v) => v.ToString(v < 10 ? "0.#" : "0", CultureInfo.CurrentCulture) + " ms";

    /// <summary>Arrondit vers le haut à une valeur « ronde » (1, 2, 2.5, 5 × 10^n) pour des graduations lisibles.</summary>
    internal static double NiceCeiling(double v)
    {
        if (v <= 0) return 1;
        double exp = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (var m in new[] { 1, 2, 2.5, 5, 10 })
            if (m * exp >= v - 1e-9) return m * exp;
        return 10 * exp;
    }

    private FormattedText MakeText(string s, Brush brush, double size, double ppd, bool mono = true)
    {
        var family = TryFindResource(mono ? "Font.Mono" : "Font.UI") as FontFamily ?? new FontFamily("Segoe UI");
        return new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, brush, ppd);
    }

    private void DrawText(DrawingContext dc, string s, Point at, Brush brush, double size, double ppd, TextAlignment align, bool mono = true)
    {
        var ft = MakeText(s, brush, size, ppd, mono);
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
