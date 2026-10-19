using JsonDiff.Models;

namespace JsonDiff.Services;

public interface ISettingsService
{
    AppSettings? Load();
    void Save(AppSettings settings);
}
