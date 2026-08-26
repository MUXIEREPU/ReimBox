using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using ReimbursementAssistant.Models;
using ReimbursementAssistant.Services;
using ReimbursementAssistant.ViewModels;
using System.Windows.Controls.Primitives;

namespace ReimbursementAssistant.Views;
public partial class MainWindow : Window
{
    private Point? _pdfPreviewDragStartPoint;
    private double _pdfPreviewDragStartHorizontalOffset;
    private double _pdfPreviewDragStartVerticalOffset;
    private bool _isGridSelectionChanging;
    private bool _isBulkCategoryApplying;
    private List<InvoiceRecord> _selectedInvoiceRecords = [];
    private PdfPreviewWindow? _pdfPreviewWindow;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        ((MainViewModel)DataContext).IsImportDragOver = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        ((MainViewModel)DataContext).IsImportDragOver = false;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        ((MainViewModel)DataContext).IsImportDragOver = false;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            ((MainViewModel)DataContext).ImportPaths(files);
        }

        e.Handled = true;
    }

    private void EditReimbursementInfo_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var dialog = new ReimbursementInfoWindow(
            viewModel.ReimbursementPersonName,
            viewModel.ReimbursementPersonIdentifier,
            viewModel.ReimbursementDescription)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            viewModel.ReimbursementPersonName = dialog.PersonName;
            viewModel.ReimbursementPersonIdentifier = dialog.PersonIdentifier;
            viewModel.ReimbursementDescription = dialog.Description;
            viewModel.ScheduleAutosave();
        }
    }

    private void EditNamingRule_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (viewModel.Records.Count == 0)
        {
            MessageBox.Show("请先导入 PDF 发票，再设置批量命名规则。", "暂无发票", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new NamingRuleWindow(viewModel.Records.ToList(), viewModel.NamingRule)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (dialog.ApplyTarget == NamingApplyTarget.RenameOriginalFiles)
        {
            var result = MessageBox.Show(
                "你选择的是“直接重命名原始 PDF 文件”。\n\n这会修改导入的原发票文件夹中的 PDF 文件名，而不是只影响导出的报销文件夹。\n原文件内容不会被修改，但文件名会变化。\n\n确定继续吗？",
                "确认重命名原始文件",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        viewModel.ApplyNamingRule(dialog.Rule, dialog.ApplyTarget);
    }

    private void Attachment_DragOver(object sender, DragEventArgs e)
    {
        ((MainViewModel)DataContext).IsAttachmentDragOver = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Attachment_DragLeave(object sender, DragEventArgs e)
    {
        ((MainViewModel)DataContext).IsAttachmentDragOver = false;
        e.Handled = true;
    }

    private void Attachment_Drop(object sender, DragEventArgs e)
    {
        ((MainViewModel)DataContext).IsAttachmentDragOver = false;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            ((MainViewModel)DataContext).AttachFiles(files);
        }

        e.Handled = true;
    }

    private void Attachment_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.ChooseAttachmentFilesCommand.CanExecute(null))
        {
            viewModel.ChooseAttachmentFilesCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void AttachmentOpen_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AttachmentRecord attachment || !File.Exists(attachment.FilePath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(attachment.FilePath) { UseShellExecute = true });
    }

    private void AttachmentPreview_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AttachmentRecord attachment) return;
        new AttachmentPreviewWindow(attachment.FilePath) { Owner = this }.ShowDialog();
    }

    private void AttachmentMoveUp_Click(object sender, RoutedEventArgs e) => MoveAttachment(sender, -1);
    private void AttachmentMoveDown_Click(object sender, RoutedEventArgs e) => MoveAttachment(sender, 1);

    private void MoveAttachment(object sender, int direction)
    {
        if ((sender as FrameworkElement)?.DataContext is not AttachmentRecord attachment || DataContext is not MainViewModel viewModel || viewModel.SelectedRecord is null) return;
        var collection = viewModel.SelectedRecord.AttachedDocuments;
        var invoiceOffset = collection.Count > 0 && collection[0].AttachmentType == AttachmentType.Invoice ? 1 : 0;
        var oldIndex = collection.IndexOf(attachment);
        var newIndex = Math.Clamp(oldIndex + direction, invoiceOffset, collection.Count - 1);
        if (oldIndex == newIndex) return;
        collection.Move(oldIndex, newIndex);
        viewModel.ScheduleAutosave();
    }

    private void AttachmentDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AttachmentRecord attachment || !attachment.IsSupplement || DataContext is not MainViewModel viewModel || viewModel.SelectedRecord is null)
        {
            return;
        }

        viewModel.SelectedRecord.AttachedDocuments.Remove(attachment);
        viewModel.RecheckCommand.Execute(null);
    }

    private void AttachmentType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.RecheckCommand.CanExecute(null))
        {
            viewModel.RecheckCommand.Execute(null);
        }
    }

    private void PdfPreview_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var factor = e.Delta > 0 ? 1.12 : 1 / 1.12;
        viewModel.PdfPreviewZoom *= factor;
        e.Handled = true;
    }

    private void PdfPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        _pdfPreviewDragStartPoint = e.GetPosition(scrollViewer);
        _pdfPreviewDragStartHorizontalOffset = scrollViewer.HorizontalOffset;
        _pdfPreviewDragStartVerticalOffset = scrollViewer.VerticalOffset;
        scrollViewer.CaptureMouse();
        scrollViewer.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void PdfPreview_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pdfPreviewDragStartPoint is not Point startPoint || sender is not ScrollViewer scrollViewer || !scrollViewer.IsMouseCaptured)
        {
            return;
        }

        var currentPoint = e.GetPosition(scrollViewer);
        var deltaX = currentPoint.X - startPoint.X;
        var deltaY = currentPoint.Y - startPoint.Y;

        scrollViewer.ScrollToHorizontalOffset(_pdfPreviewDragStartHorizontalOffset - deltaX);
        scrollViewer.ScrollToVerticalOffset(_pdfPreviewDragStartVerticalOffset - deltaY);
        e.Handled = true;
    }

    private void PdfPreview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        StopPdfPreviewDrag(sender);
        e.Handled = true;
    }

    private void PdfPreview_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _pdfPreviewDragStartPoint = null;
        if (sender is ScrollViewer scrollViewer)
            scrollViewer.Cursor = Cursors.Hand;
    }

    private void StopPdfPreviewDrag(object sender)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        _pdfPreviewDragStartPoint = null;
        scrollViewer.ReleaseMouseCapture();
        scrollViewer.Cursor = Cursors.Hand;
    }

    private void PdfPreviewDetach_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        if (viewModel.SelectedRecord is null)
        {
            MessageBox.Show("请先选择一张 PDF 发票。", "暂无预览", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_pdfPreviewWindow is { IsVisible: true })
        {
            if (_pdfPreviewWindow.WindowState == WindowState.Minimized)
                _pdfPreviewWindow.WindowState = WindowState.Normal;
            _pdfPreviewWindow.Activate();
            return;
        }

        _pdfPreviewWindow = new PdfPreviewWindow(viewModel) { Owner = this };
        _pdfPreviewWindow.Closed += (_, _) => _pdfPreviewWindow = null;
        _pdfPreviewWindow.Show();
    }

    private void InvoiceGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || DataContext is not MainViewModel viewModel || !viewModel.DeleteSelectedCommand.CanExecute(null))
        {
            return;
        }

        viewModel.DeleteSelectedCommand.Execute(null);
        e.Handled = true;
    }

    private void InvoiceGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _isGridSelectionChanging = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _isGridSelectionChanging = false;
            UpdateBulkCategoryHint();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void UpdateBulkCategoryHint()
    {
        var count = InvoiceGrid.SelectedItems.Count;
        if (count > 1 && DataContext is MainViewModel)
        {
            BulkCategoryHint.Text = $"已选 {count} 张，修改将批量应用";
            BulkCategoryHint.Visibility = Visibility.Visible;
        }
        else
        {
            BulkCategoryHint.Text = "";
            BulkCategoryHint.Visibility = Visibility.Collapsed;
        }
    }

    private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isGridSelectionChanging || _isBulkCategoryApplying || DataContext is not MainViewModel viewModel ||
            CategoryCombo.SelectedValue is not InvoiceCategory category)
            return;

        var selected = CurrentInvoiceSelection();
        if (selected.Count == 0) return;

        _isBulkCategoryApplying = true;
        try
        {
            viewModel.ApplyInlineCategoryEdit(selected, category, null);
            RestoreGridSelection(selected);
        }
        finally
        {
            _isBulkCategoryApplying = false;
        }
    }

    private void SubCategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isGridSelectionChanging || _isBulkCategoryApplying || DataContext is not MainViewModel viewModel ||
            SubCategoryCombo.SelectedValue is not TravelSubCategory subCategory)
            return;

        var selected = CurrentInvoiceSelection();
        if (selected.Count == 0) return;

        _isBulkCategoryApplying = true;
        try
        {
            viewModel.ApplyInlineCategoryEdit(selected, null, subCategory);
            RestoreGridSelection(selected);
        }
        finally
        {
            _isBulkCategoryApplying = false;
        }
    }

    private List<InvoiceRecord> CurrentInvoiceSelection()
    {
        var current = InvoiceGrid.SelectedItems.Cast<InvoiceRecord>().ToList();
        if (current.Count > 0)
        {
            _selectedInvoiceRecords = current;
        }
        return _selectedInvoiceRecords.Where(record => InvoiceGrid.Items.Contains(record)).ToList();
    }

    private void RestoreGridSelection(List<InvoiceRecord> records)
    {
        var valid = records.Where(record => InvoiceGrid.Items.Contains(record)).ToList();
        _selectedInvoiceRecords = valid;
        if (valid.Count == 0)
        {
            UpdateBulkCategoryHint();
            return;
        }

        _isGridSelectionChanging = true;
        InvoiceGrid.SelectedItems.Clear();
        foreach (var record in valid)
        {
            InvoiceGrid.SelectedItems.Add(record);
        }
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _isGridSelectionChanging = false;
            UpdateBulkCategoryHint();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            InvoiceSearchBox.Focus();
            InvoiceSearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O && viewModel.ImportCommand.CanExecute(null))
        {
            viewModel.ImportCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S && viewModel.SaveProjectCommand.CanExecute(null))
        {
            viewModel.SaveProjectCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && viewModel.ClearSearchCommand.CanExecute(null))
        {
            viewModel.ClearSearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.TryRestoreAutosave();
            await CheckForUpdatesAsync(viewModel, showWhenCurrent: false);
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _pdfPreviewWindow?.Close();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.FlushAutosave();
        }
    }

    private async void BulkEdit_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        var selected = CurrentInvoiceSelection();
        if (selected.Count == 0 && viewModel.SelectedRecord is not null) selected.Add(viewModel.SelectedRecord);
        if (selected.Count == 0)
        {
            MessageBox.Show("请先选择需要批量编辑的发票。\n按住 Ctrl 或 Shift 可以选择多行。", "尚未选择发票", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new BulkEditWindow(selected.Count) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        if (dialog.Request.RemoveFromList)
        {
            var result = MessageBox.Show($"确定从列表移除选中的 {selected.Count} 张发票吗？\n\n原始 PDF 不会被删除。", "确认批量移除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }
        await viewModel.ApplyBulkEditAsync(selected, dialog.Request);
    }

    private void QuickReview_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        var pending = viewModel.Records.Where(record => record.Category == InvoiceCategory.Unknown || record.Status is RecordStatus.NeedConfirmation or RecordStatus.Error).OrderBy(record => record.DisplayIndex).ToList();
        if (pending.Count == 0)
        {
            MessageBox.Show("当前没有需要人工确认分类的发票。", "无需复核", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = new QuickReviewWindow(pending) { Owner = this };
        dialog.ShowDialog();
        viewModel.CompleteQuickReview(pending.Take(dialog.ReviewedCount), dialog.ReviewedCount, pending.Count);
        if (dialog.LastReviewedRecord is not null) viewModel.SelectedRecord = dialog.LastReviewedRecord;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        var dialog = new SettingsWindow(viewModel.GetSettings(), viewModel.LearnedCorrectionCount) { Owner = this };
        if (dialog.ShowDialog() == true) viewModel.ApplySettings(dialog.ResultSettings, dialog.ClearLearningRequested);
    }

    private void ProjectMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not Button button) return;
        var menu = BuildProjectMenu(viewModel);
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.HorizontalOffset = -8;
        menu.VerticalOffset = 4;
        menu.IsOpen = true;
    }

    private ContextMenu BuildProjectMenu(MainViewModel viewModel)
    {
        var menu = new ContextMenu
        {
            Style = (Style)FindResource("ModernContextMenuStyle")
        };
        menu.Items.Add(CreateMenuItem("新建项目", "\uE710", viewModel.NewProjectCommand));
        menu.Items.Add(CreateMenuItem("打开项目…", "\uE8E5", viewModel.OpenProjectCommand));
        menu.Items.Add(CreateMenuSeparator());
        menu.Items.Add(CreateMenuItem("保存项目", "\uE74E", viewModel.SaveProjectCommand));
        menu.Items.Add(CreateMenuItem("项目另存为…", "\uE792", viewModel.SaveProjectAsCommand));
        var recent = viewModel.RecentProjects;
        if (recent.Count > 0)
        {
            menu.Items.Add(CreateMenuSeparator());
            var recentRoot = CreateMenuItem("最近项目", "\uE81C");
            foreach (var path in recent)
            {
                var item = CreateMenuItem(Path.GetFileNameWithoutExtension(path), "\uE8A5");
                item.ToolTip = path;
                item.Click += (_, _) => viewModel.OpenProjectPath(path);
                recentRoot.Items.Add(item);
            }
            menu.Items.Add(recentRoot);
        }
        menu.Items.Add(CreateMenuSeparator());
        var updateItem = CreateMenuItem("检查更新…", "\uE895");
        updateItem.Click += async (_, _) => await CheckForUpdatesAsync(viewModel, showWhenCurrent: true);
        menu.Items.Add(updateItem);
        return menu;
    }

    private MenuItem CreateMenuItem(string header, string glyph, System.Windows.Input.ICommand? command = null) => new()
    {
        Header = CreateMenuHeader(header, glyph),
        Command = command,
        Style = (Style)FindResource("ModernMenuItemStyle")
    };

    private Separator CreateMenuSeparator() => new()
    {
        Style = (Style)FindResource("ModernMenuSeparatorStyle")
    };

    private static FrameworkElement CreateMenuHeader(string text, string glyph)
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize = 15,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105)),
            VerticalAlignment = VerticalAlignment.Center
        };
        var label = new TextBlock
        {
            Text = text,
            FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 300
        };
        Grid.SetColumn(label, 1);
        panel.Children.Add(icon);
        panel.Children.Add(label);
        return panel;
    }

    private async Task CheckForUpdatesAsync(MainViewModel viewModel, bool showWhenCurrent)
    {
        if (!showWhenCurrent && !viewModel.GetSettings().CheckForUpdatesOnStartup) return;
        try
        {
            var result = await new UpdateCheckService().CheckAsync();
            if (showWhenCurrent && result.LatestVersion is null)
            {
                MessageBox.Show($"已连接到 GitHub Release，但最新版本号无法识别。\n\n请确认 Release 标签使用类似 v0.2.1 的格式。\n当前检测到：{result.Tag}", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (result.HasUpdate && (showWhenCurrent || !string.Equals(viewModel.GetSettings().IgnoredUpdateTag, result.Tag, StringComparison.OrdinalIgnoreCase)))
            {
                var dialog = new UpdateAvailableWindow(result) { Owner = this };
                var answer = dialog.ShowDialog();
                if (dialog.IgnoreThisVersion) viewModel.IgnoreUpdateTag(result.Tag);
                if (answer == true && Uri.TryCreate(result.ReleaseUrl, UriKind.Absolute, out var uri))
                {
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                }
            }
            else if (showWhenCurrent)
            {
                MessageBox.Show($"当前已经是最新版本 v{result.CurrentVersion.ToString(3)}。", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch when (!showWhenCurrent)
        {
            // 启动时静默检查失败不打扰用户。
        }
        catch (Exception ex)
        {
            MessageBox.Show($"暂时无法检查更新。\n\n{ex.Message}", "检查更新失败", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
