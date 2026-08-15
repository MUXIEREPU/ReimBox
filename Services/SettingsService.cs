using System.Text.Json;
using ReimbursementAssistant.Configuration;

namespace ReimbursementAssistant.Services;

public sealed class SettingsService
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReimBox", "settings.json");

    public ReimbursementSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new ReimbursementSettings();
            return JsonSerializer.Deserialize<ReimbursementSettings>(File.ReadAllText(_path)) ?? new ReimbursementSettings();
        }
        catch
        {
            return new ReimbursementSettings();
        }
    }

    public void Save(ReimbursementSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
