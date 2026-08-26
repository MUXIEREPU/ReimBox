namespace ReimbursementAssistant.Models;

public sealed class ReimbursementProjectDocument
{
    public int SchemaVersion { get; set; } = 1;
    public DateTime SavedAt { get; set; } = DateTime.Now;
    public string PersonName { get; set; } = "";
    public string PersonIdentifier { get; set; } = "";
    public string Description { get; set; } = "";
    public List<NamingField> NamingFields { get; set; } = [];
    public string NamingSeparator { get; set; } = "_";
    public List<ProjectInvoiceData> Invoices { get; set; } = [];
}

public sealed class ProjectInvoiceData
{
    public int DisplayIndex { get; set; }
    public string OriginalFilePath { get; set; } = "";
    public string? FileHash { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public string? SellerName { get; set; }
    public string? BuyerName { get; set; }
    public string? ItemDescription { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Tax { get; set; }
    public decimal? TotalAmount { get; set; }
    public InvoiceCategory Category { get; set; }
    public TravelSubCategory SubCategory { get; set; }
    public double Confidence { get; set; }
    public double ClassificationConfidence { get; set; }
    public string RecognitionSummary { get; set; } = "";
    public RecognitionSource RecognitionSource { get; set; }
    public string? ExtractedText { get; set; }
    public bool ManualOverride { get; set; }
    public bool UserIgnored { get; set; }
    public string? Notes { get; set; }
    public bool IsThreeDPrinting { get; set; }
    public bool DuplicateDismissed { get; set; }
    public List<ProjectAttachmentData> Attachments { get; set; } = [];
}

public sealed class ProjectAttachmentData
{
    public string FilePath { get; set; } = "";
    public AttachmentType AttachmentType { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? Date { get; set; }
    public double Confidence { get; set; }
}

public sealed record ProjectLoadResult(
    IReadOnlyList<InvoiceRecord> Records,
    ReimbursementInfo ReimbursementInfo,
    InvoiceNamingRule NamingRule,
    DateTime SavedAt);
