using System.Text.Json;
using System.Text.Json.Serialization;
using ReimbursementAssistant.Models;

namespace ReimbursementAssistant.Services;

public sealed class ProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _appDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ReimBox");

    public string AutosavePath => Path.Combine(_appDirectory, "autosave.reimbox");
    private string RecentProjectsPath => Path.Combine(_appDirectory, "recent-projects.json");

    public void Save(
        string path,
        IEnumerable<InvoiceRecord> records,
        ReimbursementInfo reimbursementInfo,
        InvoiceNamingRule namingRule)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var document = new ReimbursementProjectDocument
        {
            SavedAt = DateTime.Now,
            PersonName = reimbursementInfo.PersonName,
            PersonIdentifier = reimbursementInfo.PersonIdentifier,
            Description = reimbursementInfo.Description ?? "",
            NamingFields = namingRule.Fields.ToList(),
            NamingSeparator = namingRule.Separator,
            Invoices = records.Select(ToData).ToList()
        };

        var temporaryPath = fullPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, JsonOptions));
        File.Move(temporaryPath, fullPath, true);
    }

    public ProjectLoadResult Load(string path)
    {
        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<ReimbursementProjectDocument>(json, JsonOptions)
                       ?? throw new InvalidDataException("项目文件为空或格式不正确。");
        if (document.SchemaVersion > 1)
        {
            throw new InvalidDataException($"该项目由更高版本的 ReimBox 创建（格式版本 {document.SchemaVersion}）。");
        }

        var records = document.Invoices.Select(FromData).ToList();
        var namingRule = new InvoiceNamingRule { Separator = string.IsNullOrWhiteSpace(document.NamingSeparator) ? "_" : document.NamingSeparator };
        namingRule.Fields.AddRange(document.NamingFields.Count > 0 ? document.NamingFields : InvoiceNamingRule.Default().Fields);
        return new ProjectLoadResult(
            records,
            new ReimbursementInfo(document.PersonName, document.PersonIdentifier, document.Description),
            namingRule,
            document.SavedAt);
    }

    public IReadOnlyList<string> LoadRecentProjects()
    {
        try
        {
            if (!File.Exists(RecentProjectsPath)) return [];
            var paths = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentProjectsPath), JsonOptions) ?? [];
            return paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
        }
        catch
        {
            return [];
        }
    }

    public void AddRecentProject(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var recent = LoadRecentProjects().Where(item => !string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase)).ToList();
        recent.Insert(0, fullPath);
        Directory.CreateDirectory(_appDirectory);
        File.WriteAllText(RecentProjectsPath, JsonSerializer.Serialize(recent.Take(8), JsonOptions));
    }

    public void DeleteAutosave()
    {
        if (File.Exists(AutosavePath)) File.Delete(AutosavePath);
    }

    private static ProjectInvoiceData ToData(InvoiceRecord record) => new()
    {
        DisplayIndex = record.DisplayIndex,
        OriginalFilePath = record.OriginalFilePath,
        FileHash = record.FileHash,
        InvoiceNumber = record.InvoiceNumber,
        InvoiceDate = record.InvoiceDate,
        SellerName = record.SellerName,
        BuyerName = record.BuyerName,
        ItemDescription = record.ItemDescription,
        Amount = record.Amount,
        Tax = record.Tax,
        TotalAmount = record.TotalAmount,
        Category = record.Category,
        SubCategory = record.SubCategory,
        Confidence = record.Confidence,
        RecognitionSummary = record.RecognitionSummary,
        RecognitionSource = record.RecognitionSource,
        ExtractedText = record.ExtractedText,
        ManualOverride = record.ManualOverride,
        UserIgnored = record.UserIgnored,
        Notes = record.Notes,
        IsThreeDPrinting = record.IsThreeDPrinting,
        DuplicateDismissed = record.DuplicateDismissed,
        Attachments = record.SupplementAttachments.Select(attachment => new ProjectAttachmentData
        {
            FilePath = attachment.FilePath,
            AttachmentType = attachment.AttachmentType,
            Amount = attachment.Amount,
            Date = attachment.Date,
            Confidence = attachment.Confidence
        }).ToList()
    };

    private static InvoiceRecord FromData(ProjectInvoiceData data)
    {
        if (string.IsNullOrWhiteSpace(data.OriginalFilePath))
        {
            throw new InvalidDataException("项目中存在缺少原始文件路径的发票记录。");
        }

        var record = new InvoiceRecord
        {
            OriginalFilePath = data.OriginalFilePath,
            FileHash = data.FileHash,
            DisplayIndex = data.DisplayIndex,
            InvoiceNumber = data.InvoiceNumber,
            InvoiceDate = data.InvoiceDate,
            SellerName = data.SellerName,
            BuyerName = data.BuyerName,
            ItemDescription = data.ItemDescription,
            Amount = data.Amount,
            Tax = data.Tax,
            TotalAmount = data.TotalAmount,
            Category = data.Category,
            SubCategory = data.SubCategory,
            Confidence = data.Confidence,
            RecognitionSummary = data.RecognitionSummary,
            RecognitionSource = data.RecognitionSource,
            ExtractedText = data.ExtractedText,
            ManualOverride = data.ManualOverride,
            UserIgnored = data.UserIgnored,
            Notes = data.Notes,
            IsThreeDPrinting = data.IsThreeDPrinting,
            DuplicateDismissed = data.DuplicateDismissed,
            Status = RecordStatus.NeedConfirmation
        };
        record.AttachedDocuments.Add(new AttachmentRecord
        {
            FilePath = record.OriginalFilePath,
            AttachmentType = AttachmentType.Invoice,
            RelatedInvoiceId = record.Id
        });
        foreach (var attachment in data.Attachments.Where(item => !string.IsNullOrWhiteSpace(item.FilePath)))
        {
            record.AttachedDocuments.Add(new AttachmentRecord
            {
                FilePath = attachment.FilePath,
                AttachmentType = attachment.AttachmentType,
                RelatedInvoiceId = record.Id,
                Amount = attachment.Amount,
                Date = attachment.Date,
                Confidence = attachment.Confidence
            });
        }

        return record;
    }
}
