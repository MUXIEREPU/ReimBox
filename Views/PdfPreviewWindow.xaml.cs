using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReimbursementAssistant.ViewModels;

namespace ReimbursementAssistant.Views;

public partial class PdfPreviewWindow : Window
{
    private Point? _dragStartPoint;
    private double _dragStartHorizontalOffset;
    private double _dragStartVerticalOffset;

    public PdfPreviewWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsPdfPreviewWindowOpen = true;
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsPdfPreviewWindowOpen = false;
        }
    }

    private void PdfPreview_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        var factor = e.Delta > 0 ? 1.12 : 1 / 1.12;
        viewModel.PdfPreviewZoom *= factor;
        e.Handled = true;
    }

    private void PdfPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;
        _dragStartPoint = e.GetPosition(scrollViewer);
        _dragStartHorizontalOffset = scrollViewer.HorizontalOffset;
        _dragStartVerticalOffset = scrollViewer.VerticalOffset;
        scrollViewer.CaptureMouse();
        scrollViewer.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void PdfPreview_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStartPoint is not Point startPoint || sender is not ScrollViewer scrollViewer || !scrollViewer.IsMouseCaptured)
            return;

        var currentPoint = e.GetPosition(scrollViewer);
        var deltaX = currentPoint.X - startPoint.X;
        var deltaY = currentPoint.Y - startPoint.Y;
        scrollViewer.ScrollToHorizontalOffset(_dragStartHorizontalOffset - deltaX);
        scrollViewer.ScrollToVerticalOffset(_dragStartVerticalOffset - deltaY);
        e.Handled = true;
    }

    private void PdfPreview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        StopDrag(sender);
        e.Handled = true;
    }

    private void PdfPreview_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _dragStartPoint = null;
        if (sender is ScrollViewer scrollViewer)
            scrollViewer.Cursor = Cursors.Hand;
    }

    private void StopDrag(object sender)
    {
        if (sender is not ScrollViewer scrollViewer) return;
        _dragStartPoint = null;
        scrollViewer.ReleaseMouseCapture();
        scrollViewer.Cursor = Cursors.Hand;
    }
}
