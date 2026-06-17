using System.IO;
using System.Text.Json;

namespace kparser2.Services;

public sealed class ViewSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "kparser2",
        "view-settings.json");

    public IReadOnlyList<string> LoadActiveViewIds(IReadOnlyList<string> defaultIds)
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return defaultIds;
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<ViewSettings>(json, JsonOptions);
            return settings?.ActiveViewIds?.Count > 0 ? settings.ActiveViewIds : defaultIds;
        }
        catch
        {
            return defaultIds;
        }
    }

    public void SaveActiveViewIds(IEnumerable<string> viewIds)
    {
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);

        var settings = new ViewSettings { ActiveViewIds = viewIds.ToList() };
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private sealed class ViewSettings
    {
        public List<string> ActiveViewIds { get; set; } = [];
    }
}
