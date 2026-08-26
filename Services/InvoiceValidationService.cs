using System.Text.RegularExpressions;
using ReimbursementAssistant.Models;
namespace ReimbursementAssistant.Services;
public sealed class InvoiceValidationService
{
    public void Validate(InvoiceRecord record)
    {
        record.ValidationIssues.Clear();
        if (record.TotalAmount is null or <= 0) record.ValidationIssues.Add(new("TOTAL_MISSING", ValidationSeverity.Error, "未能识别有效的价税合计"));
        if (record.InvoiceDate is null) record.ValidationIssues.Add(new("DATE_MISSING", ValidationSeverity.Warning, "未能识别开票日期"));
        if (string.IsNullOrWhiteSpace(record.SellerName)) record.ValidationIssues.Add(new("SELLER_MISSING", ValidationSeverity.Warning, "未能识别销售方"));
        if (string.IsNullOrWhiteSpace(record.InvoiceNumber)) record.ValidationIssues.Add(new("NUMBER_MISSING", ValidationSeverity.Warning, "未能识别发票号码"));
        var invoiceCount = Regex.Matches(record.ExtractedText ?? "", @"(?:发票号码|号码)\s*[:：]?\s*(?<number>\d{8,30})")
            .Select(match => match.Groups["number"].Value)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (invoiceCount > 1) record.ValidationIssues.Add(new("MULTIPLE_INVOICES", ValidationSeverity.Warning, $"PDF 内检测到 {invoiceCount} 张发票，金额已按各发票价税合计汇总，请核对"));
        if (invoiceCount <= 1 && record.Amount is { } amount && record.Tax is { } tax && record.TotalAmount is { } total && Math.Abs(amount + tax - total) > 0.02m) record.ValidationIssues.Add(new("AMOUNT_MISMATCH", ValidationSeverity.Error, $"金额 {amount:N2} + 税额 {tax:N2} 与价税合计 {total:N2} 不一致"));
        if (record.Confidence < 0.65) record.ValidationIssues.Add(new("LOW_CONFIDENCE", ValidationSeverity.Warning, "识别置信度较低，请人工核对"));
        record.OnChanged(nameof(record.ValidationDisplays));
    }
}
