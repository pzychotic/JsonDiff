using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using JsonDiff.Models;

namespace JsonDiff.Behaviors;

/// <summary>
/// Shows a row's text in a <see cref="TextBlock"/> with its search hits highlighted: set <c>Line</c> to the row and
/// <c>Highlights</c> to its pane's hits. The colors are the theme's <c>SearchMatchBrush</c>, <c>SearchCurrentBrush</c>
/// and <c>SearchCurrentTextBrush</c>.
/// </summary>
public static class SearchHighlight
{
    public static readonly DependencyProperty LineProperty =
        DependencyProperty.RegisterAttached("Line", typeof(DiffLine), typeof(SearchHighlight),
            new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty HighlightsProperty =
        DependencyProperty.RegisterAttached("Highlights", typeof(SearchHighlights), typeof(SearchHighlight),
            new PropertyMetadata(null, OnChanged));

    public static DiffLine? GetLine(TextBlock element) => (DiffLine?)element.GetValue(LineProperty);

    public static void SetLine(TextBlock element, DiffLine? value) => element.SetValue(LineProperty, value);

    public static SearchHighlights? GetHighlights(TextBlock element) =>
        (SearchHighlights?)element.GetValue(HighlightsProperty);

    public static void SetHighlights(TextBlock element, SearchHighlights? value) =>
        element.SetValue(HighlightsProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock)
            return;

        var line = GetLine(textBlock);
        var text = line?.Text ?? "";
        var highlights = line is not null && GetHighlights(textBlock) is { } all ? all.For(line) : [];
        if (highlights.Count == 0)
        {
            textBlock.Text = text;
            return;
        }

        textBlock.Inlines.Clear();
        var position = 0;
        foreach (var highlight in highlights)
        {
            if (highlight.Start > position)
                textBlock.Inlines.Add(new Run(text[position..highlight.Start]));

            var run = new Run(text.Substring(highlight.Start, highlight.Length));
            run.SetResourceReference(TextElement.BackgroundProperty,
                highlight.IsCurrent ? "SearchCurrentBrush" : "SearchMatchBrush");
            if (highlight.IsCurrent)
                run.SetResourceReference(TextElement.ForegroundProperty, "SearchCurrentTextBrush");
            textBlock.Inlines.Add(run);
            position = highlight.Start + highlight.Length;
        }
        if (position < text.Length)
            textBlock.Inlines.Add(new Run(text[position..]));
    }
}
