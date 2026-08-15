using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ReimbursementAssistant.Models;

public sealed class InvoiceRecord : INotifyPropertyChanged
{
    public InvoiceRecord()
    {
        AttachedDocuments.CollectionChanged += (_, _) =>
        {
            OnChanged(nameof(SupplementAttachments));
            OnChanged(nameof(RequirementDisplays));
        };
    }

    public Guid Id { get; init; } = Guid.NewGuid();
    private int _displayIndex;
    public int DisplayIndex { get => _displayIndex; set => Set(ref _displayIndex, value); }
    private string _originalFilePath = "";
    public required string OriginalFilePath
    {
        get => _originalFilePath;
        set
        {
            if (Set(ref _originalFilePath, value))
            {
                OnChanged(nameof(OriginalFileName));
                OnChanged(nameof(FileType));
                OnChanged(nameof(ProjectMerchantDisplay));
            }
        }
    }
    public string? FileHash { get; init; }
    public string OriginalFileName => Path.GetFileName(OriginalFilePath);
    public string FileType => Path.GetExtension(OriginalFilePath).TrimStart('.').ToUpperInvariant();
    public string? InvoiceNumber { get; set; }
    public DateTime? InvoiceDate { get; set; }
    private string? _sellerName;
    public string? SellerName { get => _sellerName; set { if (Set(ref _sellerName, value)) OnChanged(nameof(ProjectMerchantDisplay)); } }
    public string? BuyerName { get; set; }

    private string? _itemDescription;
    public string? ItemDescription { get => _itemDescription; set { if (Set(ref _itemDescription, value)) OnChanged(nameof(ProjectMerchantDisplay)); } }

    public decimal? Amount { get; set; }
    public decimal? Tax { get; set; }

    private decimal? _totalAmount;
    public decimal? TotalAmount
    {
        get => _totalAmount;
        set
        {
            if (Set(ref _totalAmount, value))
            {
                OnChanged(nameof(RequirementDisplays));
            }
        }
    }

    private InvoiceCategory _category = InvoiceCategory.Unknown;
    public InvoiceCategory Category
    {
        get => _category;
        set
        {
            if (Set(ref _category, value))
            {
                OnChanged(nameof(CategoryDisplay));
                OnChanged(nameof(RequirementDisplays));
            }
        }
    }

    private TravelSubCategory _subCategory;
    public TravelSubCategory SubCategory
    {
        get => _subCategory;
        set
        {
            if (Set(ref _subCategory, value))
            {
                OnChanged(nameof(CategoryDisplay));
                OnChanged(nameof(RequirementDisplays));
            }
        }
    }

    public double Confidence { get; set; }
    public string RecognitionSummary { get; set; } = "尚未识别";
    public RecognitionSource RecognitionSource { get; set; }
    public string? ExtractedText { get; set; }
    public Collection<ValidationIssue> ValidationIssues { get; } = [];
    public ObservableCollection<AttachmentRecord> AttachedDocuments { get; } = [];
    public IEnumerable<AttachmentRecord> SupplementAttachments => AttachedDocuments.Where(x => x.AttachmentType != AttachmentType.Invoice);
    public Collection<AttachmentType> RequiredDocuments { get; } = [];
    private bool _isThreeDPrinting;
    public bool IsThreeDPrinting { get => _isThreeDPrinting; set { if (Set(ref _isThreeDPrinting, value)) OnChanged(nameof(RequirementDisplays)); } }

    private RecordStatus _status;
    public RecordStatus Status
    {
        get => _status;
        set
        {
            if (Set(ref _status, value))
            {
                OnChanged(nameof(StatusDisplay));
                OnChanged(nameof(MaterialStatusLabel));
                OnChanged(nameof(MaterialStatusBackground));
                OnChanged(nameof(MaterialStatusForeground));
            }
        }
    }

    public bool ManualOverride { get; set; }
    public bool UserIgnored { get; set; }
    public string? Notes { get; set; }
    private bool _isPossibleDuplicate;
    public bool IsPossibleDuplicate
    {
        get => _isPossibleDuplicate;
        set
        {
            if (Set(ref _isPossibleDuplicate, value))
            {
                OnChanged(nameof(DuplicateStatusDisplay));
                OnChanged(nameof(MaterialStatusLabel));
                OnChanged(nameof(MaterialStatusBackground));
                OnChanged(nameof(MaterialStatusForeground));
            }
        }
    }
    private string _duplicateHint = "";
    public string DuplicateHint { get => _duplicateHint; set => Set(ref _duplicateHint, value); }
    public bool DuplicateDismissed { get; set; }
    public string DuplicateStatusDisplay => IsPossibleDuplicate ? "疑似重复" : "";
    public bool OriginalFileExists => File.Exists(OriginalFilePath);
    public bool HasMissingFiles => !OriginalFileExists || SupplementAttachments.Any(attachment => !File.Exists(attachment.FilePath));
    public string MissingFilesDisplay => !OriginalFileExists
        ? "原始发票文件已被移动或删除"
        : SupplementAttachments.FirstOrDefault(attachment => !File.Exists(attachment.FilePath)) is { } missing
            ? $"附件不存在：{missing.FileName}"
            : "";
    public IReadOnlyList<EnumOption<InvoiceCategory>> CategoryOptions { get; } =
    [
        new(InvoiceCategory.Consumable, "耗材"),
        new(InvoiceCategory.Travel, "差旅"),
        new(InvoiceCategory.PrintFee, "打印费"),
        new(InvoiceCategory.ShippingFee, "邮寄费"),
        new(InvoiceCategory.Other, "其他")
    ];

