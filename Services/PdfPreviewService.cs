using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ReimbursementAssistant.Services;

public sealed class PdfPreviewService
{
    public async Task<int> GetPageCountAsync(string filePath)
    {
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        var document = await PdfDocument.LoadFromFileAsync(file);
        return (int)document.PageCount;
    }

    public async Task<BitmapImage> RenderPageAsync(string filePath, int pageIndex)
    {
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        var document = await PdfDocument.LoadFromFileAsync(file);
        if (document.PageCount == 0)
        {
            throw new InvalidOperationException("PDF 没有可预览的页面。");
        }

        var safePageIndex = Math.Clamp(pageIndex, 0, (int)document.PageCount - 1);
        using var page = document.GetPage((uint)safePageIndex);
        using var randomAccessStream = new InMemoryRandomAccessStream();

        var renderOptions = new PdfPageRenderOptions
        {
            DestinationWidth = 1800
        };

        await page.RenderToStreamAsync(randomAccessStream, renderOptions);
        randomAccessStream.Seek(0);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = randomAccessStream.AsStreamForRead();
        image.EndInit();
        image.Freeze();
        return image;
    }
}
