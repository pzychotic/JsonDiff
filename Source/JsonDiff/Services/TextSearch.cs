using System.Text.RegularExpressions;
using JsonDiff.Models;

namespace JsonDiff.Services;

/// <summary>What to look for: plain text, optionally case-sensitive and only as a whole word.</summary>
public sealed record SearchQuery(string Text, bool MatchCase = false, bool WholeWord = false);

/// <summary>A hit of a search: <see cref="Length"/> characters from <see cref="Start"/> in the text of row <see cref="Row"/>.</summary>
public readonly record struct SearchMatch(int Row, int Start, int Length);

/// <summary>Finds the text of a <see cref="SearchQuery"/> in the rows of a pane.</summary>
public static class TextSearch
{
    /// <summary>
    /// The non-overlapping hits in the rows' text, ordered by row and then by position. A whole word is not preceded or
    /// followed by a letter, digit or underscore, so a query may start or end with punctuation such as a quote.
    /// </summary>
    public static IReadOnlyList<SearchMatch> Find(IReadOnlyList<DiffLine> rows, SearchQuery query)
    {
        if (query.Text.Length == 0)
            return [];

        var pattern = Regex.Escape(query.Text);
        if (query.WholeWord)
            pattern = $@"(?<!\w){pattern}(?!\w)";
        var options = RegexOptions.CultureInvariant | (query.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        var regex = new Regex(pattern, options);

        var matches = new List<SearchMatch>();
        for (var row = 0; row < rows.Count; row++)
        {
            foreach (Match match in regex.Matches(rows[row].Text))
                matches.Add(new SearchMatch(row, match.Index, match.Length));
        }
        return matches;
    }
}
