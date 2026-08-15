namespace ReimbursementAssistant.Models;

public sealed class BulkEditRequest
{
    public bool ChangeCategory { get; set; }
    public InvoiceCategory Category { get; set; } = InvoiceCategory.Consumable;
    public bool ChangeSubCategory { get; set; }
    public TravelSubCategory SubCategory { get; set; } = TravelSubCategory.None;
    public bool ChangeAttachmentType { get; set; }
    public AttachmentType AttachmentType { get; set; } = AttachmentType.Other;
    public bool MarkIgnored { get; set; }
    public bool RestoreIgnored { get; set; }
    public bool Reanalyze { get; set; }
    public bool RemoveFromList { get; set; }
}
