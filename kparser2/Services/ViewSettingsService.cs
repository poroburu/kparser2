using System.IO;
using System.Text.Json;

namespace kparser2.Services;

public sealed class ViewSettingsService
{
    private static readonly string SettingsPath = Environment.GetEnvironmentVariable("KPARSER2_VIEW_SETTINGS_PATH")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kparser2", "view-settings.json");
    public static ViewSettingsService Shared { get; } = new();
    public ViewSettings State { get; private set; } = new();
    public string? Error { get; private set; }
    public ViewSettingsService()
    {
        try
        {
            if (File.Exists(SettingsPath)) State = JsonSerializer.Deserialize<ViewSettings>(File.ReadAllText(SettingsPath)) ?? new();
            State.Reports ??= new();
            State.RecentCaptures ??= [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { Error = ex.Message; }
    }
    public ReportPreferences Report(string id)
    {
        if (!State.Reports.TryGetValue(id, out var preferences)) State.Reports[id] = preferences = new();
        return preferences;
    }
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath + ".tmp", JsonSerializer.Serialize(State, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(SettingsPath + ".tmp", SettingsPath, true);
            Error = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Error = ex.Message; }
    }
    public sealed class ViewSettings
    {
        public List<string> ActiveViewIds { get; set; } = [];
        public string SelectedReport { get; set; } = "offense";
        public string Search { get; set; } = "";
        public double SidebarWidth { get; set; } = 220;
        public double Width { get; set; } = 1200;
        public double Height { get; set; } = 800;
        public double? Left { get; set; }
        public double? Top { get; set; }
        public bool Maximized { get; set; }
        public Dictionary<string, ReportPreferences> Reports { get; set; } = new();
        public List<string> RecentCaptures { get; set; } = [];
    }
    public sealed class ReportPreferences
    {
        public bool GroupMobs { get; set; } = true;
        public bool ExcludeZeroXp { get; set; }
        public string? Player { get; set; }
        public string? Mob { get; set; }
        public string Category { get; set; } = "All";
        public string Mode { get; set; } = "All";
        public string? Speaker { get; set; }
        public bool Cumulative { get; set; }
        public int BucketSeconds { get; set; } = 10;
        public bool ShowDetails { get; set; }
        public bool ExcludeCrystals { get; set; }
        public int BaseAttacks { get; set; } = 1;
        // null means all fights; an empty list intentionally selects none.
        public List<int>? BattleIds { get; set; }
    }
}
