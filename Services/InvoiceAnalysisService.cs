using System.Globalization;
using System.Text.RegularExpressions;
using ReimbursementAssistant.Models;

namespace ReimbursementAssistant.Services;

public sealed class InvoiceAnalysisService(
    PdfTextService pdfText,
    PaddleOcrVlService paddle,
    WindowsOcrService ocr,
    InvoiceClassificationService classifier,
    InvoiceValidationService validator)
{
    private static readonly Regex DatePattern = new(@"(?:开票日期|日期)?\s*[:：]?\s*(?<year>20\d{2})\s*[年/\-.]\s*(?<month>\d{1,2})\s*[月/\-.]\s*(?<day>\d{1,2})\s*日?", RegexOptions.Compiled);
    private static readonly Regex InvoiceNumberPattern = new(@"(?:发票号码|号码)\s*[:：]?\s*(?<number>\d{8,30})", RegexOptions.Compiled);
    private static readonly Regex SellerPattern = new(
        @"(?:销\s*名称|销售方名称|销方名称|销售方信息[\s\S]{0,40}?名称|销售方[\s\S]{0,40}?名称|销方[\s\S]{0,40}?名称)\s*[:：]?\s*(?<seller>[^\r\n]+)",
        RegexOptions.Compiled);
    private static readonly Regex AnyNamePattern = new(
        @"(?<!项目)名称\s*[:：]\s*(?<name>[\u4e00-\u9fffA-Za-z0-9（）()·\-]{2,80})",
        RegexOptions.Compiled);
    private static readonly Regex TotalWithSmallPattern = new(@"(?:价税合计|小写)[\s\S]{0,80}?[¥￥]?\s*(?<amount>\d{1,10}(?:\.\d{1,2})?)", RegexOptions.Compiled);
    private static readonly Regex AmountPattern = new(@"(?:金额|合计|价税合计|小写)\s*[:：]?\s*[¥￥]?\s*(?<amount>\d{1,10}(?:\.\d{1,2})?)", RegexOptions.Compiled);
    private static readonly Regex CurrencyPattern = new(@"[¥￥]\s*(?<amount>\d{1,10}(?:\.\d{1,2})?)", RegexOptions.Compiled);
    private static readonly Regex TotalsPattern = new(@"合\s*计\s*[¥￥]?\s*(?<amount>\d{1,10}(?:\.\d{1,2})?)\s*[¥￥]?\s*(?<tax>\d{1,10}(?:\.\d{1,2})?)", RegexOptions.Compiled);

    public void Analyze(InvoiceRecord record, bool allowPaddle = false) =>
        AnalyzeAsync(record, allowPaddle).GetAwaiter().GetResult();

    public async Task AnalyzeAsync(InvoiceRecord record, bool allowPaddle = false)
    {
        record.Status = RecordStatus.Analyzing;
        record.OnChanged(nameof(record.StatusDisplay));

        var text = pdfText.Read(record.OriginalFilePath);
        var method = "PDF 原生解析";
        var source = RecognitionSource.NativePdfText;

        if (text.Length < 20)
        {
            if (allowPaddle)
            {
                var paddleResult = await paddle.TryReadAsync(record.OriginalFilePath);
                if (!string.IsNullOrWhiteSpace(paddleResult.Text))
                {
                    text = paddleResult.Text;
                    method = "PaddleOCR-VL-1.6";
                    source = RecognitionSource.PaddleOcrVl16;
                }
                else
                {
                    method = paddleResult.FailureReason ?? "PaddleOCR-VL 未返回可用文字";
                }
            }

            if (text.Length < 20)
            {
                var ocrText = await ocr.ReadAsync(record.OriginalFilePath);
                if (!string.IsNullOrWhiteSpace(ocrText))
                {
                    text = ocrText;
                    method = allowPaddle ? "Windows 本地 OCR（Paddle 不可用）" : "Windows 本地 OCR";
                    source = RecognitionSource.WindowsOcr;
                }
            }
        }

        record.ExtractedText = text;
        record.ItemDescription = InvoiceItemExtractionService.Extract(text) ?? record.ItemDescription ?? Path.GetFileNameWithoutExtension(record.OriginalFileName);
        record.InvoiceDate = ExtractDate(text) ?? record.InvoiceDate;
        record.SellerName = ExtractSeller(text) ?? record.SellerName;
        record.InvoiceNumber = ExtractInvoiceNumber(text) ?? record.InvoiceNumber;

        var totals = ExtractTotals(text);
        record.Amount = totals.Amount ?? record.Amount;
        record.Tax = totals.Tax ?? record.Tax;
        record.TotalAmount = ExtractTotalAmount(text) ?? record.TotalAmount;
        var amountFromFileName = false;
        if (record.TotalAmount is null && source != RecognitionSource.NativePdfText && LooksLikeInvoice(text) && ExtractAmountFromFileName(record.OriginalFilePath) is { } fileNameAmount)
        {
            record.TotalAmount = fileNameAmount;
            amountFromFileName = true;
        }

        classifier.Classify(record, text);
        record.RecognitionSource = source;
        record.Confidence = Score(record, text);
        validator.Validate(record);
        if (amountFromFileName)
        {
            record.ValidationIssues.Add(new("AMOUNT_FROM_FILENAME", ValidationSeverity.Warning, "票面 OCR 未读出金额，已采用文件名中的金额，请人工核对"));
            record.OnChanged(nameof(record.ValidationDisplays));
        }
        record.RecognitionSummary = $"{method} · {text.Length} 个字符 · {record.ValidationIssues.Count} 项校验提示";

        record.OnChanged(nameof(record.InvoiceDate));
        record.OnChanged(nameof(record.SellerName));
        record.OnChanged(nameof(record.InvoiceNumber));
        record.OnChanged(nameof(record.Amount));
        record.OnChanged(nameof(record.Tax));
        record.OnChanged(nameof(record.TotalAmount));
        record.OnChanged(nameof(record.Confidence));
        record.OnChanged(nameof(record.RecognitionMethodDisplay));
        record.OnChanged(nameof(record.RecognitionSummary));
    }

    private static decimal? ExtractTotalAmount(string text)
    {
        var combinedTotal = ExtractCombinedInvoiceTotal(text);
        if (combinedTotal is not null) return combinedTotal;

        var exact = TotalWithSmallPattern.Match(text);
        if (TryDecimal(exact, out var total)) return total;

        var aviationTotal = ExtractAviationTotal(text);
        if (aviationTotal is not null) return aviationTotal;

        var amounts = AmountPattern.Matches(text)
            .Cast<Match>()
            .Concat(CurrencyPattern.Matches(text).Cast<Match>())
            .Select(match => TryDecimal(match, out var value) ? value : 0)
            .Where(value => value > 0 && value < 10_000_000)
            .ToArray();

        return amounts.Length == 0 ? null : amounts.Max();
    }

    private static decimal? ExtractCombinedInvoiceTotal(string text)
    {
        var invoiceMatches = InvoiceNumberPattern.Matches(text).Cast<Match>().ToArray();
        var distinctNumbers = invoiceMatches.Select(match => match.Groups["number"].Value).Distinct().ToArray();
        if (distinctNumbers.Length <= 1) return null;

        var totals = new List<decimal>();
        var processedNumbers = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < invoiceMatches.Length; i++)
        {
            var current = invoiceMatches[i];
            var number = current.Groups["number"].Value;
            if (!processedNumbers.Add(number)) continue;

            var end = text.Length;
            for (var next = i + 1; next < invoiceMatches.Length; next++)
            {
                if (invoiceMatches[next].Groups["number"].Value == number) continue;
                end = invoiceMatches[next].Index;
                break;
            }

            var segment = text[current.Index..end];
            var totalMatch = TotalWithSmallPattern.Match(segment);
            if (TryDecimal(totalMatch, out var segmentTotal)) totals.Add(segmentTotal);
        }

        return totals.Count > 1 ? totals.Sum() : null;
    }

    private static decimal? ExtractAviationTotal(string text)
    {
        var section = Regex.Match(text, @"票价[\s\S]{0,120}?合计(?<values>[\s\S]{0,240}?)(?:电子客票号码|验证码|销售网点|$)", RegexOptions.IgnoreCase);
        if (!section.Success) return null;

        var values = Regex.Matches(section.Groups["values"].Value, @"(?:CNY|[¥￥])\s*(?<amount>\d{1,10}(?:\.\d{1,2})?)", RegexOptions.IgnoreCase)
            .Cast<Match>()
            .Select(match => TryDecimal(match, out var value) ? value : 0)
            .Where(value => value > 0 && value < 10_000_000)
            .ToArray();
        return values.Length == 0 ? null : values[^1];
    }

    private static decimal? ExtractAmountFromFileName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var matches = Regex.Matches(fileName, @"(?<amount>\d+(?:\.\d{1,2})?)\s*元(?!运费|邮费|快递费)");
        if (matches.Count == 0) return null;
        return TryDecimal(matches[^1], out var amount) ? amount : null;
    }

    private static bool LooksLikeInvoice(string text)
    {
        var compact = Regex.Replace(text, @"\s+", string.Empty);
        return compact.Contains("发票", StringComparison.Ordinal)
               || compact.Contains("票价", StringComparison.Ordinal)
               || compact.Contains("座位号", StringComparison.Ordinal);
    }
    private static DateTime? ExtractDate(string text)
    {
        var match = DatePattern.Match(text);
        if (!match.Success) return null;

        var raw = $"{match.Groups["year"].Value}-{match.Groups["month"].Value}-{match.Groups["day"].Value}";
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
    }

    private static string? ExtractSeller(string text)
    {
        var exact = SellerPattern.Match(text);
        if (exact.Success) return CleanField(exact.Groups["seller"].Value);

        var names = AnyNamePattern.Matches(text).Select(match => CleanField(match.Groups["name"].Value)).Where(x => x.Length > 1).ToArray();
        return names.Length >= 2 ? names[^1] : names.FirstOrDefault();
    }

    private static string? ExtractInvoiceNumber(string text)
    {
        var match = InvoiceNumberPattern.Match(text);
        return match.Success ? match.Groups["number"].Value : null;
    }

    private static (decimal? Amount, decimal? Tax) ExtractTotals(string text)
    {
        var match = TotalsPattern.Match(text);
        if (!match.Success) return (null, null);

        var amount = TryDecimal(match, out var amountValue, "amount") ? amountValue : (decimal?)null;
        var tax = TryDecimal(match, out var taxValue, "tax") ? taxValue : (decimal?)null;
        return (amount, tax);
    }

    private static bool TryDecimal(Match match, out decimal value, string groupName = "amount")
    {
        value = 0;
        return match.Success && decimal.TryParse(match.Groups[groupName].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static string CleanField(string value)
    {
        var text = value.Trim();
        var stop = text.IndexOfAny([' ', '\t', '　']);
        if (stop > 1) text = text[..stop];
        return text.Trim('：', ':', '，', ',', ';', '；');
    }

    private static double Score(InvoiceRecord record, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        var fields = new[]
        {
            record.TotalAmount is not null,
            record.InvoiceDate is not null,
            !string.IsNullOrWhiteSpace(record.SellerName),
            !string.IsNullOrWhiteSpace(record.InvoiceNumber),
            !string.IsNullOrWhiteSpace(record.ItemDescription)
        };

        return Math.Round(fields.Count(x => x) / (double)fields.Length, 2);
    }
}
