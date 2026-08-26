using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ReimbursementAssistant.Services;

public sealed record PdfPreviewFrame(BitmapImage Image, int PageCount, int PageIndex);

public sealed class PdfPreviewService
{
    public async Task<int> GetPageCountAsync(string filePath)
    {
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        var document = await PdfDocument.LoadFromFileAsync(file);
        return (int)document.PageCount;
    }

    public async Task<BitmapImage> RenderPageAsync(string filePath, int pageIndex) =>
        (await RenderAsync(filePath, pageIndex)).Image;

    public async Task<PdfPreviewFrame> RenderAsync(string filePath, int requestedPageIndex)
    {
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        var document = await PdfDocument.LoadFromFileAsync(file);
        if (document.PageCount == 0)
            throw new InvalidOperationException("PDF 没有可预览的页面。");

        var pageCount = (int)document.PageCount;
        var pageIndex = Math.Clamp(requestedPageIndex, 0, pageCount - 1);
        using var page = document.GetPage((uint)pageIndex);
        using var randomAccessStream = new InMemoryRandomAccessStream();

        var renderOptions = new PdfPageRenderOptions { DestinationWidth = 1800 };
        await page.RenderToStreamAsync(randomAccessStream, renderOptions);
        randomAccessStream.Seek(0);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = randomAccessStream.AsStreamForRead();
        image.EndInit();
        image.Freeze();
        return new PdfPreviewFrame(image, pageCount, pageIndex);
    }
}
