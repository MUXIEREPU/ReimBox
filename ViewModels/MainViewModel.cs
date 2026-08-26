using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using ReimbursementAssistant.Configuration;
using ReimbursementAssistant.Models;
using ReimbursementAssistant.Services;

namespace ReimbursementAssistant.ViewModels;

public sealed partial class MainViewModel : INotifyPropertyChanged
{
    private readonly SettingsService _settingsService = new();
    private readonly ReimbursementSettings _settings;
    private readonly ReimbursementRuleEngine _rules;
    private readonly FileImportService _import = new(new HashService());
    private readonly ExportService _export;
    private readonly InvoiceAnalysisService _analysis;
    private readonly PdfPreviewService _pdfPreview = new();
    private readonly InvoiceNamingService _naming = new();
    private readonly OperationLogService _log = new();
    private readonly DuplicateInvoiceService _duplicateInvoices = new();
    private readonly CorrectionLearningService _learning = new();
    private readonly HashSet<string> _hashes = [];
    private InvoiceNamingRule _namingRule = InvoiceNamingRule.Default();

    private bool _isBusy;
    private bool _isProgrammaticUpdate;
    private bool _isImportDragOver;
    private bool _isAttachmentDragOver;
    private bool _isPreviewMode;
    private bool _isPdfPreviewWindowOpen;
    private bool _isPdfPreviewLoading;
    private InvoiceListFilter _activeFilter = InvoiceListFilter.All;
    private string _progressMessage = "就绪";
    private string _searchText = "";
    private double _operationProgress;
    private bool _isProgressIndeterminate;
    private string _pdfPreviewStatus = "请选择一张 PDF 发票进行预览";
    private string _reimbursementPersonName = "";
    private string _reimbursementPersonIdentifier = "";
    private string _reimbursementDescription = "";
    private double _pdfPreviewZoom = 1.0;
    private int _pdfPreviewPageIndex;
    private int _pdfPreviewPageCount;
    private ImageSource? _pdfPreviewImage;
    private InvoiceRecord? _selectedRecord;
    private readonly List<(InvoiceRecord Record, int Index)> _lastDeletedRecords = [];
    private int _pdfPreviewRequestVersion;
    private string? _pdfPreviewPath;

