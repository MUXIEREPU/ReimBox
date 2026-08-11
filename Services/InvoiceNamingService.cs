using ReimbursementAssistant.Models;

namespace ReimbursementAssistant.Services;

public sealed class InvoiceNamingService
{
    public string BuildBaseName(InvoiceRecord record, InvoiceNamingRule rule)
    {
        var parts = rule.Fields
            .Select(field => BuildPart(record, field))
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(SafeName)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        if (parts.Count == 0)
        {
            parts.Add($"{record.DisplayIndex:D3}");
            parts.Add(SafeName(record.ItemDescription ?? Path.GetFileNameWithoutExtension(record.OriginalFileName)));
            parts.Add($"{record.TotalAmount ?? 0:0.##}元");
        }

        var separator = string.IsNullOrEmpty(rule.Separator) ? "_" : rule.Separator;
        return SafeName(string.Join(separator, parts));
    }

    public string BuildFileName(InvoiceRecord record, InvoiceNamingRule rule)
    {
        return $"{BuildBaseName(record, rule)}{Path.GetExtension(record.OriginalFilePath)}";
    }

    public int RenameOriginalFiles(IReadOnlyList<InvoiceRecord> records, InvoiceNamingRule rule)
    {
        var renamed = 0;
        foreach (var record in records)
        {
            if (!File.Exists(record.OriginalFilePath))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(record.OriginalFilePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var desiredPath = Path.Combine(directory, BuildFileName(record, rule));
            var targetPath = MakeUniquePath(desiredPath, record.OriginalFilePath);
            if (string.Equals(record.OriginalFilePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Move(record.OriginalFilePath, targetPath);
            record.OriginalFilePath = targetPath;
            renamed++;
        }

        return renamed;
    }

    public static string MakeUniqueName(string candidate, ISet<string> usedNames)
    {
        var safe = string.IsNullOrWhiteSpace(candidate) ? "未命名" : candidate;
        if (usedNames.Add(safe))
        {
            return safe;
        }

        var index = 2;
        string next;
        do
        {
            next = $"{safe}_{index++:D2}";
        } while (!usedNames.Add(next));

        return next;
    }

    private static string MakeUniquePath(string desiredPath, string currentPath)
    {
        if (string.Equals(desiredPath, currentPath, StringComparison.OrdinalIgnoreCase))
        {
            return currentPath;
        }

        if (!File.Exists(desiredPath))
        {
            return desiredPath;
        }

        var directory = Path.GetDirectoryName(desiredPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);
        var index = 2;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{name}_{index++:D2}{extension}");
        } while (File.Exists(candidate) && !string.Equals(candidate, currentPath, StringComparison.OrdinalIgnoreCase));

        return candidate;
    }

    private static string BuildPart(InvoiceRecord record, NamingField field)
    {
        return field switch
        {
            NamingField.Number => $"{record.DisplayIndex:D3}",
            NamingField.Category => record.CategoryDisplay.Replace(" / ", "-"),
            NamingField.ItemDescription => record.ItemDescription ?? Path.GetFileNameWithoutExtension(record.OriginalFileName),
            NamingField.TotalAmount => $"{record.TotalAmount ?? 0:0.##}元",
            NamingField.InvoiceDate => record.InvoiceDate?.ToString("yyyyMMdd") ?? "",
            NamingField.SellerName => record.SellerName ?? "",
            NamingField.InvoiceNumber => record.InvoiceNumber ?? "",
            _ => ""
        };
    }

    private static string SafeName(string text)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var normalized = string.Concat(text.Trim().Select(c => invalidChars.Contains(c) ? '_' : c));
        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        return normalized.Trim(' ', '_').Length > 80 ? normalized.Trim(' ', '_')[..80] : normalized.Trim(' ', '_');
    }
}
