using JsonDiff.Models;
using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace JsonDiff.Behaviors;

/// <summary>
/// Focuses the associated text box and selects its text when a <see cref="FocusRequestNotifier"/> asks for it.
/// </summary>
public sealed class FocusRequestBehavior : Behavior<TextBox>
{
    public static readonly DependencyProperty NotifierProperty =
        DependencyProperty.Register(
            nameof(Notifier),
            typeof(FocusRequestNotifier),
            typeof(FocusRequestBehavior),
            new PropertyMetadata(null, OnNotifierChanged));

    public FocusRequestNotifier? Notifier
    {
        get => (FocusRequestNotifier?)GetValue(NotifierProperty);
        set => SetValue(NotifierProperty, value);
    }

    private static void OnNotifierChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FocusRequestBehavior behavior || behavior.AssociatedObject == null)
            return;

        if (e.OldValue is FocusRequestNotifier oldNotifier)
            oldNotifier.FocusRequested -= behavior.OnFocusRequested;
        if (e.NewValue is FocusRequestNotifier newNotifier)
            newNotifier.FocusRequested += behavior.OnFocusRequested;
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (Notifier != null)
            Notifier.FocusRequested += OnFocusRequested;
    }

    protected override void OnDetaching()
    {
        if (Notifier != null)
            Notifier.FocusRequested -= OnFocusRequested;
        base.OnDetaching();
    }

    private void OnFocusRequested()
    {
        // The request may have just made the text box visible (e.g. by opening a collapsed bar),
        // and it cannot take focus before it is laid out.
        AssociatedObject.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            AssociatedObject?.Focus();
            AssociatedObject?.SelectAll();
        });
    }
}
