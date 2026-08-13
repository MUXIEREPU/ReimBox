namespace ReimbursementAssistant.Models;
public sealed class AttachmentRecord : System.ComponentModel.INotifyPropertyChanged
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string FilePath { get; init; }
    public string FileName => Path.GetFileName(FilePath);
    public bool FileExists => File.Exists(FilePath);
    public bool IsImage => new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" }.Contains(Path.GetExtension(FilePath), StringComparer.OrdinalIgnoreCase);
    private AttachmentType _attachmentType = AttachmentType.Other;
    public AttachmentType AttachmentType
    {
        get => _attachmentType;
        set
        {
            if (_attachmentType == value) return;
            _attachmentType = value;
            OnChanged(nameof(AttachmentType));
            OnChanged(nameof(AttachmentTypeDisplay));
            OnChanged(nameof(DisplayText));
            OnChanged(nameof(IsSupplement));
        }
    }
    public string AttachmentTypeDisplay => InvoiceRecord.DisplayAttachment(AttachmentType);
    public string DisplayText => $"{AttachmentTypeDisplay}：{FileName}";
    public bool IsSupplement => AttachmentType != AttachmentType.Invoice;
    public IReadOnlyList<EnumOption<AttachmentType>> AttachmentTypeOptions { get; } =
    [
        new(AttachmentType.PaymentProof, "支付记录截图"),
        new(AttachmentType.OrderPage, "订单页面"),
        new(AttachmentType.ThreeDPrintDetails, "3D打印明细"),
        new(AttachmentType.Other, "其他材料")
    ];
    public Guid? RelatedInvoiceId { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? Date { get; set; }
    public double Confidence { get; set; }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string propertyName) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
}
