using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReimbursementAssistant.Models;
using ReimbursementAssistant.ViewModels;

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
}
