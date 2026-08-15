using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using ReimbursementAssistant.Services;

namespace ReimbursementAssistant.Views;

public partial class AttachmentPreviewWindow : Window
{
    private readonly string _path;
    public AttachmentPreviewWindow(string path)
    {
        InitializeComponent();
        _path = path;
        FileNameText.Text = Path.GetFileName(path);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_path)) { EmptyText.Text = "附件已被移动或删除。"; StatusText.Text = _path; return; }
        try
        {
            var extension = Path.GetExtension(_path).ToLowerInvariant();
            if (extension == ".pdf")
            {
                PreviewImage.Source = await new PdfPreviewService().RenderPageAsync(_path, 0);
                StatusText.Text = "PDF 第一页预览";
            }
            else if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp")
            {
                var image = new BitmapImage();
                image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(_path); image.EndInit(); image.Freeze();
                PreviewImage.Source = image;
                StatusText.Text = "图片预览";
            }
            else
            {
                EmptyText.Text = $"此格式暂不支持内嵌预览。\n\n文件类型：{extension.TrimStart('.').ToUpperInvariant()}\n可以使用下方按钮打开。";
                StatusText.Text = _path;
                return;
            }
            EmptyText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) { EmptyText.Text = $"预览失败：{ex.Message}\n\n可以使用默认程序打开。"; }
    }
    private void Open_Click(object sender, RoutedEventArgs e) { if (File.Exists(_path)) Process.Start(new ProcessStartInfo(_path) { UseShellExecute = true }); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