    public IReadOnlyList<EnumOption<TravelSubCategory>> SubCategoryOptions { get; } =
    [
        new(TravelSubCategory.None, "无"),
        new(TravelSubCategory.Flight, "飞机"),
        new(TravelSubCategory.Train, "火车"),
        new(TravelSubCategory.Hotel, "住宿"),
        new(TravelSubCategory.Taxi, "出租车/网约车"),
        new(TravelSubCategory.RentalCar, "租车"),
        new(TravelSubCategory.Toll, "路桥费"),
        new(TravelSubCategory.Fuel, "燃油费"),
        new(TravelSubCategory.OtherTravel, "其他差旅")
    ];

    public string CategoryDisplay => Category == InvoiceCategory.Travel && SubCategory != TravelSubCategory.None
        ? $"差旅 / {SubCategoryDisplay}"
        : Category switch
        {
            InvoiceCategory.Consumable => "耗材",
            InvoiceCategory.Travel => "差旅",
            InvoiceCategory.PrintFee => "打印费",
            InvoiceCategory.ShippingFee => "邮寄费",
            InvoiceCategory.Other => "其他",
            _ => "待确认"
        };

    public string SubCategoryDisplay => SubCategory switch
    {
        TravelSubCategory.Flight => "飞机",
        TravelSubCategory.Train => "火车",
        TravelSubCategory.Hotel => "住宿",
        TravelSubCategory.Taxi => "出租车/网约车",
        TravelSubCategory.RentalCar => "租车",
        TravelSubCategory.Toll => "路桥费",
        TravelSubCategory.Fuel => "燃油费",
        TravelSubCategory.OtherTravel => "其他差旅",
        _ => ""
    };

    public string StatusDisplay => Status switch
    {
        RecordStatus.Complete => "✓ 材料完整",
        RecordStatus.MissingDocuments => "⚠ 缺少" + string.Join("、", MissingTypes().Select(DisplayAttachment)),
        RecordStatus.NeedConfirmation => "待确认分类",
        RecordStatus.Error => "识别失败",
        RecordStatus.Ignored => "已忽略",
        _ => "分析中"
    };

    public string MaterialStatusLabel => HasMissingFiles ? "文件丢失" : IsPossibleDuplicate ? "疑似重复" : Status switch
    {
        RecordStatus.Complete => "完整",
        RecordStatus.MissingDocuments => "缺" + string.Join("、", MissingTypes().Select(DisplayAttachment)),
        RecordStatus.NeedConfirmation => "待确认",
        RecordStatus.Error => "识别失败",
        RecordStatus.Ignored => "已忽略",
        _ => "分析中"
    };

    public string MaterialStatusBackground => HasMissingFiles ? "#FEE2E2" : IsPossibleDuplicate ? "#FCE7F3" : Status switch
    {
        RecordStatus.Complete => "#DCFCE7",
        RecordStatus.MissingDocuments => "#FEF3C7",
        RecordStatus.NeedConfirmation => "#E2E8F0",
        RecordStatus.Error => "#FEE2E2",
        _ => "#DBEAFE"
    };

    public string MaterialStatusForeground => HasMissingFiles ? "#991B1B" : IsPossibleDuplicate ? "#9D174D" : Status switch
    {
        RecordStatus.Complete => "#166534",
        RecordStatus.MissingDocuments => "#92400E",
        RecordStatus.NeedConfirmation => "#475569",
        RecordStatus.Error => "#991B1B",
        _ => "#1D4ED8"
    };

    public string ProjectMerchantDisplay
    {
        get
        {
            var item = string.IsNullOrWhiteSpace(ItemDescription) ? Path.GetFileNameWithoutExtension(OriginalFileName) : ItemDescription;
            return string.IsNullOrWhiteSpace(SellerName) ? item : $"{item} / {SellerName}";
        }
    }

    public string RecognitionMethodDisplay => RecognitionSource switch
    {
        RecognitionSource.NativePdfText => "PDF文本解析",
        RecognitionSource.PaddleOcrVl16 => "Paddle深度识别",
        RecognitionSource.WindowsOcr => "Windows本地OCR",
        _ => "尚未识别"
    };

    public IEnumerable<string> RequirementDisplays => RequiredDocuments.Select(x => (AttachedDocuments.Any(a => a.AttachmentType == x) ? "✓ " : "⚠ ") + DisplayAttachment(x));
    public IEnumerable<string> ValidationDisplays => ValidationIssues.Select(x => (x.Severity == ValidationSeverity.Error ? "✕ " : "⚠ ") + x.Message);
    public IEnumerable<AttachmentType> MissingTypes() => RequiredDocuments.Where(x => !AttachedDocuments.Any(a => a.AttachmentType == x));

    public static string DisplayAttachment(AttachmentType type) => type switch
    {
        AttachmentType.Invoice => "发票",
        AttachmentType.PaymentProof => "支付记录截图",
        AttachmentType.OrderPage => "订单页面",
        AttachmentType.ThreeDPrintDetails => "3D打印明细",
        _ => "其他材料"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;

        field = value;
        OnChanged(propertyName);
        return true;
    }

    public void OnChanged(string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void RefreshFileState()
    {
        OnChanged(nameof(OriginalFileExists));
        OnChanged(nameof(HasMissingFiles));
        OnChanged(nameof(MissingFilesDisplay));
        OnChanged(nameof(MaterialStatusLabel));
        OnChanged(nameof(MaterialStatusBackground));
        OnChanged(nameof(MaterialStatusForeground));
    }
}

public sealed record EnumOption<T>(T Value, string Label);
