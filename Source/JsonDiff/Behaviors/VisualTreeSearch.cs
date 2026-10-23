using System.Windows;
using System.Windows.Media;

namespace JsonDiff.Behaviors;

internal static class VisualTreeSearch
{
    /// <summary>The first descendant of <paramref name="root"/> of type <typeparamref name="T"/>, breadth first.</summary>
    public static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var queue = new Queue<DependencyObject>([root]);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is T match)
                return match;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++)
                queue.Enqueue(VisualTreeHelper.GetChild(current, i));
        }
        return null;
    }
}
