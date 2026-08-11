using System.Text.RegularExpressions;

namespace ReimbursementAssistant.Services;

public static class InvoiceItemExtractionService
{
    private static readonly Regex TaxCategoryPattern = new(@"\*[^*\r\n]{1,40}\*", RegexOptions.Compiled);
    private static readonly Regex PromotionPattern = new(@"【[^】]*(?:活动|优惠|狂欢|特价|券后|促销|秒杀|官方|套餐)[^】]*】", RegexOptions.Compiled);
    private static readonly Regex TailColumnsPattern = new(@"\s+(?:(?:\S+\s+)?(?:件|个|米|套|只|张|台|块|支|卷|盒|批|片|PCS|pc|kg|g)\s+)?\d+(?:\.\d+)?\s+\d+(?:\.\d+)?\s+\d+(?:\.\d+)?\s+\d+%\s+\d+(?:\.\d+)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NumericOnlyColumnsPattern = new(@"^\s*\d+(?:\.\d+)?\s+\d+(?:\.\d+)?\s+\d+(?:\.\d+)?\s+\d+%\s+\d+(?:\.\d+)?\s*$", RegexOptions.Compiled);
    private static readonly Regex HeaderPattern = new(@"项目名称|货物或应税劳务|服务名称", RegexOptions.Compiled);
    private static readonly Regex StopPattern = new(@"^\s*(合\s*计|价税合计|备\s*注|开票人)", RegexOptions.Compiled);
    private static readonly Regex LongLeadingModelPattern = new(@"^[A-Za-z0-9/_.+\-]{8,}(?=[\u4e00-\u9fff])", RegexOptions.Compiled);
    private static readonly string[] MeaningfulKeywords =
    [
        "电机", "螺丝", "螺母", "垫片", "胶带", "连接器", "电池", "电子元件", "电子元器件", "模块",
        "工具", "材料", "配件", "线", "板", "碳纤维", "泡沫板", "扎带", "端子", "插头", "数据线"
    ];

    public static string? Extract(string text)
    {
        var rows = ExtractDetailRows(text).Select(CleanRow).Where(IsUseful).Distinct().ToArray();
        if (rows.Length > 0) return PickBest(rows);

        return ExtractFallback(text);
    }

    private static IEnumerable<List<string>> ExtractDetailRows(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        var headerIndex = Array.FindIndex(lines, line => HeaderPattern.IsMatch(line));
        if (headerIndex < 0) yield break;

        List<string>? current = null;
        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (StopPattern.IsMatch(line)) break;
            if (line.Contains("发票号码") || line.Contains("开票日期")) continue;

            if (TaxCategoryPattern.IsMatch(line))
            {
                if (current is { Count: > 0 }) yield return current;
                current = [line];
            }
            else if (current is not null)
            {
                current.Add(line);
            }
        }

        if (current is { Count: > 0 }) yield return current;
    }

    private static string CleanRow(List<string> row)
    {
        var fragments = new List<string>();
        foreach (var rawLine in row)
        {
            var line = TaxCategoryPattern.Replace(rawLine, string.Empty).Trim();
            if (NumericOnlyColumnsPattern.IsMatch(line)) continue;
            line = TailColumnsPattern.Replace(line, string.Empty).Trim();
            if (NumericOnlyColumnsPattern.IsMatch(line) || !ContainsChinese(line)) continue;
            if (line.Length == 0) continue;
            fragments.Add(line);
        }

        return CleanItemName(string.Concat(fragments));
    }

    private static string CleanItemName(string text)
    {
        var value = PromotionPattern.Replace(text, string.Empty);
        value = Regex.Replace(value, @"[（(]\s*\d+\s*(?:个|件|只|片|套|米|m|cm)\s*[）)]", string.Empty, RegexOptions.IgnoreCase);
        value = LongLeadingModelPattern.Replace(value, string.Empty);
        value = Regex.Replace(value, @"M\d[\dM*×xX./+\-]*(?:\(\d*)?", string.Empty, RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\d+\s*(?:个|件|只|片|套|米)\)?", string.Empty);
        value = Regex.Replace(value, @"\s+", string.Empty);
        value = Regex.Replace(value, @"(?<=金)\d+(?=超)", string.Empty);
        value = Regex.Replace(value, @"^[A-Za-z0-9/_.+\-]{2,}(?=步进|电机|模块|配件|螺丝|螺母|垫片)", string.Empty);
        value = Regex.Replace(value, @"(?<![\u4e00-\u9fff])(?:件|个|只|片|套|米)(?![\u4e00-\u9fff])", string.Empty);
        value = value.Trim('：', ':', '，', ',', ';', '；', '、', '-', '_', ' ');
        return value.Length <= 42 ? value : value[..42];
    }

    private static string PickBest(IReadOnlyList<string> rows)
    {
        var candidates = rows.Where(x => !x.Contains("价外费用")).ToArray();
        if (candidates.Length == 0) candidates = rows.ToArray();

        var first = candidates[0];
        if (first.Length >= 6) return first;

        return candidates.OrderByDescending(Score).ThenBy(x => x.Length).First();
    }

    private static int Score(string value)
    {
        var keywordScore = MeaningfulKeywords.Count(value.Contains) * 8;
        var lengthScore = Math.Min(value.Length, 30);
        var penalty = value.Any(char.IsDigit) ? 4 : 0;
        return keywordScore + lengthScore - penalty;
    }

    private static bool IsUseful(string value)
    {
        if (value.Length < 2) return false;
        if (value is "无" or "详见清单") return false;
        return value.Any(c => c >= '\u4e00' && c <= '\u9fff');
    }

    private static bool ContainsChinese(string value) => value.Any(c => c >= '\u4e00' && c <= '\u9fff');

    private static string? ExtractFallback(string text)
    {
        var marked = Regex.Match(text, @"\*[^*\r\n]{1,40}\*\s*(?<item>[^\r\n]{2,80})");
        if (marked.Success)
        {
            var cleaned = CleanItemName(marked.Groups["item"].Value);
            if (IsUseful(cleaned)) return cleaned;
        }

        var named = Regex.Match(text, @"(?:项目名称|货物或应税劳务、服务名称|货物或应税劳务名称)\s*[:：]?\s*(?<item>[^\r\n]{2,80})");
        if (!named.Success) return null;

        var value = CleanItemName(named.Groups["item"].Value);
        return IsUseful(value) ? value : null;
    }
}
