using System.Text.Json;
using ReimbursementAssistant.Models;

namespace ReimbursementAssistant.Services;

public sealed class CorrectionLearningService
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReimBox", "learned-corrections.json");
    private Dictionary<string, LearnedCorrection> _corrections = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _corrections.Count;
    public CorrectionLearningService() => Load();

    public bool TryApply(InvoiceRecord record)
    {
        var key = Normalize(record.SellerName);
        if (key.Length == 0 || !_corrections.TryGetValue(key, out var correction)) return false;
        record.Category = correction.Category;
        record.SubCategory = correction.Category == InvoiceCategory.Travel ? correction.SubCategory : TravelSubCategory.None;
        record.ManualOverride = true;
        return true;
    }

    public void Remember(InvoiceRecord record)
    {
        var key = Normalize(record.SellerName);
        if (key.Length == 0 || record.Category == InvoiceCategory.Unknown) return;
        _corrections[key] = new LearnedCorrection(record.SellerName!.Trim(), record.Category, record.SubCategory, DateTime.Now);
        Save();
    }

    public void Clear()
    {
        _corrections.Clear();
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var items = JsonSerializer.Deserialize<List<LearnedCorrection>>(File.ReadAllText(_path)) ?? [];
            _corrections = items.Where(item => Normalize(item.SellerName).Length > 0)
                .GroupBy(item => Normalize(item.SellerName), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt).First(), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _corrections = new Dictionary<string, LearnedCorrection>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_corrections.Values.OrderBy(item => item.SellerName), new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value)
        ? ""
        : new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray()).ToUpperInvariant();

    private sealed record LearnedCorrection(string SellerName, InvoiceCategory Category, TravelSubCategory SubCategory, DateTime UpdatedAt);
}
