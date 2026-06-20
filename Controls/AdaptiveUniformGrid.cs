using System;
using System.Windows;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace

namespace Toolkit.Controls;

public class AdaptiveUniformGrid : UniformGrid
{
    public double ItemMinWidth
    {
        get => (double)GetValue(ItemMinWidthProperty);
        set => SetValue(ItemMinWidthProperty, value);
    }

    public static readonly DependencyProperty ItemMinWidthProperty =
        DependencyProperty.Register(nameof(ItemMinWidth), typeof(double), typeof(AdaptiveUniformGrid),
            new PropertyMetadata(240.0, OnItemMinWidthChanged));

    private static void OnItemMinWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AdaptiveUniformGrid grid)
            grid.InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (constraint.Width > 0 && constraint.Width < double.PositiveInfinity && ItemMinWidth > 0)
        {
            int computed = Math.Max(1, (int)Math.Floor(constraint.Width / ItemMinWidth));
            if (computed != Columns)
                Columns = computed;
        }
        return base.MeasureOverride(constraint);
    }
}
