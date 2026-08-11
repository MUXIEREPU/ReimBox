using Windows.Data.Pdf;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ReimbursementAssistant.Services;

/// <summary>Uses Windows' installed OCR language packs; all recognition stays on the device.</summary>
public sealed class WindowsOcrService
{
    public async Task<string> ReadAsync(string filePath)
    {
        try
        {
            var engine = CreateEngine();
            if (engine is null) return string.Empty;
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            if (string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var document = await PdfDocument.LoadFromFileAsync(file);
                var pages = Enumerable.Range(0, Math.Min(2, (int)document.PageCount));
                var text = new List<string>();
                foreach (var index in pages) { using var page = document.GetPage((uint)index); text.Add(await RecognizePageAsync(page, engine)); }
                return string.Join(Environment.NewLine, text);
            }
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            return await RecognizeBitmapAsync(stream, engine);
        }
        catch { return string.Empty; }
    }

    private static OcrEngine? CreateEngine()
    {
        var chinese = new Language("zh-Hans");
        return OcrEngine.IsLanguageSupported(chinese) ? OcrEngine.TryCreateFromLanguage(chinese) : OcrEngine.TryCreateFromUserProfileLanguages();
    }
    private static async Task<string> RecognizePageAsync(PdfPage page, OcrEngine engine)
    {
        using var stream = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(stream); stream.Seek(0);
        return await RecognizeBitmapAsync(stream, engine);
    }
    private static async Task<string> RecognizeBitmapAsync(IRandomAccessStream stream, OcrEngine engine)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync();
        var result = await engine.RecognizeAsync(bitmap);
        return result.Text;
    }
}
