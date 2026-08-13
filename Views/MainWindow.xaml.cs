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

    private void PdfPreview_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is ScrollViewer { IsMouseCaptured: true })
        {
            StopPdfPreviewDrag(sender);
        }
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

    private void InvoiceGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || DataContext is not MainViewModel viewModel || !viewModel.DeleteSelectedCommand.CanExecute(null))
        {
            return;
        }

        viewModel.DeleteSelectedCommand.Execute(null);
        e.Handled = true;
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
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.FlushAutosave();
        }
    }

    private async void BulkEdit_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        var selected = InvoiceGrid.SelectedItems.Cast<InvoiceRecord>().ToList();
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
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("新建项目", viewModel.NewProjectCommand));
        menu.Items.Add(CreateMenuItem("打开项目…", viewModel.OpenProjectCommand));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("保存项目", viewModel.SaveProjectCommand));
        menu.Items.Add(CreateMenuItem("项目另存为…", viewModel.SaveProjectAsCommand));
        var recent = viewModel.RecentProjects;
        if (recent.Count > 0)
        {
            menu.Items.Add(new Separator());
            var recentRoot = new MenuItem { Header = "最近项目" };
            foreach (var path in recent)
            {
                var item = new MenuItem { Header = Path.GetFileNameWithoutExtension(path), ToolTip = path };
                item.Click += (_, _) => viewModel.OpenProjectPath(path);
                recentRoot.Items.Add(item);
            }
            menu.Items.Add(recentRoot);
        }
        menu.Items.Add(new Separator());
        var updateItem = new MenuItem { Header = "检查更新…" };
        updateItem.Click += async (_, _) => await CheckForUpdatesAsync(viewModel, showWhenCurrent: true);
        menu.Items.Add(updateItem);
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private static MenuItem CreateMenuItem(string header, System.Windows.Input.ICommand command) => new() { Header = header, Command = command };

    private async Task CheckForUpdatesAsync(MainViewModel viewModel, bool showWhenCurrent)
    {
        if (!showWhenCurrent && !viewModel.GetSettings().CheckForUpdatesOnStartup) return;
        try
        {
            var result = await new UpdateCheckService().CheckAsync();
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
