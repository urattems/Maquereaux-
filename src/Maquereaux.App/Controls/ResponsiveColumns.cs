using System.Windows;
using System.Windows.Controls;

namespace Maquereaux.App.Controls;

/// <summary>
/// Deux colonnes côte à côte (proportions <see cref="LeftRatio"/>) quand la place le permet, empilées sinon.
/// Plus simple et plus fiable qu'un VisualStateManager pour une mise en page « responsive » en WPF.
/// </summary>
public sealed class ResponsiveColumns : Panel
{
    public static readonly DependencyProperty BreakpointProperty = DependencyProperty.Register(
        nameof(Breakpoint), typeof(double), typeof(ResponsiveColumns),
        new FrameworkPropertyMetadata(820.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty LeftRatioProperty = DependencyProperty.Register(
        nameof(LeftRatio), typeof(double), typeof(ResponsiveColumns),
        new FrameworkPropertyMetadata(0.6, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(ResponsiveColumns),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double Breakpoint { get => (double)GetValue(BreakpointProperty); set => SetValue(BreakpointProperty, value); }
    public double LeftRatio { get => (double)GetValue(LeftRatioProperty); set => SetValue(LeftRatioProperty, value); }
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    private bool IsWide(double width) => !double.IsInfinity(width) && width >= Breakpoint;

    protected override Size MeasureOverride(Size available)
    {
        var kids = InternalChildren.Cast<UIElement>().Take(2).ToArray();
        if (kids.Length == 0) return default;
        if (kids.Length == 1 || !IsWide(available.Width))
        {
            double h = 0, w = 0;
            foreach (var k in kids)
            {
                k.Measure(new Size(available.Width, double.PositiveInfinity));
                h += k.DesiredSize.Height;
                w = Math.Max(w, k.DesiredSize.Width);
            }
            return new Size(double.IsInfinity(available.Width) ? w : available.Width, h + (kids.Length - 1) * Gap);
        }
        var (lw, rw) = Split(available.Width);
        kids[0].Measure(new Size(lw, available.Height));
        kids[1].Measure(new Size(rw, available.Height));
        return new Size(available.Width, Math.Max(kids[0].DesiredSize.Height, kids[1].DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size final)
    {
        var kids = InternalChildren.Cast<UIElement>().Take(2).ToArray();
        if (kids.Length == 0) return final;
        if (kids.Length == 1 || !IsWide(final.Width))
        {
            double y = 0;
            foreach (var k in kids)
            {
                k.Arrange(new Rect(0, y, final.Width, k.DesiredSize.Height));
                y += k.DesiredSize.Height + Gap;
            }
            return final;
        }
        var (lw, rw) = Split(final.Width);
        kids[0].Arrange(new Rect(0, 0, lw, final.Height));
        kids[1].Arrange(new Rect(lw + Gap, 0, rw, final.Height));
        return final;
    }

    private (double Left, double Right) Split(double width)
    {
        var usable = Math.Max(0, width - Gap);
        var left = Math.Round(usable * LeftRatio);
        return (left, usable - left);
    }
}
