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
var records = new List<InvoiceRecord>();

foreach (var file in Directory.EnumerateFiles(args[0], "*.pdf", SearchOption.TopDirectoryOnly))
{
    var record = new InvoiceRecord { OriginalFilePath = file };
    analyzer.Analyze(record); ruleEngine.Evaluate(record); records.Add(record);
    if (verbose && Path.GetFileName(file) == "1300.00.pdf") Console.WriteLine(record.ExtractedText);
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
    var expectedMatch = Regex.Match(Path.GetFileNameWithoutExtension(file), @"^(?<amount>\d+(?:\.\d{1,2})?)");
    if (!expectedMatch.Success || !decimal.TryParse(expectedMatch.Groups["amount"].Value, CultureInfo.InvariantCulture, out var expected)) continue;
    if (record.TotalAmount is null || Math.Abs(record.TotalAmount.Value - expected) > 0.01m)
        failures.Add($"{Path.GetFileName(file)}: expected {expected:N2}, extracted {record.TotalAmount?.ToString("N2") ?? "none"}; {record.RecognitionSummary}");
}

Console.WriteLine($"Checked: {records.Count} PDFs");
Console.WriteLine($"Amount match: {records.Count - failures.Count}/{records.Count}");
Console.WriteLine($"Consumable classified: {records.Count(x => x.Category == InvoiceCategory.Consumable)}/{records.Count}");
Console.WriteLine($"Invoice number extracted: {records.Count(x => !string.IsNullOrWhiteSpace(x.InvoiceNumber))}/{records.Count}");
Console.WriteLine($"Invoice date extracted: {records.Count(x => x.InvoiceDate is not null)}/{records.Count}");
Console.WriteLine($"Seller extracted: {records.Count(x => !string.IsNullOrWhiteSpace(x.SellerName))}/{records.Count}");
Console.WriteLine($"Amount + tax cross-check: {records.Count(x => x.Amount is not null && x.Tax is not null)}/{records.Count}");
Console.WriteLine($"Validation errors: {records.Count(x => x.ValidationIssues.Any(i => i.Severity == ValidationSeverity.Error))}");
foreach (var record in records.Where(x => x.InvoiceDate is null || x.Amount is null || x.Tax is null))
    Console.WriteLine($"REVIEW {record.OriginalFileName}: missing {string.Join(", ", new[] { record.InvoiceDate is null ? "date" : null, record.Amount is null ? "amount" : null, record.Tax is null ? "tax" : null }.Where(x => x is not null))}");
foreach (var failure in failures) Console.WriteLine("FAIL " + failure);
return failures.Count == 0 ? 0 : 1;
