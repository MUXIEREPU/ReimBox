using System.Globalization;
using System.Text.RegularExpressions;
using ReimbursementAssistant.Configuration;
using ReimbursementAssistant.Models;
using ReimbursementAssistant.Services;

if (args.Length is < 1 or > 3 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: RecognitionVerifier <PDF-folder> [--verbose|--dump <file-name>]");
    return 2;
}

var settings = new ReimbursementSettings { EnablePaddleOcrVl = false };
var verbose = args.Length == 2 && args[1] == "--verbose";
var dumpFileName = args.Length == 3 && args[1] == "--dump" ? args[2] : null;
var analyzer = new InvoiceAnalysisService(new PdfTextService(), new PaddleOcrVlService(settings), new WindowsOcrService(), new InvoiceClassificationService(), new InvoiceValidationService());
var ruleEngine = new ReimbursementRuleEngine(settings);
var failures = new List<string>();
var fileNameMismatches = new List<string>();
var records = new List<InvoiceRecord>();
var amountExpectationCount = 0;

foreach (var file in Directory.EnumerateFiles(args[0], "*.pdf", SearchOption.AllDirectories))
{
    var record = new InvoiceRecord { OriginalFilePath = file };
    analyzer.Analyze(record); ruleEngine.Evaluate(record); records.Add(record);
    if (verbose)
        Console.WriteLine($"RESULT {Path.GetRelativePath(args[0], file)}\t{record.CategoryDisplay}\t{record.TotalAmount?.ToString("0.00") ?? "-"}\t{record.ItemDescription ?? "-"}");
    if (dumpFileName is not null && string.Equals(Path.GetFileName(file), dumpFileName, StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"FILE: {record.OriginalFileName}");
        Console.WriteLine($"ITEM: {record.ItemDescription}");
        Console.WriteLine($"TOTAL: {record.TotalAmount}");
        Console.WriteLine($"CATEGORY: {record.CategoryDisplay} ({record.Confidence:P0})");
        Console.WriteLine("TEXT:");
        Console.WriteLine(record.ExtractedText);
        return 0;
    }
    if (!TryGetExpectedAmountFromFileName(file, out var expected)) continue;
    amountExpectationCount++;
    if (record.TotalAmount is null || Math.Abs(record.TotalAmount.Value - expected) > 0.01m)
    {
        var amountIsInternallyConsistent = record.TotalAmount is { } total && record.Amount is { } amount && record.Tax is { } tax && Math.Abs(amount + tax - total) <= 0.02m;
        var message = $"{Path.GetFileName(file)}: file name {expected:N2}, invoice {record.TotalAmount?.ToString("N2") ?? "none"}; {record.RecognitionSummary}";
        if (amountIsInternallyConsistent && record.ValidationIssues.All(issue => issue.Severity != ValidationSeverity.Error))
            fileNameMismatches.Add(message);
        else
            failures.Add(message);
    }
}

Console.WriteLine($"Checked: {records.Count} PDFs");
Console.WriteLine($"Amount match: {amountExpectationCount - failures.Count - fileNameMismatches.Count}/{amountExpectationCount}");
Console.WriteLine($"File-name amount mismatches: {fileNameMismatches.Count}");
foreach (var categoryGroup in records.GroupBy(record => record.Category).OrderBy(group => group.Key))
    Console.WriteLine($"Category {categoryGroup.Key}: {categoryGroup.Count()}/{records.Count}");
foreach (var travelGroup in records.Where(record => record.Category == InvoiceCategory.Travel).GroupBy(record => record.SubCategory).OrderBy(group => group.Key))
    Console.WriteLine($"Travel {travelGroup.Key}: {travelGroup.Count()}");
var probableInvoices = records.Where(IsProbableInvoice).ToList();
Console.WriteLine($"Probable invoices: {probableInvoices.Count}/{records.Count}");
Console.WriteLine($"Invoice number extracted: {probableInvoices.Count(x => !string.IsNullOrWhiteSpace(x.InvoiceNumber))}/{probableInvoices.Count}");
Console.WriteLine($"Invoice date extracted: {probableInvoices.Count(x => x.InvoiceDate is not null)}/{probableInvoices.Count}");
Console.WriteLine($"Seller extracted: {probableInvoices.Count(x => !string.IsNullOrWhiteSpace(x.SellerName))}/{probableInvoices.Count}");
Console.WriteLine($"Amount + tax cross-check: {probableInvoices.Count(x => x.Amount is not null && x.Tax is not null)}/{probableInvoices.Count}");
Console.WriteLine($"Validation errors: {probableInvoices.Count(x => x.ValidationIssues.Any(i => i.Severity == ValidationSeverity.Error))}");
if (verbose)
    foreach (var record in probableInvoices.Where(x => x.InvoiceDate is null || x.Amount is null || x.Tax is null))
        Console.WriteLine($"REVIEW {record.OriginalFileName}: missing {string.Join(", ", new[] { record.InvoiceDate is null ? "date" : null, record.Amount is null ? "amount" : null, record.Tax is null ? "tax" : null }.Where(x => x is not null))}");
foreach (var mismatch in fileNameMismatches) Console.WriteLine("DATASET NAME MISMATCH " + mismatch);
foreach (var failure in failures) Console.WriteLine("FAIL " + failure);
if (verbose)
    foreach (var record in records.Where(record => record.Category == InvoiceCategory.Unknown))
        Console.WriteLine($"UNKNOWN {Path.GetRelativePath(args[0], record.OriginalFilePath)}: {record.ItemDescription ?? "未提取项目"}");
return failures.Count == 0 ? 0 : 1;

static bool TryGetExpectedAmountFromFileName(string filePath, out decimal expected)
{
    var name = Path.GetFileNameWithoutExtension(filePath);
    var explicitCurrencyMatches = Regex.Matches(name, @"(?<amount>\d+(?:\.\d{1,2})?)\s*元(?!运费|邮费|快递费)");
    if (explicitCurrencyMatches.Count > 0)
        return decimal.TryParse(explicitCurrencyMatches[^1].Groups["amount"].Value, CultureInfo.InvariantCulture, out expected);

    var exactAmount = Regex.Match(name, @"^(?<amount>\d{1,7}\.\d{1,2})(?:_\d+)?$");
    if (exactAmount.Success)
        return decimal.TryParse(exactAmount.Groups["amount"].Value, CultureInfo.InvariantCulture, out expected);

    expected = 0;
    return false;
}

static bool IsProbableInvoice(InvoiceRecord record)
{
    var text = record.ExtractedText ?? string.Empty;
    return !string.IsNullOrWhiteSpace(record.InvoiceNumber)
           || text.Contains("价税合计", StringComparison.Ordinal)
           || text.Contains("发票代码", StringComparison.Ordinal)
           || (record.InvoiceDate is not null && record.TotalAmount is not null && !string.IsNullOrWhiteSpace(record.SellerName));
}
