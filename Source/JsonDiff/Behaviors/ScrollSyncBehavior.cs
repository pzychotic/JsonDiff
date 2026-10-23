using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;

namespace JsonDiff.Behaviors;

/// <summary>
/// Keeps the vertical offset of the first <see cref="ScrollViewer"/> inside the associated element in sync with
/// every other element that uses the same <see cref="Group"/>. Horizontal scrolling stays independent, since the
/// two sides' line widths differ.
/// </summary>
public sealed class ScrollSyncBehavior : Behavior<FrameworkElement>
{
    private static readonly Dictionary<string, List<ScrollSyncBehavior>> Groups = [];

    public static readonly DependencyProperty GroupProperty =
        DependencyProperty.Register(
            nameof(Group),
            typeof(string),
            typeof(ScrollSyncBehavior),
            new PropertyMetadata(null));

    public string? Group
    {
        get => (string?)GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    private ScrollViewer? _viewer;
    private string? _registeredGroup;

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
        if (_registeredGroup is not null && Groups.TryGetValue(_registeredGroup, out var members))
            members.Remove(this);
        _viewer = null;
        base.OnDetaching();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (Group is not { } group || VisualTreeSearch.FindDescendant<ScrollViewer>(AssociatedObject) is not { } viewer)
            return;

        AssociatedObject.LayoutUpdated -= OnLayoutUpdated;
        _viewer = viewer;
        _viewer.ScrollChanged += OnScrollChanged;
        _registeredGroup = group;
        if (!Groups.TryGetValue(group, out var members))
            Groups[group] = members = [];
        members.Add(this);
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange == 0 || _viewer is null || _registeredGroup is null)
            return;

        // Setting a partner's offset raises its ScrollChanged later; it then finds the offsets already equal.
        foreach (var member in Groups[_registeredGroup])
        {
            if (member != this && member._viewer is { } other && other.VerticalOffset != _viewer.VerticalOffset)
                other.ScrollToVerticalOffset(_viewer.VerticalOffset);
        }
    }
}
