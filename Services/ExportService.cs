using ReimbursementAssistant.Models;
namespace ReimbursementAssistant.Services;

public sealed class ExportService
{
    private readonly InvoiceNamingService _naming = new();

    public string Export(IEnumerable<InvoiceRecord> records, string baseDirectory, ReimbursementInfo reimbursementInfo, InvoiceNamingRule? namingRule = null)
    {
        var rule = namingRule ?? InvoiceNamingRule.Default();
        var recordList = records.Where(record => !record.UserIgnored).ToList();
        if (recordList.Count == 0) throw new InvalidOperationException("没有可导出的发票；当前记录均已标记为忽略。");
        var root = CreateUniqueRoot(baseDirectory, reimbursementInfo, recordList);
        var groups = recordList.GroupBy(x => x.Category switch
        {
            InvoiceCategory.Consumable => "01_耗材",
            InvoiceCategory.Travel => "02_差旅",
            InvoiceCategory.PrintFee => "03_打印费",
            InvoiceCategory.Other => "04_其他",
            _ => "05_待确认"
        });
        foreach (var group in groups)
        {
            var groupRecords = group.ToList();
            var groupTotal = groupRecords.Sum(x => x.TotalAmount ?? 0);
            var parent = Path.Combine(root, $"{group.Key}_{FormatMoney(groupTotal)}元");
            Directory.CreateDirectory(parent);
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in groupRecords)
            {
                var prefix = InvoiceNamingService.MakeUniqueName(_naming.BuildBaseName(record, rule), usedNames);
                if (HasSupplementAttachments(record))
                {
                    var attachmentFolder = Path.Combine(parent, "带附件发票");
                    ExportAsFolder(record, Path.Combine(attachmentFolder, prefix), prefix);
                }
                else
                {
                    ExportFlat(record, parent, prefix);
                }
            }
        }

        WriteReadme(root, reimbursementInfo, recordList);
        new XlsxReportService().Write(Path.Combine(root, "报销清单.xlsx"), recordList);
        return root;
    }

    private static void WriteReadme(string root, ReimbursementInfo reimbursementInfo, IReadOnlyList<InvoiceRecord> records)
    {
        var lines = new List<string>
        {
            "**报销材料说明**",
            "",
            $"报销人姓名：{reimbursementInfo.PersonName.Trim()}",
            $"学号/工号：{reimbursementInfo.PersonIdentifier.Trim()}",
            $"**总金额：¥{records.Sum(x => x.TotalAmount ?? 0):N2}**",
            $"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}",
            ""
        };

        if (!string.IsNullOrWhiteSpace(reimbursementInfo.Description))
        {
            lines.Add("补充说明：");
            lines.Add(reimbursementInfo.Description.Trim());
            lines.Add("");
        }

        lines.Add("报销明细：");
        var index = 1;
        foreach (var record in records.OrderBy(x => x.CategoryDisplay).ThenBy(x => x.DisplayIndex))
        {
            var item = string.IsNullOrWhiteSpace(record.ItemDescription)
                ? Path.GetFileNameWithoutExtension(record.OriginalFileName)
                : record.ItemDescription.Trim();
            lines.Add($"{index++}. {record.CategoryDisplay}｜{item}｜{record.OriginalFileName}｜¥{(record.TotalAmount ?? 0):N2}");
        }
        lines.Add("");

        File.WriteAllText(Path.Combine(root, "README.txt"), string.Join(Environment.NewLine, lines));
    }

    private static void ExportAsFolder(InvoiceRecord record, string folder, string prefix)
    {
        Directory.CreateDirectory(folder);
        Copy(record.OriginalFilePath, Path.Combine(folder, $"01_{prefix}{Path.GetExtension(record.OriginalFilePath)}"));

        var attachmentIndex = 2;
        foreach (var attachment in record.AttachedDocuments.Where(x => x.AttachmentType != AttachmentType.Invoice))
        {
            Copy(attachment.FilePath, Path.Combine(folder, $"{attachmentIndex++:D2}_{InvoiceRecord.DisplayAttachment(attachment.AttachmentType)}{Path.GetExtension(attachment.FilePath)}"));
        }
    }

    private static bool HasSupplementAttachments(InvoiceRecord record) => record.AttachedDocuments.Any(x => x.AttachmentType != AttachmentType.Invoice);

    private static void ExportFlat(InvoiceRecord record, string parent, string prefix)
    {
        Copy(record.OriginalFilePath, Path.Combine(parent, $"{prefix}_01_发票{Path.GetExtension(record.OriginalFilePath)}"));

        var attachmentIndex = 2;
        foreach (var attachment in record.AttachedDocuments.Where(x => x.AttachmentType != AttachmentType.Invoice))
        {
            Copy(attachment.FilePath, Path.Combine(parent, $"{prefix}_{attachmentIndex++:D2}_{InvoiceRecord.DisplayAttachment(attachment.AttachmentType)}{Path.GetExtension(attachment.FilePath)}"));
        }
    }

    private static void Copy(string source, string destination) => File.Copy(source, destination, true);
    private static string SafeName(string text) => string.Concat(text.Take(30).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
    private static string FormatMoney(decimal amount) => amount.ToString("0.##");
    private static string CreateUniqueRoot(string baseDirectory, ReimbursementInfo reimbursementInfo, IReadOnlyList<InvoiceRecord> records)
    {
        var total = records.Sum(x => x.TotalAmount ?? 0);
        var name = SafeName(reimbursementInfo.PersonName);
        var initial = Path.Combine(baseDirectory, $"{DateTime.Today:yyyyMMdd}_{name}_{FormatMoney(total)}元");
        if (!Directory.Exists(initial)) { Directory.CreateDirectory(initial); return initial; }
        var index = 1; string candidate;
        do candidate = $"{initial}_{index++:D2}"; while (Directory.Exists(candidate));
        Directory.CreateDirectory(candidate); return candidate;
    }
}
