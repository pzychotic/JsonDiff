using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff.Tests.TestSupport;

/// <summary>Records the themes <c>MainViewModel</c> applies, without touching any resources.</summary>
internal sealed class FakeThemeService : IThemeService
{
    public List<AppTheme> Applied { get; } = [];

    public void Apply(AppTheme theme) => Applied.Add(theme);
}
