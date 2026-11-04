namespace JsonDiff.Models;

/// <summary>
/// Notifier that view-layer behaviors subscribe to in order to move keyboard
/// focus to an element, even if it already has focus.
/// </summary>
public sealed class FocusRequestNotifier
{
    public event Action? FocusRequested;

    public void Request() => FocusRequested?.Invoke();
}
