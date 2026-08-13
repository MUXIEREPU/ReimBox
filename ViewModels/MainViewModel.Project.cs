using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using ReimbursementAssistant.Models;
using ReimbursementAssistant.Services;

namespace ReimbursementAssistant.ViewModels;

public sealed partial class MainViewModel
{
    private readonly ProjectService _projects = new();
    private readonly DispatcherTimer _autosaveTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };
    private string? _currentProjectPath;
    private bool _isLoadingProject;
    private DateTime? _lastSavedAt;

    public string CurrentProjectName => string.IsNullOrWhiteSpace(_currentProjectPath)
        ? "未命名项目"
        : Path.GetFileNameWithoutExtension(_currentProjectPath);
    public string ProjectSaveStatus => _lastSavedAt is null ? "尚未保存" : $"已自动保存 {_lastSavedAt:HH:mm:ss}";
    public IReadOnlyList<string> RecentProjects => _projects.LoadRecentProjects();

    public RelayCommand NewProjectCommand { get; private set; } = null!;
    public RelayCommand OpenProjectCommand { get; private set; } = null!;
    public RelayCommand SaveProjectCommand { get; private set; } = null!;
    public RelayCommand SaveProjectAsCommand { get; private set; } = null!;

    private void InitializeProjectFeatures()
    {
        NewProjectCommand = new RelayCommand(NewProject, () => !IsBusy);
        OpenProjectCommand = new RelayCommand(OpenProject, () => !IsBusy);
        SaveProjectCommand = new RelayCommand(() => SaveProject(false), () => Records.Count > 0 && !IsBusy);
        SaveProjectAsCommand = new RelayCommand(() => SaveProject(true), () => Records.Count > 0 && !IsBusy);
        _autosaveTimer.Tick += (_, _) =>
        {
            _autosaveTimer.Stop();
            SaveAutosaveNow();
        };
    }

    public void TryRestoreAutosave()
    {
        if (!File.Exists(_projects.AutosavePath)) return;
        try
        {
            var autosave = _projects.Load(_projects.AutosavePath);
            if (autosave.Records.Count == 0) return;
            var result = MessageBox.Show(
                $"检测到 {autosave.SavedAt:yyyy-MM-dd HH:mm} 自动保存的项目，共 {autosave.Records.Count} 张发票。\n\n是否恢复上次工作？",
                "恢复上次项目",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                ApplyProject(autosave, null);
                ProgressMessage = $"已恢复上次项目，共 {Records.Count} 张发票";
            }
        }
        catch (Exception ex)
        {
            _log.Write($"自动恢复失败：{ex}");
        }
    }

    public void FlushAutosave()
    {
        _autosaveTimer.Stop();
        SaveAutosaveNow();
    }

    public void ScheduleAutosave()
    {
        if (_isLoadingProject) return;
        if (Records.Count == 0)
        {
            _autosaveTimer.Stop();
            _projects.DeleteAutosave();
            _lastSavedAt = null;
            OnChanged(nameof(ProjectSaveStatus));
            return;
        }
        _autosaveTimer.Stop();
        _autosaveTimer.Start();
    }

    private void SaveAutosaveNow()
    {
        if (_isLoadingProject) return;
        if (Records.Count == 0)
        {
            _projects.DeleteAutosave();
            return;
        }
        try
        {
            _projects.Save(_projects.AutosavePath, Records, BuildReimbursementInfo(), _namingRule);
            if (!string.IsNullOrWhiteSpace(_currentProjectPath))
            {
                _projects.Save(_currentProjectPath, Records, BuildReimbursementInfo(), _namingRule);
            }
            _lastSavedAt = DateTime.Now;
            OnChanged(nameof(ProjectSaveStatus));
        }
        catch (Exception ex)
        {
            ProgressMessage = $"自动保存失败：{ex.Message}";
            _log.Write($"自动保存失败：{ex}");
        }
    }

    private void NewProject()
    {
        if (Records.Count > 0)
        {
            var result = MessageBox.Show(
                "新建项目会清空当前列表。当前项目已经自动保存，原始文件不会被修改。\n\n确定继续吗？",
                "新建项目",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
        }

        ClearProjectRecords();
        ReimbursementPersonName = "";
        ReimbursementPersonIdentifier = "";
        ReimbursementDescription = "";
        _namingRule = InvoiceNamingRule.Default();
        _currentProjectPath = null;
        _lastSavedAt = null;
        _projects.DeleteAutosave();
        OnChanged(nameof(CurrentProjectName));
        OnChanged(nameof(ProjectSaveStatus));
        ProgressMessage = "已新建空白项目";
    }

    private void OpenProject()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "ReimBox 项目|*.reimbox",
            Title = "打开 ReimBox 项目"
        };
        if (dialog.ShowDialog() != true) return;

        OpenProjectPath(dialog.FileName);
    }

    public void OpenProjectPath(string path)
    {
        try
        {
            var project = _projects.Load(path);
            ApplyProject(project, path);
            _projects.AddRecentProject(path);
            OnChanged(nameof(RecentProjects));
            ProgressMessage = $"已打开项目：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"项目文件无法打开。\n\n{ex.Message}", "打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveProject(bool saveAs)
    {
        if (Records.Count == 0) return;
        var path = _currentProjectPath;
        if (saveAs || string.IsNullOrWhiteSpace(path))
        {
            var dialog = new SaveFileDialog
            {
                Filter = "ReimBox 项目|*.reimbox",
                DefaultExt = ".reimbox",
                AddExtension = true,
                FileName = $"报销项目_{DateTime.Today:yyyyMMdd}"
            };
            if (dialog.ShowDialog() != true) return;
            path = dialog.FileName;
        }

        try
        {
            _projects.Save(path!, Records, BuildReimbursementInfo(), _namingRule);
            _currentProjectPath = path;
            _lastSavedAt = DateTime.Now;
            _projects.AddRecentProject(path!);
            OnChanged(nameof(CurrentProjectName));
            OnChanged(nameof(ProjectSaveStatus));
            OnChanged(nameof(RecentProjects));
            ProgressMessage = $"项目已保存：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"项目保存失败。\n\n{ex.Message}", "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyProject(ProjectLoadResult project, string? path)
    {
        _isLoadingProject = true;
        try
        {
            ClearProjectRecords();
            ReimbursementPersonName = project.ReimbursementInfo.PersonName;
            ReimbursementPersonIdentifier = project.ReimbursementInfo.PersonIdentifier;
            ReimbursementDescription = project.ReimbursementInfo.Description ?? "";
            _namingRule = project.NamingRule.Clone();
            _currentProjectPath = path;
            _lastSavedAt = project.SavedAt;

            foreach (var record in project.Records.OrderBy(record => record.DisplayIndex))
            {
                RegisterRecord(record);
                Records.Add(record);
                record.RefreshFileState();
                new InvoiceValidationService().Validate(record);
                _rules.Evaluate(record);
            }

            ReindexRecords();
            _duplicateInvoices.Evaluate(Records.ToList());
            SelectedRecord = Records.FirstOrDefault();
            ActiveFilter = InvoiceListFilter.All;
            RefreshStats();
            OnChanged(nameof(CurrentProjectName));
            OnChanged(nameof(ProjectSaveStatus));
        }
        finally
        {
            _isLoadingProject = false;
        }
    }

    private void ClearProjectRecords()
    {
        foreach (var record in Records)
        {
            record.PropertyChanged -= Record_PropertyChanged;
        }

        Records.Clear();
        _hashes.Clear();
        _lastDeletedRecords.Clear();
        SelectedRecord = null;
        SearchText = "";
        ActiveFilter = InvoiceListFilter.All;
        OnChanged(nameof(HasUndoDelete));
        OnChanged(nameof(UndoDeleteText));
        UndoDeleteCommand?.RaiseCanExecuteChanged();
        RefreshStats();
    }

    private void RegisterRecord(InvoiceRecord record)
    {
        record.PropertyChanged -= Record_PropertyChanged;
        record.PropertyChanged += Record_PropertyChanged;
        if (!string.IsNullOrWhiteSpace(record.FileHash)) _hashes.Add(record.FileHash);
    }

    private ReimbursementInfo BuildReimbursementInfo() => new(
        ReimbursementPersonName,
        ReimbursementPersonIdentifier,
        ReimbursementDescription);
}
