using System.Text.RegularExpressions;
using ReimbursementAssistant.Models;

namespace ReimbursementAssistant.Services;

public sealed partial class DuplicateInvoiceService
{
    public int Evaluate(IReadOnlyCollection<InvoiceRecord> records)
    {
        foreach (var record in records)
        {
            record.IsPossibleDuplicate = false;
            record.DuplicateHint = "";
        }

        var groups = new List<(IReadOnlyList<InvoiceRecord> Records, string Reason)>();
        groups.AddRange(records
            .Where(record => !record.DuplicateDismissed && NormalizeInvoiceNumber(record.InvoiceNumber) is not null)
            .GroupBy(record => NormalizeInvoiceNumber(record.InvoiceNumber)!)
            .Where(group => group.Count() > 1)
            .Select(group => ((IReadOnlyList<InvoiceRecord>)group.ToList(), $"发票号码相同：{group.Key}")));

        var alreadyMatched = groups.SelectMany(group => group.Records).ToHashSet();
        groups.AddRange(records
            .Where(record => !record.DuplicateDismissed && !alreadyMatched.Contains(record) && record.InvoiceDate is not null && record.TotalAmount is > 0 && !string.IsNullOrWhiteSpace(record.SellerName))
            .GroupBy(record => $"{record.InvoiceDate:yyyyMMdd}|{record.TotalAmount:0.00}|{NormalizeText(record.SellerName)}")
            .Where(group => group.Count() > 1)
            .Select(group => ((IReadOnlyList<InvoiceRecord>)group.ToList(), "销售方、日期和金额均相同")));

        foreach (var (duplicateRecords, reason) in groups)
        {
            var names = string.Join("、", duplicateRecords.Select(record => record.OriginalFileName));
            foreach (var record in duplicateRecords)
            {
                record.IsPossibleDuplicate = true;
                record.DuplicateHint = $"{reason}；涉及：{names}";
            }
        }

        return records.Count(record => record.IsPossibleDuplicate);
    }

    private static string? NormalizeInvoiceNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var number = DigitsOnly().Replace(value, "");
        return number.Length >= 8 ? number : null;
    }

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : WhiteSpace().Replace(value.Trim().ToUpperInvariant(), "");

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnly();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}