    public ObservableCollection<InvoiceRecord> Records { get; } = [];
    public ICollectionView RecordsView { get; }
    public InvoiceRecord? SelectedRecord
    {
        get => _selectedRecord;
        set
        {
            _selectedRecord = value;
            OnChanged();
            AnalyzeCommand.RaiseCanExecuteChanged();
            PaddleAnalyzeCommand.RaiseCanExecuteChanged();
            OpenSelectedFileCommand.RaiseCanExecuteChanged();
            RevealSelectedFileCommand.RaiseCanExecuteChanged();
            DeleteSelectedCommand.RaiseCanExecuteChanged();
            ChooseAttachmentFilesCommand.RaiseCanExecuteChanged();
            PreviousPdfPageCommand.RaiseCanExecuteChanged();
            NextPdfPageCommand.RaiseCanExecuteChanged();
            ResetPdfPreviewCommand.RaiseCanExecuteChanged();
            NewProjectCommand?.RaiseCanExecuteChanged();
            OpenProjectCommand?.RaiseCanExecuteChanged();
            SaveProjectCommand?.RaiseCanExecuteChanged();
            SaveProjectAsCommand?.RaiseCanExecuteChanged();
            RecheckCommand.RaiseCanExecuteChanged();
            UndoDeleteCommand.RaiseCanExecuteChanged();
            DismissDuplicateCommand.RaiseCanExecuteChanged();
            if (IsPreviewMode || IsPdfPreviewWindowOpen)
            {
                _ = LoadPdfPreviewAsync(0);
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnChanged();
            ImportCommand.RaiseCanExecuteChanged();
            ImportFolderCommand.RaiseCanExecuteChanged();
            ExportCommand.RaiseCanExecuteChanged();
            AnalyzeCommand.RaiseCanExecuteChanged();
            PaddleAnalyzeCommand.RaiseCanExecuteChanged();
            OpenSelectedFileCommand.RaiseCanExecuteChanged();
            RevealSelectedFileCommand.RaiseCanExecuteChanged();
            DeleteSelectedCommand.RaiseCanExecuteChanged();
            ChooseAttachmentFilesCommand.RaiseCanExecuteChanged();
            PreviousPdfPageCommand.RaiseCanExecuteChanged();
            NextPdfPageCommand.RaiseCanExecuteChanged();
            ResetPdfPreviewCommand.RaiseCanExecuteChanged();
        }
    }

    public string ProgressMessage { get => _progressMessage; private set { _progressMessage = value; OnChanged(); } }
    public string ReimbursementPersonName { get => _reimbursementPersonName; set { _reimbursementPersonName = value; OnChanged(); OnChanged(nameof(HasRequiredReimbursementInfo)); OnChanged(nameof(ReimbursementInfoButtonText)); } }
    public string ReimbursementPersonIdentifier { get => _reimbursementPersonIdentifier; set { _reimbursementPersonIdentifier = value; OnChanged(); OnChanged(nameof(HasRequiredReimbursementInfo)); OnChanged(nameof(ReimbursementInfoButtonText)); } }
    public string ReimbursementDescription { get => _reimbursementDescription; set { _reimbursementDescription = value; OnChanged(); } }
    public bool HasRequiredReimbursementInfo => !string.IsNullOrWhiteSpace(ReimbursementPersonName) && !string.IsNullOrWhiteSpace(ReimbursementPersonIdentifier);
    public string ReimbursementInfoButtonText => HasRequiredReimbursementInfo ? "说明已填写" : "添加说明";
    public bool IsImportDragOver { get => _isImportDragOver; set { _isImportDragOver = value; OnChanged(); } }
    public bool IsAttachmentDragOver { get => _isAttachmentDragOver; set { _isAttachmentDragOver = value; OnChanged(); } }
    public bool IsPreviewMode { get => _isPreviewMode; private set { _isPreviewMode = value; OnChanged(); OnChanged(nameof(IsDetailMode)); } }
    public bool IsDetailMode => !IsPreviewMode;
    public bool IsPdfPreviewWindowOpen
    {
        get => _isPdfPreviewWindowOpen;
        set
        {
            _isPdfPreviewWindowOpen = value;
            OnChanged();
            PreviousPdfPageCommand.RaiseCanExecuteChanged();
            NextPdfPageCommand.RaiseCanExecuteChanged();
            ResetPdfPreviewCommand.RaiseCanExecuteChanged();
            if (value && SelectedRecord is not null)
            {
                _ = LoadPdfPreviewAsync(0);
            }
        }
    }
    public bool IsPdfPreviewLoading { get => _isPdfPreviewLoading; private set { _isPdfPreviewLoading = value; OnChanged(); PreviousPdfPageCommand.RaiseCanExecuteChanged(); NextPdfPageCommand.RaiseCanExecuteChanged(); ResetPdfPreviewCommand.RaiseCanExecuteChanged(); } }
    public double PdfPreviewZoom { get => _pdfPreviewZoom; set { _pdfPreviewZoom = Math.Clamp(value, 0.25, 5.0); OnChanged(); OnChanged(nameof(PdfPreviewZoomPercent)); } }
    public string PdfPreviewZoomPercent => $"{PdfPreviewZoom:P0}";
    public int PdfPreviewPageIndex { get => _pdfPreviewPageIndex; private set { _pdfPreviewPageIndex = value; OnChanged(); OnChanged(nameof(PdfPreviewPageDisplay)); PreviousPdfPageCommand.RaiseCanExecuteChanged(); NextPdfPageCommand.RaiseCanExecuteChanged(); } }
    public int PdfPreviewPageCount { get => _pdfPreviewPageCount; private set { _pdfPreviewPageCount = value; OnChanged(); OnChanged(nameof(PdfPreviewPageDisplay)); PreviousPdfPageCommand.RaiseCanExecuteChanged(); NextPdfPageCommand.RaiseCanExecuteChanged(); } }
    public string PdfPreviewPageDisplay => PdfPreviewPageCount <= 0 ? "第 - / - 页" : $"第 {PdfPreviewPageIndex + 1} / {PdfPreviewPageCount} 页";
    public ImageSource? PdfPreviewImage { get => _pdfPreviewImage; private set { _pdfPreviewImage = value; OnChanged(); } }
    public string PdfPreviewStatus { get => _pdfPreviewStatus; private set { _pdfPreviewStatus = value; OnChanged(); } }
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (string.Equals(_searchText, value, StringComparison.Ordinal)) return;
            _searchText = value ?? "";
            OnChanged();
            OnChanged(nameof(HasSearchText));
            RecordsView.Refresh();
            EnsureVisibleSelection();
            OnChanged(nameof(HasVisibleRecords));
            OnChanged(nameof(EmptyStateMessage));
            ClearSearchCommand.RaiseCanExecuteChanged();
        }
    }
    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);
    public double OperationProgress { get => _operationProgress; private set { _operationProgress = Math.Clamp(value, 0, 100); OnChanged(); } }
    public bool IsProgressIndeterminate { get => _isProgressIndeterminate; private set { _isProgressIndeterminate = value; OnChanged(); } }
    public InvoiceListFilter ActiveFilter
    {
        get => _activeFilter;
        private set
        {
            if (_activeFilter == value) return;
            _activeFilter = value;
            OnChanged();
            OnChanged(nameof(IsAllFilterActive));
            OnChanged(nameof(IsConsumableFilterActive));
            OnChanged(nameof(IsTravelFilterActive));
            OnChanged(nameof(IsPrintFeeFilterActive));
            OnChanged(nameof(IsShippingFeeFilterActive));
            OnChanged(nameof(IsOtherFilterActive));
            OnChanged(nameof(IsPendingFilterActive));
            OnChanged(nameof(IsDuplicateFilterActive));
            RecordsView.Refresh();
            EnsureVisibleSelection();
            OnChanged(nameof(HasVisibleRecords));
            OnChanged(nameof(EmptyStateMessage));
        }
    }
    public bool IsAllFilterActive => ActiveFilter == InvoiceListFilter.All;
    public bool IsConsumableFilterActive => ActiveFilter == InvoiceListFilter.Consumable;
    public bool IsTravelFilterActive => ActiveFilter == InvoiceListFilter.Travel;
    public bool IsPrintFeeFilterActive => ActiveFilter == InvoiceListFilter.PrintFee;
    public bool IsShippingFeeFilterActive => ActiveFilter == InvoiceListFilter.ShippingFee;
    public bool IsOtherFilterActive => ActiveFilter == InvoiceListFilter.Other;
    public bool IsPendingFilterActive => ActiveFilter == InvoiceListFilter.Pending;
    public bool IsDuplicateFilterActive => ActiveFilter == InvoiceListFilter.Duplicate;
    public bool HasRecords => Records.Count > 0;
    public bool HasVisibleRecords => !RecordsView.IsEmpty;
    public string EmptyStateMessage => Records.Count == 0
        ? "还没有发票\n点击上方按钮或将 PDF 拖入窗口"
        : HasSearchText ? "没有找到匹配的发票\n请尝试其他关键词" : "当前筛选下没有发票";
    public bool HasUndoDelete => _lastDeletedRecords.Count > 0;
    public string UndoDeleteText => _lastDeletedRecords.Count switch
    {
        0 => "撤销删除",
        1 => $"撤销删除：{_lastDeletedRecords[0].Record.OriginalFileName}",
        _ => $"撤销删除：{_lastDeletedRecords.Count} 张发票"
    };
    public int ConsumableCount => Records.Count(x => !x.UserIgnored && x.Category == InvoiceCategory.Consumable);
    public int TravelCount => Records.Count(x => !x.UserIgnored && x.Category == InvoiceCategory.Travel);
    public int PrintFeeCount => Records.Count(x => !x.UserIgnored && x.Category == InvoiceCategory.PrintFee);
    public int ShippingFeeCount => Records.Count(x => !x.UserIgnored && x.Category == InvoiceCategory.ShippingFee);
    public int OtherCount => Records.Count(x => !x.UserIgnored && x.Category == InvoiceCategory.Other);
    public int DuplicateCount => Records.Count(x => x.IsPossibleDuplicate);
    public int MissingCount => Records.Count(x => x.Status == RecordStatus.MissingDocuments);
    public int PendingCount => Records.Count(IsPending);
    public int CompleteCount => Records.Count(x => x.Status == RecordStatus.Complete);
    public int ExportableCount => Records.Count(x => !x.UserIgnored);
    public string FooterSummary => $"共 {Records.Count} 笔 | 完整 {CompleteCount} | 待处理 {PendingCount}";
    public decimal ConsumableTotal => Records.Where(x => !x.UserIgnored && x.Category == InvoiceCategory.Consumable).Sum(x => x.TotalAmount ?? 0);
    public decimal TravelTotal => Records.Where(x => !x.UserIgnored && x.Category == InvoiceCategory.Travel).Sum(x => x.TotalAmount ?? 0);
    public decimal PrintFeeTotal => Records.Where(x => !x.UserIgnored && x.Category == InvoiceCategory.PrintFee).Sum(x => x.TotalAmount ?? 0);
    public decimal ShippingFeeTotal => Records.Where(x => !x.UserIgnored && x.Category == InvoiceCategory.ShippingFee).Sum(x => x.TotalAmount ?? 0);
    public decimal OtherTotal => Records.Where(x => !x.UserIgnored && x.Category == InvoiceCategory.Other).Sum(x => x.TotalAmount ?? 0);
    public decimal UnknownTotal => Records.Where(x => !x.UserIgnored && x.Category == InvoiceCategory.Unknown).Sum(x => x.TotalAmount ?? 0);
    public decimal GrandTotal => Records.Where(x => !x.UserIgnored).Sum(x => x.TotalAmount ?? 0);
    public string AmountSummary => $"耗材 ¥{ConsumableTotal:N2}  |  差旅 ¥{TravelTotal:N2}  |  打印费 ¥{PrintFeeTotal:N2}  |  邮寄费 ¥{ShippingFeeTotal:N2}  |  其他 ¥{OtherTotal:N2}  |  待确认 ¥{UnknownTotal:N2}  |  总计 ¥{GrandTotal:N2}";
    public InvoiceNamingRule NamingRule => _namingRule.Clone();

    public RelayCommand ImportCommand { get; }
    public RelayCommand ImportFolderCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand RecheckCommand { get; }
    public RelayCommand AnalyzeCommand { get; }
    public RelayCommand PaddleAnalyzeCommand { get; }
    public RelayCommand ShowAllCommand { get; }
    public RelayCommand ShowConsumableCommand { get; }
    public RelayCommand ShowTravelCommand { get; }
    public RelayCommand ShowPrintFeeCommand { get; }
    public RelayCommand ShowShippingFeeCommand { get; }
    public RelayCommand ShowOtherCommand { get; }
    public RelayCommand ShowPendingCommand { get; }
    public RelayCommand ShowDuplicateCommand { get; }
    public RelayCommand ShowDetailCommand { get; }
    public RelayCommand ShowPreviewCommand { get; }
    public RelayCommand OpenSelectedFileCommand { get; }
    public RelayCommand RevealSelectedFileCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }
    public RelayCommand ChooseAttachmentFilesCommand { get; }
    public RelayCommand PreviousPdfPageCommand { get; }
    public RelayCommand NextPdfPageCommand { get; }
    public RelayCommand ResetPdfPreviewCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public RelayCommand UndoDeleteCommand { get; }
    public RelayCommand DismissDuplicateCommand { get; }

    public MainViewModel()
    {
        _settings = _settingsService.Load();
        RecordsView = CollectionViewSource.GetDefaultView(Records);
        RecordsView.Filter = FilterRecord;

        _rules = new ReimbursementRuleEngine(_settings);
        _export = new ExportService();
        _analysis = new InvoiceAnalysisService(new PdfTextService(), new PaddleOcrVlService(_settings), new WindowsOcrService(), new InvoiceClassificationService(), new InvoiceValidationService());

        ImportCommand = new RelayCommand(ChooseFiles, () => !IsBusy);
        ImportFolderCommand = new RelayCommand(ChooseFolder, () => !IsBusy);
        ExportCommand = new RelayCommand(Export, () => Records.Count > 0 && !IsBusy);
        RecheckCommand = new RelayCommand(RecheckAll, () => !IsBusy);
        AnalyzeCommand = new RelayCommand(AnalyzeSelectedFast, () => SelectedRecord is not null && !IsBusy);
        PaddleAnalyzeCommand = new RelayCommand(AnalyzeSelectedWithPaddle, () => SelectedRecord is not null && !IsBusy);
        ShowAllCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.All);
        ShowConsumableCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.Consumable);
        ShowTravelCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.Travel);
        ShowPrintFeeCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.PrintFee);
        ShowShippingFeeCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.ShippingFee);
        ShowOtherCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.Other);
        ShowPendingCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.Pending);
        ShowDuplicateCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.Duplicate);
        ShowDetailCommand = new RelayCommand(() => IsPreviewMode = false);
        ShowPreviewCommand = new RelayCommand(() =>
        {
            IsPreviewMode = true;
            _ = LoadPdfPreviewAsync(0);
        });
        OpenSelectedFileCommand = new RelayCommand(OpenSelectedFile, () => SelectedRecord is not null && File.Exists(SelectedRecord.OriginalFilePath));
        RevealSelectedFileCommand = new RelayCommand(RevealSelectedFile, () => SelectedRecord is not null && File.Exists(SelectedRecord.OriginalFilePath));
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => SelectedRecord is not null && !IsBusy);
        ChooseAttachmentFilesCommand = new RelayCommand(ChooseAttachmentFiles, () => SelectedRecord is not null && !IsBusy);
        PreviousPdfPageCommand = new RelayCommand(() => _ = LoadPdfPreviewAsync(PdfPreviewPageIndex - 1), () => (IsPreviewMode || IsPdfPreviewWindowOpen) && !IsPdfPreviewLoading && PdfPreviewPageIndex > 0);
        NextPdfPageCommand = new RelayCommand(() => _ = LoadPdfPreviewAsync(PdfPreviewPageIndex + 1), () => (IsPreviewMode || IsPdfPreviewWindowOpen) && !IsPdfPreviewLoading && PdfPreviewPageCount > 0 && PdfPreviewPageIndex < PdfPreviewPageCount - 1);
        ResetPdfPreviewCommand = new RelayCommand(() => PdfPreviewZoom = 1.0, () => (IsPreviewMode || IsPdfPreviewWindowOpen) && PdfPreviewImage is not null && !IsPdfPreviewLoading);
        ClearSearchCommand = new RelayCommand(() => SearchText = "", () => HasSearchText);
        UndoDeleteCommand = new RelayCommand(UndoDelete, () => HasUndoDelete && !IsBusy);
        DismissDuplicateCommand = new RelayCommand(DismissSelectedDuplicate, () => SelectedRecord?.IsPossibleDuplicate == true && !IsBusy);
        InitializeProjectFeatures();
    }

    private void ChooseAttachmentFiles()
    {
        if (SelectedRecord is null) return;

        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "所有附件|*.*",
            Title = $"选择要关联到 {SelectedRecord.OriginalFileName} 的附件"
        };

        if (dialog.ShowDialog() == true)
        {
            AttachFiles(dialog.FileNames);
        }
    }

    private void DeleteSelected()
    {
        var record = SelectedRecord;
        if (record is null) return;

        var index = Records.IndexOf(record);
        _lastDeletedRecords.Clear();
        _lastDeletedRecords.Add((record, index));
        record.PropertyChanged -= Record_PropertyChanged;
        Records.Remove(record);
        if (!string.IsNullOrWhiteSpace(record.FileHash))
        {
            _hashes.Remove(record.FileHash);
        }

        ReindexRecords();
        SelectedRecord = Records.Count == 0 ? null : Records[Math.Clamp(index, 0, Records.Count - 1)];
        RecordsView.Refresh();
        EnsureVisibleSelection();
        RefreshStats();
        ProgressMessage = $"已从列表删除：{record.OriginalFileName}（原始文件未修改）";
        ScheduleAutosave();
        OnChanged(nameof(HasUndoDelete));
        OnChanged(nameof(UndoDeleteText));
        UndoDeleteCommand.RaiseCanExecuteChanged();
    }

    private void UndoDelete()
    {
        if (_lastDeletedRecords.Count == 0) return;

        var candidates = _lastDeletedRecords.OrderBy(item => item.Index).ToList();
        var restoredRecords = new List<(InvoiceRecord Record, int Index)>();
        var skippedDuplicates = 0;
        foreach (var (record, originalIndex) in candidates)
        {
            if (!string.IsNullOrWhiteSpace(record.FileHash) && _hashes.Contains(record.FileHash))
            {
                skippedDuplicates++;
                continue;
            }

            var insertIndex = Math.Clamp(originalIndex, 0, Records.Count);
            RegisterRecord(record);
            Records.Insert(insertIndex, record);
            restoredRecords.Add((record, insertIndex));
        }

        _lastDeletedRecords.Clear();
        ReindexRecords();
        if (restoredRecords.Count > 0) SelectedRecord = restoredRecords[0].Record;
        RecordsView.Refresh();
        RefreshStats();
        ProgressMessage = restoredRecords.Count switch
        {
            0 when skippedDuplicates > 0 => "没有恢复：相同文件已重新导入",
            1 => $"已恢复：{restoredRecords[0].Record.OriginalFileName}",
            _ => $"已恢复 {restoredRecords.Count} 张发票" + (skippedDuplicates > 0 ? $"，跳过 {skippedDuplicates} 张重复文件" : "")
        };
        ScheduleAutosave();
        OnChanged(nameof(HasUndoDelete));
        OnChanged(nameof(UndoDeleteText));
        UndoDeleteCommand.RaiseCanExecuteChanged();
    }

    private async Task LoadPdfPreviewAsync(int? requestedPageIndex = null)
    {
        var requestVersion = ++_pdfPreviewRequestVersion;
        var record = SelectedRecord;

        if (record is null)
        {
            ClearPdfPreview("请选择一张 PDF 发票进行预览");
            return;
        }

        var previewPath = record.OriginalFilePath;
        var pathChanged = !string.Equals(previewPath, _pdfPreviewPath, StringComparison.OrdinalIgnoreCase);
        if (pathChanged)
        {
            _pdfPreviewPath = null;
            PdfPreviewImage = null;
            PdfPreviewPageCount = 0;
            PdfPreviewPageIndex = 0;
            PdfPreviewZoom = 1.0;
        }

        if (!File.Exists(previewPath))
        {
            ClearPdfPreview("原始文件不存在，无法预览");
            return;
        }

        if (!string.Equals(Path.GetExtension(previewPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            ClearPdfPreview("当前文件不是 PDF，暂不支持在这里预览");
            return;
        }

        PdfPreviewStatus = $"正在生成预览：{record.OriginalFileName}";
        IsPdfPreviewLoading = true;

        try
        {
            var pageIndex = requestedPageIndex ?? (pathChanged ? 0 : PdfPreviewPageIndex);
            var frame = await _pdfPreview.RenderAsync(previewPath, pageIndex);
            if (requestVersion != _pdfPreviewRequestVersion || !ReferenceEquals(record, SelectedRecord) ||
                !string.Equals(previewPath, SelectedRecord?.OriginalFilePath, StringComparison.OrdinalIgnoreCase))
                return;

            PdfPreviewPageCount = frame.PageCount;
            PdfPreviewPageIndex = frame.PageIndex;
            PdfPreviewImage = frame.Image;
            _pdfPreviewPath = previewPath;
            PdfPreviewStatus = "左键按住拖动 · 鼠标滚轮缩放";
            ResetPdfPreviewCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            if (requestVersion == _pdfPreviewRequestVersion && ReferenceEquals(record, SelectedRecord))
                ClearPdfPreview($"预览失败：{ToFriendlyError(ex)}");
        }
        finally
        {
            if (requestVersion == _pdfPreviewRequestVersion)
                IsPdfPreviewLoading = false;
        }
    }

    private void ClearPdfPreview(string status)
    {
        IsPdfPreviewLoading = false;
        _pdfPreviewPath = null;
        PdfPreviewImage = null;
        PdfPreviewPageCount = 0;
        PdfPreviewPageIndex = 0;
        PdfPreviewStatus = status;
        ResetPdfPreviewCommand.RaiseCanExecuteChanged();
    }

    private void OpenSelectedFile()
    {
        if (SelectedRecord is null || !File.Exists(SelectedRecord.OriginalFilePath)) return;

        Process.Start(new ProcessStartInfo(SelectedRecord.OriginalFilePath)
        {
            UseShellExecute = true
        });
    }

    private void RevealSelectedFile()
    {
        if (SelectedRecord is null || !File.Exists(SelectedRecord.OriginalFilePath)) return;

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{SelectedRecord.OriginalFilePath}\"")
        {
            UseShellExecute = true
        });
    }

    private void ChooseFiles()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "PDF发票|*.pdf|所有文件|*.*" };
        if (dialog.ShowDialog() == true) ImportPaths(dialog.FileNames);
    }

    private void ChooseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择根文件夹（将递归导入所有子文件夹中的 PDF）"
        };
        if (dialog.ShowDialog() == true) ImportPaths([dialog.FolderName]);
    }

    public async void ImportPaths(IEnumerable<string> paths)
    {
        if (IsBusy) return;

        var importPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (importPaths.Count == 0) return;

        var includesFolder = importPaths.Any(Directory.Exists);

        IsBusy = true;
        IsProgressIndeterminate = true;
        OperationProgress = 0;
        ProgressMessage = includesFolder
            ? "正在递归扫描文件夹及其所有子文件夹中的 PDF…"
            : "正在扫描 PDF 并检查重复项…";
        try
        {
            var result = await Task.Run(() => _import.Import(importPaths, _hashes));
            var imported = result.Records.ToList();
            if (imported.Count == 0)
            {
                ProgressMessage = result.SkippedUnsupported > 0
                    ? $"只支持导入 PDF 发票，已跳过 {result.SkippedUnsupported} 个非 PDF 文件"
                    : result.Errors.Count > 0 ? "部分文件无法读取，请查看导入结果" : "没有发现新的 PDF 发票，或文件已经导入过";
                if (HasImportNotes(result))
                {
                    System.Windows.MessageBox.Show(BuildImportSummary(imported.Count, result), "导入结果", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                return;
            }

            foreach (var record in imported)
            {
                record.DisplayIndex = Records.Count + 1;
                record.AttachedDocuments.Add(new AttachmentRecord { FilePath = record.OriginalFilePath, AttachmentType = AttachmentType.Invoice, RelatedInvoiceId = record.Id });
                RegisterRecord(record);
                Records.Add(record);
            }

            SelectedRecord ??= Records.FirstOrDefault();
            RefreshStats();
            if (HasImportNotes(result))
            {
                ProgressMessage = $"已导入 {imported.Count} 个 PDF，部分文件已跳过";
                System.Windows.MessageBox.Show(BuildImportSummary(imported.Count, result), "导入结果", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }

            IsBusy = false;
            IsProgressIndeterminate = false;
            await AnalyzeRecordsAsync(imported, allowPaddle: _settings.EnablePaddleAutoFallback);
            ScheduleAutosave();
        }
        catch (Exception ex)
        {
            ProgressMessage = $"导入失败：{ToFriendlyError(ex)}";
            System.Windows.MessageBox.Show(ProgressMessage, "导入失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
        }
    }

    private static bool HasImportNotes(FileImportResult result) =>
        result.SkippedUnsupported > 0 || result.SkippedDuplicate > 0 || result.Errors.Count > 0;

    private static string BuildImportSummary(int importedCount, FileImportResult result)
    {
        var lines = new List<string>
        {
            $"成功导入 PDF 发票：{importedCount} 个"
        };

        if (result.SkippedUnsupported > 0)
        {
            lines.Add($"已跳过非 PDF 文件：{result.SkippedUnsupported} 个");
        }

        if (result.SkippedDuplicate > 0)
        {
            lines.Add($"已跳过重复文件：{result.SkippedDuplicate} 个");
        }

        if (result.Errors.Count > 0)
        {
            lines.Add($"无法读取：{result.Errors.Count} 个");
            lines.AddRange(result.Errors.Take(8).Select(error => $"  • {error}"));
            if (result.Errors.Count > 8)
            {
                lines.Add($"  • 其余 {result.Errors.Count - 8} 个未显示");
            }
        }

        lines.Add("");
        lines.Add("说明：发票入口只导入 PDF；右侧补充材料区域可以添加任意格式附件。");
        return string.Join(Environment.NewLine, lines);
    }

    private void ReindexRecords()
    {
        for (var i = 0; i < Records.Count; i++)
        {
            Records[i].DisplayIndex = i + 1;
        }
    }

    public void AttachFiles(IEnumerable<string> paths)
    {
        if (SelectedRecord is null) return;

        _rules.Evaluate(SelectedRecord);
        var files = ExpandAttachmentPaths(paths).Where(path => SelectedRecord.AttachedDocuments.All(x => !string.Equals(x.FilePath, path, StringComparison.OrdinalIgnoreCase))).ToList();
        if (files.Count == 0)
        {
            ProgressMessage = "没有发现新的附件文件，或附件已经添加过";
            return;
        }

        foreach (var path in files)
        {
            SelectedRecord.AttachedDocuments.Add(new AttachmentRecord { FilePath = path, AttachmentType = InferAttachmentType(path, SelectedRecord), RelatedInvoiceId = SelectedRecord.Id });
        }

        _rules.Evaluate(SelectedRecord);
        _log.Write($"添加附件：{string.Join("、", files.Select(Path.GetFileName))} -> {SelectedRecord.OriginalFileName}");
        SelectedRecord.OnChanged(nameof(SelectedRecord.RequirementDisplays));
        SelectedRecord.OnChanged(nameof(SelectedRecord.StatusDisplay));
        ProgressMessage = $"已添加 {files.Count} 个附件到：{SelectedRecord.OriginalFileName}";
        SelectedRecord.RefreshFileState();
        RecordsView.Refresh();
        RefreshStats();
        ScheduleAutosave();
    }

    private static AttachmentType InferAttachmentType(string path, InvoiceRecord record)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (name.Contains("订单") || name.Contains("携程") || name.Contains("行程")) return AttachmentType.OrderPage;
        if (name.Contains("支付") || name.Contains("付款") || name.Contains("微信") || name.Contains("支付宝")) return AttachmentType.PaymentProof;
        if ((name.Contains("3D", StringComparison.OrdinalIgnoreCase) || name.Contains("三维") || name.Contains("打印")) && name.Contains("明细")) return AttachmentType.ThreeDPrintDetails;
        var missing = record.MissingTypes().Where(type => type != AttachmentType.Invoice).ToList();
        if (missing.Count > 0) return missing[0];
        return AttachmentType.Other;
    }

    private static IEnumerable<string> ExpandAttachmentPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path) && IsSupportedAttachment(path))
            {
                yield return path;
            }
            else if (Directory.Exists(path))
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Where(IsSupportedAttachment).ToList();
                }
                catch
                {
                    files = [];
                }

                foreach (var file in files)
                {
                    yield return file;
                }
            }
        }
    }

    private static bool IsSupportedAttachment(string path)
    {
        return File.Exists(path);
    }

    private void RecheckAll()
    {
        foreach (var record in Records)
        {
            record.RefreshFileState();
            _rules.Evaluate(record);
        }

        _duplicateInvoices.Evaluate(Records.ToList());

        RefreshStats();
        RecordsView.Refresh();
        ProgressMessage = "材料规则已重新检查";
        ScheduleAutosave();
    }

    private async void AnalyzeSelectedFast()
    {
        if (SelectedRecord is null) return;
        await AnalyzeRecordsAsync([SelectedRecord], allowPaddle: false);
    }

    private async void AnalyzeSelectedWithPaddle()
    {
        if (SelectedRecord is null) return;
        await AnalyzeRecordsAsync([SelectedRecord], allowPaddle: true);
    }

    private async Task AnalyzeRecordsAsync(IReadOnlyList<InvoiceRecord> records, bool allowPaddle)
    {
        IsBusy = true;
        IsProgressIndeterminate = false;
        OperationProgress = 0;
        var failed = 0;
        try
        {
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                ProgressMessage = $"正在分析 {i + 1}/{records.Count}：{record.OriginalFileName}";
                _isProgrammaticUpdate = true;
                try
                {
                    try
                    {
                        await _analysis.AnalyzeAsync(record, allowPaddle);
                        _learning.TryApply(record);
                        _rules.Evaluate(record);
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        record.Status = RecordStatus.Error;
                        record.RecognitionSummary = $"识别失败：{ToFriendlyError(ex)}";
                        record.ValidationIssues.Clear();
                        record.ValidationIssues.Add(new ValidationIssue("ANALYSIS_FAILED", ValidationSeverity.Error, ToFriendlyError(ex)));
                        record.OnChanged(nameof(record.RecognitionSummary));
                        record.OnChanged(nameof(record.ValidationDisplays));
                        _log.Write($"识别失败：{record.OriginalFileName}；{ex}");
                    }
                }
                finally
                {
                    _isProgrammaticUpdate = false;
                }
                _log.Write($"导入并分析：{record.OriginalFileName}；分类：{record.CategoryDisplay}；金额：{record.TotalAmount?.ToString("0.##") ?? "未识别"}");
                RecordsView.Refresh();
                RefreshStats();
                OperationProgress = (i + 1) * 100d / records.Count;
            }

            ProgressMessage = failed == 0
                ? allowPaddle ? "Paddle 深度识别完成" : $"识别完成，共处理 {records.Count} 张发票"
                : $"识别完成：成功 {records.Count - failed} 张，失败 {failed} 张";
            _duplicateInvoices.Evaluate(Records.ToList());
            RefreshStats();
            ScheduleAutosave();
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
        }
    }

    public async Task ApplyBulkEditAsync(IReadOnlyList<InvoiceRecord> records, BulkEditRequest request)
    {
        if (records.Count == 0 || IsBusy) return;

        _isProgrammaticUpdate = true;
        try
        {
            foreach (var record in records)
            {
                if (request.ChangeCategory)
                {
                    record.Category = request.Category;
                    if (request.Category != InvoiceCategory.Travel) record.SubCategory = TravelSubCategory.None;
                    record.ManualOverride = true;
                    record.ClassificationConfidence = 1.0;
                }
                if (request.ChangeSubCategory && record.Category == InvoiceCategory.Travel)
                {
                    record.SubCategory = request.SubCategory;
                    record.ManualOverride = true;
                    record.ClassificationConfidence = 1.0;
                }
                if (request.ChangeAttachmentType)
                {
                    foreach (var attachment in record.SupplementAttachments) attachment.AttachmentType = request.AttachmentType;
                }
                if (request.MarkIgnored) record.UserIgnored = true;
                if (request.RestoreIgnored) record.UserIgnored = false;
                _rules.Evaluate(record);
                if (request.ChangeCategory || request.ChangeSubCategory) _learning.Remember(record);
            }
        }
        finally
        {
            _isProgrammaticUpdate = false;
        }

        if (request.RemoveFromList)
        {
            _lastDeletedRecords.Clear();
            _lastDeletedRecords.AddRange(records.Select(record => (record, Records.IndexOf(record))).Where(item => item.Item2 >= 0));
            foreach (var record in records)
            {
                record.PropertyChanged -= Record_PropertyChanged;
                Records.Remove(record);
                if (!string.IsNullOrWhiteSpace(record.FileHash)) _hashes.Remove(record.FileHash);
            }
            ReindexRecords();
            SelectedRecord = Records.FirstOrDefault();
            OnChanged(nameof(HasUndoDelete));
            OnChanged(nameof(UndoDeleteText));
            UndoDeleteCommand.RaiseCanExecuteChanged();
        }

        if (request.Reanalyze && !request.RemoveFromList)
        {
            await AnalyzeRecordsAsync(records, allowPaddle: false);
        }

        _duplicateInvoices.Evaluate(Records.ToList());
        RecordsView.Refresh();
        RefreshStats();
        ScheduleAutosave();
        ProgressMessage = $"已批量处理 {records.Count} 张发票";
    }

    public int ApplyInlineCategoryEdit(
        IReadOnlyList<InvoiceRecord> records,
        InvoiceCategory? category,
        TravelSubCategory? subCategory)
    {
        if (records.Count == 0 || IsBusy) return 0;

        var affected = 0;
        _isProgrammaticUpdate = true;
        try
        {
            foreach (var record in records.Distinct())
            {
                var changed = false;
                if (category is { } selectedCategory)
                {
                    record.Category = selectedCategory;
                    if (selectedCategory != InvoiceCategory.Travel)
                        record.SubCategory = TravelSubCategory.None;
                    changed = true;
                }

                if (subCategory is { } selectedSubCategory)
                {
                    if (selectedSubCategory != TravelSubCategory.None && record.Category != InvoiceCategory.Travel)
                        record.Category = InvoiceCategory.Travel;
                    if (record.Category == InvoiceCategory.Travel)
                    {
                        record.SubCategory = selectedSubCategory;
                        changed = true;
                    }
                }

                if (!changed) continue;
                record.ManualOverride = true;
                record.ClassificationConfidence = 1.0;
                _rules.Evaluate(record);
                _learning.Remember(record);
                affected++;
            }
        }
        finally
        {
            _isProgrammaticUpdate = false;
        }

        _duplicateInvoices.Evaluate(Records.ToList());
        RecordsView.Refresh();
        RefreshStats();
        ScheduleAutosave();
        ProgressMessage = affected == 1
            ? "已修改 1 张发票的分类"
            : $"已批量修改 {affected} 张发票的分类";
        return affected;
    }

    public void CompleteQuickReview(IEnumerable<InvoiceRecord> reviewedRecords, int reviewedCount, int totalCount)
    {
        foreach (var record in reviewedRecords)
        {
            if (record.ManualOverride) _learning.Remember(record);
            _rules.Evaluate(record);
        }
        _duplicateInvoices.Evaluate(Records.ToList());
        RecordsView.Refresh();
        RefreshStats();
        ScheduleAutosave();
        ProgressMessage = reviewedCount >= totalCount
            ? $"已完成 {totalCount} 张待确认发票的复核"
            : $"已复核 {reviewedCount} 张，剩余 {totalCount - reviewedCount} 张待确认发票";
    }

    public ReimbursementSettings GetSettings() => _settings.Clone();
    public int LearnedCorrectionCount => _learning.Count;

    public void ApplySettings(ReimbursementSettings settings, bool clearLearning)
    {
        _settings.ConsumablePaymentThreshold = settings.ConsumablePaymentThreshold;
        _settings.RequireFlightOrderPage = settings.RequireFlightOrderPage;
        _settings.RequireFlightPaymentProof = settings.RequireFlightPaymentProof;
        _settings.RequireTrainOrderPage = settings.RequireTrainOrderPage;
        _settings.RequireThreeDPrintDetails = settings.RequireThreeDPrintDetails;
        _settings.AllowIncompleteExport = settings.AllowIncompleteExport;
        _settings.CheckForUpdatesOnStartup = settings.CheckForUpdatesOnStartup;
        _settings.IgnoredUpdateTag = settings.IgnoredUpdateTag;
        _settings.EnablePaddleAutoFallback = settings.EnablePaddleAutoFallback;
        if (clearLearning) _learning.Clear();
        _settingsService.Save(_settings);
        RecheckAll();
        ProgressMessage = "设置已保存并应用";
    }

    public void IgnoreUpdateTag(string tag)
    {
        _settings.IgnoredUpdateTag = tag;
        _settingsService.Save(_settings);
        ProgressMessage = $"本版本不再提醒：{tag}";
    }

    private void DismissSelectedDuplicate()
    {
        if (SelectedRecord is null) return;
        SelectedRecord.DuplicateDismissed = true;
        _duplicateInvoices.Evaluate(Records.ToList());
        RecordsView.Refresh();
        RefreshStats();
        ScheduleAutosave();
        ProgressMessage = $"已将 {SelectedRecord.OriginalFileName} 标记为不是重复发票";
        DismissDuplicateCommand.RaiseCanExecuteChanged();
    }

    private async void Export()
    {
        RecheckAll();
        var reimbursementInfo = CreateReimbursementInfo();
        if (reimbursementInfo is null || !reimbursementInfo.HasRequiredFields)
        {
            System.Windows.MessageBox.Show("提交报销材料前必须填写报销人姓名和学号/工号。\n请先点击顶部“添加说明”按钮填写。", "缺少报销人信息", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var review = new ReimbursementAssistant.Views.ExportReviewWindow(
            ExportableCount,
            CompleteCount,
            PendingCount,
            GrandTotal,
            BuildExportIssues())
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (review.ShowDialog() != true) return;

        if (PendingCount > 0 && !_settings.AllowIncompleteExport)
        {
            System.Windows.MessageBox.Show("当前规则设置为“不允许缺失材料时导出”。\n请先处理所有待确认或缺材料项目。", "暂不能导出", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var dialog = new OpenFolderDialog { Title = "选择报销材料输出位置" };
        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        IsProgressIndeterminate = true;
        ProgressMessage = "正在复制并整理报销材料…";
        try
        {
            var recordsToExport = Records.Where(record => !record.UserIgnored).ToList();
            var namingRule = _namingRule.Clone();
            var output = await Task.Run(() => _export.Export(recordsToExport, dialog.FolderName, reimbursementInfo, namingRule));
            _log.Write($"导出：{output}");
            ProgressMessage = "报销材料已生成";
            var result = System.Windows.MessageBox.Show(
                $"已复制生成报销文件夹：\n{output}\n\n是否立即打开输出文件夹？",
                "导出完成",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(output) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            ProgressMessage = $"导出失败：{ToFriendlyError(ex)}";
            _log.Write($"导出失败：{ex}");
            System.Windows.MessageBox.Show(
                $"未能生成报销材料。\n\n{ToFriendlyError(ex)}\n\n请确认输出目录可写，并关闭可能正在占用的同名文件后重试。",
                "导出失败",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
        }
    }

    public void ApplyNamingRule(InvoiceNamingRule rule, NamingApplyTarget target)
    {
        _namingRule = rule.Clone();
        if (target == NamingApplyTarget.ExportOnly)
        {
            ProgressMessage = "已保存批量命名规则：仅影响导出文件夹";
            ScheduleAutosave();
            return;
        }

        var renamed = _naming.RenameOriginalFiles(Records.ToList(), _namingRule);
        RecordsView.Refresh();
        RefreshStats();
        ProgressMessage = $"已直接重命名 {renamed} 个原始 PDF 文件";
        _log.Write($"批量重命名原始 PDF：{renamed} 个");
        ScheduleAutosave();
    }

    private IEnumerable<string> BuildExportIssues()
    {
        foreach (var record in Records.Where(record => IsPending(record) || record.IsPossibleDuplicate || record.HasMissingFiles).OrderBy(x => x.DisplayIndex))
        {
            var missing = string.Join("、", record.MissingTypes().Where(x => x != AttachmentType.Invoice).Select(InvoiceRecord.DisplayAttachment));
            var issue = string.IsNullOrWhiteSpace(missing)
                ? record.MaterialStatusLabel
                : $"{record.MaterialStatusLabel}：{missing}";
            yield return $"{record.DisplayIndex:D3}｜{record.OriginalFileName}｜{issue}";
        }
    }

    private ReimbursementInfo? CreateReimbursementInfo()
    {
        if (string.IsNullOrWhiteSpace(ReimbursementPersonName) && string.IsNullOrWhiteSpace(ReimbursementPersonIdentifier) && string.IsNullOrWhiteSpace(ReimbursementDescription))
        {
            return null;
        }

        return new ReimbursementInfo(ReimbursementPersonName, ReimbursementPersonIdentifier, ReimbursementDescription);
    }

    private void Record_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isProgrammaticUpdate) return;
        if (sender is not InvoiceRecord record) return;
        if (e.PropertyName is not (nameof(InvoiceRecord.Category) or nameof(InvoiceRecord.SubCategory) or nameof(InvoiceRecord.TotalAmount))) return;

        if (e.PropertyName is nameof(InvoiceRecord.Category) or nameof(InvoiceRecord.SubCategory))
        {
            record.ManualOverride = true;
            record.ClassificationConfidence = 1.0;
            _learning.Remember(record);
        }
        _rules.Evaluate(record);
        _duplicateInvoices.Evaluate(Records.ToList());
        RecordsView.Refresh();
        RefreshStats();
        ScheduleAutosave();
    }

    private void RefreshStats()
    {
        OnChanged(nameof(ConsumableCount));
        OnChanged(nameof(TravelCount));
        OnChanged(nameof(PrintFeeCount));
        OnChanged(nameof(ShippingFeeCount));
        OnChanged(nameof(OtherCount));
        OnChanged(nameof(DuplicateCount));
        OnChanged(nameof(MissingCount));
        OnChanged(nameof(PendingCount));
        OnChanged(nameof(CompleteCount));
        OnChanged(nameof(ExportableCount));
        OnChanged(nameof(FooterSummary));
        OnChanged(nameof(ConsumableTotal));
        OnChanged(nameof(TravelTotal));
        OnChanged(nameof(PrintFeeTotal));
        OnChanged(nameof(ShippingFeeTotal));
        OnChanged(nameof(OtherTotal));
        OnChanged(nameof(UnknownTotal));
        OnChanged(nameof(GrandTotal));
        OnChanged(nameof(AmountSummary));
        OnChanged(nameof(HasRecords));
        OnChanged(nameof(HasVisibleRecords));
        OnChanged(nameof(EmptyStateMessage));
        ExportCommand.RaiseCanExecuteChanged();
        UndoDeleteCommand.RaiseCanExecuteChanged();
        SaveProjectCommand.RaiseCanExecuteChanged();
        SaveProjectAsCommand.RaiseCanExecuteChanged();
    }

    private bool FilterRecord(object value)
    {
        if (value is not InvoiceRecord record) return false;

        var matchesFilter = ActiveFilter switch
        {
            InvoiceListFilter.Consumable => record.Category == InvoiceCategory.Consumable,
            InvoiceListFilter.Travel => record.Category == InvoiceCategory.Travel,
            InvoiceListFilter.PrintFee => record.Category == InvoiceCategory.PrintFee,
            InvoiceListFilter.ShippingFee => record.Category == InvoiceCategory.ShippingFee,
            InvoiceListFilter.Other => record.Category == InvoiceCategory.Other,
            InvoiceListFilter.Pending => IsPending(record),
            InvoiceListFilter.Duplicate => record.IsPossibleDuplicate,
            _ => true
        };

        if (!matchesFilter || string.IsNullOrWhiteSpace(SearchText)) return matchesFilter;

        var keyword = SearchText.Trim();
        return record.OriginalFileName.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)
               || record.ProjectMerchantDisplay.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)
               || record.CategoryDisplay.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)
               || (record.InvoiceNumber?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
               || (record.TotalAmount?.ToString("0.##").Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void EnsureVisibleSelection()
    {
        if (SelectedRecord is not null && FilterRecord(SelectedRecord)) return;
        SelectedRecord = RecordsView.Cast<InvoiceRecord>().FirstOrDefault();
    }

    private static string ToFriendlyError(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "没有访问该文件或文件夹的权限",
        FileNotFoundException => "原始文件已被移动或删除",
        DirectoryNotFoundException => "目标文件夹不存在",
        IOException => "文件正在被其他程序占用，或磁盘暂时无法访问",
        _ => exception.Message
    };

    private static bool IsPending(InvoiceRecord record) => !record.UserIgnored && record.Status is RecordStatus.MissingDocuments or RecordStatus.NeedConfirmation or RecordStatus.Error;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
