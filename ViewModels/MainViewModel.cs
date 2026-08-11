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

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ReimbursementSettings _settings = new();
    private readonly ReimbursementRuleEngine _rules;
    private readonly FileImportService _import = new(new HashService());
    private readonly ExportService _export;
    private readonly InvoiceAnalysisService _analysis;
    private readonly PdfPreviewService _pdfPreview = new();
    private readonly InvoiceNamingService _naming = new();
    private readonly OperationLogService _log = new();
    private readonly HashSet<string> _hashes = [];
    private InvoiceNamingRule _namingRule = InvoiceNamingRule.Default();

    private bool _isBusy;
    private bool _isProgrammaticUpdate;
    private bool _isImportDragOver;
    private bool _isAttachmentDragOver;
    private bool _isPreviewMode;
    private InvoiceListFilter _activeFilter = InvoiceListFilter.All;
    private string _progressMessage = "就绪";
    private string _pdfPreviewStatus = "请选择一张 PDF 发票进行预览";
    private string _reimbursementPersonName = "";
    private string _reimbursementPersonIdentifier = "";
    private string _reimbursementDescription = "";
    private double _pdfPreviewZoom = 1.0;
    private int _pdfPreviewPageIndex;
    private int _pdfPreviewPageCount;
    private ImageSource? _pdfPreviewImage;
    private InvoiceRecord? _selectedRecord;

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
            if (IsPreviewMode)
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
        }
    }

    public string ProgressMessage { get => _progressMessage; private set { _progressMessage = value; OnChanged(); } }
    public string ReimbursementPersonName { get => _reimbursementPersonName; set { _reimbursementPersonName = value; OnChanged(); } }
    public string ReimbursementPersonIdentifier { get => _reimbursementPersonIdentifier; set { _reimbursementPersonIdentifier = value; OnChanged(); } }
    public string ReimbursementDescription { get => _reimbursementDescription; set { _reimbursementDescription = value; OnChanged(); } }
    public bool IsImportDragOver { get => _isImportDragOver; set { _isImportDragOver = value; OnChanged(); } }
    public bool IsAttachmentDragOver { get => _isAttachmentDragOver; set { _isAttachmentDragOver = value; OnChanged(); } }
    public bool IsPreviewMode { get => _isPreviewMode; private set { _isPreviewMode = value; OnChanged(); OnChanged(nameof(IsDetailMode)); } }
    public bool IsDetailMode => !IsPreviewMode;
    public double PdfPreviewZoom { get => _pdfPreviewZoom; set { _pdfPreviewZoom = Math.Clamp(value, 0.25, 5.0); OnChanged(); OnChanged(nameof(PdfPreviewZoomPercent)); } }
    public string PdfPreviewZoomPercent => $"{PdfPreviewZoom:P0}";
    public int PdfPreviewPageIndex { get => _pdfPreviewPageIndex; private set { _pdfPreviewPageIndex = value; OnChanged(); OnChanged(nameof(PdfPreviewPageDisplay)); PreviousPdfPageCommand.RaiseCanExecuteChanged(); NextPdfPageCommand.RaiseCanExecuteChanged(); } }
    public int PdfPreviewPageCount { get => _pdfPreviewPageCount; private set { _pdfPreviewPageCount = value; OnChanged(); OnChanged(nameof(PdfPreviewPageDisplay)); PreviousPdfPageCommand.RaiseCanExecuteChanged(); NextPdfPageCommand.RaiseCanExecuteChanged(); } }
    public string PdfPreviewPageDisplay => PdfPreviewPageCount <= 0 ? "第 - / - 页" : $"第 {PdfPreviewPageIndex + 1} / {PdfPreviewPageCount} 页";
    public ImageSource? PdfPreviewImage { get => _pdfPreviewImage; private set { _pdfPreviewImage = value; OnChanged(); } }
    public string PdfPreviewStatus { get => _pdfPreviewStatus; private set { _pdfPreviewStatus = value; OnChanged(); } }
    public InvoiceListFilter ActiveFilter { get => _activeFilter; private set { _activeFilter = value; OnChanged(); RecordsView.Refresh(); } }
    public int ConsumableCount => Records.Count(x => x.Category == InvoiceCategory.Consumable);
    public int TravelCount => Records.Count(x => x.Category == InvoiceCategory.Travel);
    public int PrintFeeCount => Records.Count(x => x.Category == InvoiceCategory.PrintFee);
    public int OtherCount => Records.Count(x => x.Category == InvoiceCategory.Other);
    public int MissingCount => Records.Count(x => x.Status == RecordStatus.MissingDocuments);
    public int PendingCount => Records.Count(IsPending);
    public int CompleteCount => Records.Count(x => x.Status == RecordStatus.Complete);
    public string FooterSummary => $"共 {Records.Count} 笔 | 完整 {CompleteCount} | 待处理 {PendingCount}";
    public decimal ConsumableTotal => Records.Where(x => x.Category == InvoiceCategory.Consumable).Sum(x => x.TotalAmount ?? 0);
    public decimal TravelTotal => Records.Where(x => x.Category == InvoiceCategory.Travel).Sum(x => x.TotalAmount ?? 0);
    public decimal PrintFeeTotal => Records.Where(x => x.Category == InvoiceCategory.PrintFee).Sum(x => x.TotalAmount ?? 0);
    public decimal OtherTotal => Records.Where(x => x.Category == InvoiceCategory.Other).Sum(x => x.TotalAmount ?? 0);
    public decimal UnknownTotal => Records.Where(x => x.Category == InvoiceCategory.Unknown).Sum(x => x.TotalAmount ?? 0);
    public decimal GrandTotal => Records.Sum(x => x.TotalAmount ?? 0);
    public string AmountSummary => $"耗材 ¥{ConsumableTotal:N2}  |  差旅 ¥{TravelTotal:N2}  |  打印费 ¥{PrintFeeTotal:N2}  |  其他 ¥{OtherTotal:N2}  |  待确认 ¥{UnknownTotal:N2}  |  总计 ¥{GrandTotal:N2}";
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
    public RelayCommand ShowOtherCommand { get; }
    public RelayCommand ShowPendingCommand { get; }
    public RelayCommand ShowDetailCommand { get; }
    public RelayCommand ShowPreviewCommand { get; }
    public RelayCommand OpenSelectedFileCommand { get; }
    public RelayCommand RevealSelectedFileCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }
    public RelayCommand ChooseAttachmentFilesCommand { get; }
    public RelayCommand PreviousPdfPageCommand { get; }
    public RelayCommand NextPdfPageCommand { get; }

    public MainViewModel()
    {
        RecordsView = CollectionViewSource.GetDefaultView(Records);
        RecordsView.Filter = FilterRecord;

        _rules = new ReimbursementRuleEngine(_settings);
        _export = new ExportService();
        _analysis = new InvoiceAnalysisService(new PdfTextService(), new PaddleOcrVlService(_settings), new WindowsOcrService(), new InvoiceClassificationService(), new InvoiceValidationService());

        ImportCommand = new RelayCommand(ChooseFiles, () => !IsBusy);
        ImportFolderCommand = new RelayCommand(ChooseFolder, () => !IsBusy);
        ExportCommand = new RelayCommand(Export, () => Records.Count > 0 && !IsBusy);
        RecheckCommand = new RelayCommand(RecheckAll);
        AnalyzeCommand = new RelayCommand(AnalyzeSelectedFast, () => SelectedRecord is not null && !IsBusy);
        PaddleAnalyzeCommand = new RelayCommand(AnalyzeSelectedWithPaddle, () => SelectedRecord is not null && !IsBusy);
        ShowAllCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.All);
        ShowConsumableCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.Consumable);
        ShowTravelCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.Travel);
        ShowPrintFeeCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.PrintFee);
        ShowOtherCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.Other);
        ShowPendingCommand = new RelayCommand(() => ActiveFilter = InvoiceListFilter.Pending);
        ShowDetailCommand = new RelayCommand(() => IsPreviewMode = false);
        ShowPreviewCommand = new RelayCommand(() =>
        {
            IsPreviewMode = true;
            _ = LoadPdfPreviewAsync(0);
        });
        OpenSelectedFileCommand = new RelayCommand(OpenSelectedFile, () => SelectedRecord is not null && File.Exists(SelectedRecord.OriginalFilePath));
        RevealSelectedFileCommand = new RelayCommand(RevealSelectedFile, () => SelectedRecord is not null && File.Exists(SelectedRecord.OriginalFilePath));
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => SelectedRecord is not null && !IsBusy);
        ChooseAttachmentFilesCommand = new RelayCommand(ChooseAttachmentFiles, () => SelectedRecord is not null);
        PreviousPdfPageCommand = new RelayCommand(() => _ = LoadPdfPreviewAsync(PdfPreviewPageIndex - 1), () => IsPreviewMode && PdfPreviewPageIndex > 0);
        NextPdfPageCommand = new RelayCommand(() => _ = LoadPdfPreviewAsync(PdfPreviewPageIndex + 1), () => IsPreviewMode && PdfPreviewPageCount > 0 && PdfPreviewPageIndex < PdfPreviewPageCount - 1);
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
        record.PropertyChanged -= Record_PropertyChanged;
        Records.Remove(record);
        if (!string.IsNullOrWhiteSpace(record.FileHash))
        {
            _hashes.Remove(record.FileHash);
        }

        ReindexRecords();
        SelectedRecord = Records.Count == 0 ? null : Records[Math.Clamp(index, 0, Records.Count - 1)];
        RecordsView.Refresh();
        RefreshStats();
        ProgressMessage = $"已从列表删除：{record.OriginalFileName}（原始文件未修改）";
    }

    private async Task LoadPdfPreviewAsync(int? requestedPageIndex = null)
    {
        var record = SelectedRecord;
        PdfPreviewImage = null;
        PdfPreviewZoom = 1.0;

        if (record is null)
        {
            PdfPreviewStatus = "请选择一张 PDF 发票进行预览";
            return;
        }

        if (!File.Exists(record.OriginalFilePath))
        {
            PdfPreviewStatus = "原始文件不存在，无法预览";
            return;
        }

        if (!string.Equals(Path.GetExtension(record.OriginalFilePath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            PdfPreviewStatus = "当前文件不是 PDF，暂不支持在这里预览";
            return;
        }

        var previewPath = record.OriginalFilePath;
        PdfPreviewStatus = $"正在生成预览：{record.OriginalFileName}";

        try
        {
            var pageCount = await _pdfPreview.GetPageCountAsync(previewPath);
            var pageIndex = Math.Clamp(requestedPageIndex ?? PdfPreviewPageIndex, 0, Math.Max(0, pageCount - 1));
            var image = await _pdfPreview.RenderPageAsync(previewPath, pageIndex);
            if (!ReferenceEquals(record, SelectedRecord) || !string.Equals(previewPath, SelectedRecord?.OriginalFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PdfPreviewPageCount = pageCount;
            PdfPreviewPageIndex = pageIndex;
            PdfPreviewImage = image;
            PdfPreviewStatus = "左键按住拖动 · 鼠标滚轮缩放";
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(record, SelectedRecord))
            {
                PdfPreviewStatus = $"预览失败：{ex.Message}";
            }
        }
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
        var dialog = new OpenFolderDialog { Title = "选择发票或报销材料文件夹" };
        if (dialog.ShowDialog() == true) ImportPaths([dialog.FolderName]);
    }

    public async void ImportPaths(IEnumerable<string> paths)
    {
        var result = _import.Import(paths, _hashes);
        var imported = result.Records.ToList();
        if (imported.Count == 0)
        {
            ProgressMessage = result.SkippedUnsupported > 0
                ? $"只支持导入 PDF 发票，已跳过 {result.SkippedUnsupported} 个非 PDF 文件"
                : "没有发现新的 PDF 发票，或文件已经导入过";
            if (result.SkippedUnsupported > 0 || result.SkippedDuplicate > 0)
            {
                System.Windows.MessageBox.Show(BuildImportSummary(imported.Count, result), "导入结果", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            return;
        }

        foreach (var record in imported)
        {
            record.DisplayIndex = Records.Count + 1;
            record.AttachedDocuments.Add(new AttachmentRecord { FilePath = record.OriginalFilePath, AttachmentType = AttachmentType.Invoice, RelatedInvoiceId = record.Id });
            record.PropertyChanged += Record_PropertyChanged;
            Records.Add(record);
        }

        SelectedRecord ??= Records.FirstOrDefault();
        RefreshStats();
        if (result.SkippedUnsupported > 0 || result.SkippedDuplicate > 0)
        {
            ProgressMessage = result.SkippedUnsupported > 0
                ? $"已导入 {imported.Count} 个 PDF，跳过 {result.SkippedUnsupported} 个非 PDF 文件"
                : $"已导入 {imported.Count} 个 PDF，跳过 {result.SkippedDuplicate} 个重复文件";
            System.Windows.MessageBox.Show(BuildImportSummary(imported.Count, result), "导入结果", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        await AnalyzeRecordsAsync(imported, allowPaddle: _settings.EnablePaddleAutoFallback);
    }

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
        RecordsView.Refresh();
        RefreshStats();
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
                foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Where(IsSupportedAttachment))
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
            _rules.Evaluate(record);
        }

        RefreshStats();
        RecordsView.Refresh();
        ProgressMessage = "材料规则已重新检查";
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
        try
        {
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                ProgressMessage = $"正在分析 {i + 1}/{records.Count}：{record.OriginalFileName}";
                _isProgrammaticUpdate = true;
                try
                {
                    await _analysis.AnalyzeAsync(record, allowPaddle);
                    _rules.Evaluate(record);
                }
                finally
                {
                    _isProgrammaticUpdate = false;
                }
                _log.Write($"导入并分析：{record.OriginalFileName}；分类：{record.CategoryDisplay}；金额：{record.TotalAmount?.ToString("0.##") ?? "未识别"}");
                RecordsView.Refresh();
                RefreshStats();
            }

            ProgressMessage = allowPaddle ? "Paddle 深度识别完成" : "快速识别完成";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Export()
    {
        RecheckAll();
        var reimbursementInfo = CreateReimbursementInfo();
        if (reimbursementInfo is null || !reimbursementInfo.HasRequiredFields)
        {
            System.Windows.MessageBox.Show("提交报销材料前必须填写报销人姓名和学号/工号。\n请先点击顶部“添加说明”按钮填写。", "缺少报销人信息", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var review = new ReimbursementAssistant.Views.ExportReviewWindow(
            Records.Count,
            CompleteCount,
            PendingCount,
            GrandTotal,
            BuildExportIssues());
        if (review.ShowDialog() != true) return;

        var dialog = new OpenFolderDialog { Title = "选择报销材料输出位置" };
        if (dialog.ShowDialog() != true) return;

        var output = _export.Export(Records, dialog.FolderName, reimbursementInfo, _namingRule);
        _log.Write($"导出：{output}");
        System.Windows.MessageBox.Show($"已复制生成报销文件夹：\n{output}", "导出完成", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    public void ApplyNamingRule(InvoiceNamingRule rule, NamingApplyTarget target)
    {
        _namingRule = rule.Clone();
        if (target == NamingApplyTarget.ExportOnly)
        {
            ProgressMessage = "已保存批量命名规则：仅影响导出文件夹";
            return;
        }

        var renamed = _naming.RenameOriginalFiles(Records.ToList(), _namingRule);
        RecordsView.Refresh();
        RefreshStats();
        ProgressMessage = $"已直接重命名 {renamed} 个原始 PDF 文件";
        _log.Write($"批量重命名原始 PDF：{renamed} 个");
    }

    private IEnumerable<string> BuildExportIssues()
    {
        foreach (var record in Records.Where(IsPending).OrderBy(x => x.DisplayIndex))
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

        record.ManualOverride = true;
        _rules.Evaluate(record);
        RecordsView.Refresh();
        RefreshStats();
    }

    private void RefreshStats()
    {
        OnChanged(nameof(ConsumableCount));
        OnChanged(nameof(TravelCount));
        OnChanged(nameof(PrintFeeCount));
        OnChanged(nameof(OtherCount));
        OnChanged(nameof(MissingCount));
        OnChanged(nameof(PendingCount));
        OnChanged(nameof(CompleteCount));
        OnChanged(nameof(FooterSummary));
        OnChanged(nameof(ConsumableTotal));
        OnChanged(nameof(TravelTotal));
        OnChanged(nameof(PrintFeeTotal));
        OnChanged(nameof(OtherTotal));
        OnChanged(nameof(UnknownTotal));
        OnChanged(nameof(GrandTotal));
        OnChanged(nameof(AmountSummary));
        ExportCommand.RaiseCanExecuteChanged();
    }

    private bool FilterRecord(object value)
    {
        if (value is not InvoiceRecord record) return false;

        return ActiveFilter switch
        {
            InvoiceListFilter.Consumable => record.Category == InvoiceCategory.Consumable,
            InvoiceListFilter.Travel => record.Category == InvoiceCategory.Travel,
            InvoiceListFilter.PrintFee => record.Category == InvoiceCategory.PrintFee,
            InvoiceListFilter.Other => record.Category == InvoiceCategory.Other,
            InvoiceListFilter.Pending => IsPending(record),
            _ => true
        };
    }

    private static bool IsPending(InvoiceRecord record) => record.Status is RecordStatus.MissingDocuments or RecordStatus.NeedConfirmation or RecordStatus.Error;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
