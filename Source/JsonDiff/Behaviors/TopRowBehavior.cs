using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace JsonDiff.Behaviors;

/// <summary>
/// Exposes the vertical position of the first <see cref="ScrollViewer"/> inside the associated element as rows, for a
/// viewer that scrolls by item (<c>CanContentScroll</c>): <see cref="TopRow"/> follows scrolling and setting it
/// scrolls; <see cref="ViewportRows"/> reports how many whole rows fit.
/// </summary>
public sealed class TopRowBehavior : Behavior<FrameworkElement>
{
    public static readonly DependencyProperty TopRowProperty =
        DependencyProperty.Register(
            nameof(TopRow),
            typeof(int),
            typeof(TopRowBehavior),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTopRowChanged));

    public static readonly DependencyProperty ViewportRowsProperty =
        DependencyProperty.Register(
            nameof(ViewportRows),
            typeof(int),
            typeof(TopRowBehavior),
            new FrameworkPropertyMetadata(0) { DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

    public int TopRow
    {
        get => (int)GetValue(TopRowProperty);
        set => SetValue(TopRowProperty, value);
    }

    public int ViewportRows
    {
        get => (int)GetValue(ViewportRowsProperty);
        set => SetValue(ViewportRowsProperty, value);
    }

    private ScrollViewer? _viewer;

    protected override void OnAttached()
    {
        base.OnAttached();
        // The ScrollViewer lives in a template that may not be applied yet, so retry after each layout pass.
        AssociatedObject.LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.LayoutUpdated -= OnLayoutUpdated;
        if (_viewer is not null)
            _viewer.ScrollChanged -= OnScrollChanged;
        _viewer = null;
        base.OnDetaching();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (VisualTreeSearch.FindDescendant<ScrollViewer>(AssociatedObject) is not { } viewer)
            return;

        AssociatedObject.LayoutUpdated -= OnLayoutUpdated;
        _viewer = viewer;
        _viewer.ScrollChanged += OnScrollChanged;
        _viewer.ScrollToVerticalOffset(TopRow);
    }

    private static void OnTopRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TopRowBehavior { _viewer: { } viewer } && viewer.VerticalOffset != (int)e.NewValue)
            viewer.ScrollToVerticalOffset((int)e.NewValue);
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_viewer is null)
            return;

        ViewportRows = (int)_viewer.ViewportHeight;
        // New items may reset the offset; the row asked for wins then (the viewer clamps it, and reports that back).
        if (e.ExtentHeightChange != 0 && (int)_viewer.VerticalOffset != TopRow)
            _viewer.ScrollToVerticalOffset(TopRow);
        else
            TopRow = (int)_viewer.VerticalOffset;
    }
}
