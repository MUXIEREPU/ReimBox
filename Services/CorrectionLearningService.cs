using System.Text.Json;
using System.Text.Json.Serialization;
using ReimbursementAssistant.Models;

namespace ReimbursementAssistant.Services;

public sealed class CorrectionLearningService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReimBox", "learned-corrections.json");
    private Dictionary<string, LearnedCorrection> _corrections = new(StringComparer.OrdinalIgnoreCase);

    // 这些词是 PDF 表格表头或列名，不是真实的销售方名称，不应该参与学习。
    private static readonly string[] InvalidSellerNames =
    [
        "规格型号", "项目名称", "商品名称", "货物名称", "劳务名称", "服务名称",
        "名称", "品名", "项目", "规格", "型号", "单位", "数量", "单价", "金额"
    ];

    public int Count => _corrections.Count;
    public CorrectionLearningService()
    {
        Load();
        PruneInvalid();
    }

    public bool TryApply(InvoiceRecord record)
    {
        var key = Normalize(record.SellerName);
        if (key.Length == 0 || IsInvalidSeller(record.SellerName) || !_corrections.TryGetValue(key, out var correction)) return false;
        record.Category = correction.Category;
        record.SubCategory = correction.Category == InvoiceCategory.Travel ? correction.SubCategory : TravelSubCategory.None;
        record.ManualOverride = true;
        record.ClassificationConfidence = 1.0;
        return true;
    }

    public void Remember(InvoiceRecord record)
    {
        var key = Normalize(record.SellerName);
        if (key.Length == 0 || record.Category == InvoiceCategory.Unknown || IsInvalidSeller(record.SellerName)) return;
        _corrections[key] = new LearnedCorrection(record.SellerName!.Trim(), record.Category, record.SubCategory, DateTime.Now);
        Save();
    }

    public void Clear()
    {
        _corrections.Clear();
        Save();
    }

    private static bool IsInvalidSeller(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var trimmed = name.Trim();
        return InvalidSellerNames.Any(x => string.Equals(trimmed, x, StringComparison.OrdinalIgnoreCase));
    }

    private void PruneInvalid()
    {
        var invalidKeys = _corrections.Keys.Where(k => IsInvalidSeller(_corrections[k].SellerName)).ToList();
        if (invalidKeys.Count == 0) return;
        foreach (var key in invalidKeys) _corrections.Remove(key);
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var items = JsonSerializer.Deserialize<List<LearnedCorrection>>(File.ReadAllText(_path), JsonOptions) ?? [];
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
        File.WriteAllText(_path, JsonSerializer.Serialize(_corrections.Values.OrderBy(item => item.SellerName), JsonOptions));
    }

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value)
        ? ""
        : new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray()).ToUpperInvariant();

    private sealed record LearnedCorrection(string SellerName, InvoiceCategory Category, TravelSubCategory SubCategory, DateTime UpdatedAt);
}
