using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ReimbursementAssistant.Services;

/// <summary>Reads embedded PDF text only. Scanned documents are deliberately left for the later OCR phase.</summary>
public sealed class PdfTextService
{
    public string Read(string filePath)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        try
        {
            using var document = PdfDocument.Open(filePath);
            return string.Join(Environment.NewLine, document.GetPages().Select(ReadPageInVisualOrder));
        }
        catch { return string.Empty; }
    }
    private static string ReadPageInVisualOrder(Page page)
    {
        var lines = new List<List<Word>>();
        foreach (var word in page.GetWords().Where(x => !string.IsNullOrWhiteSpace(x.Text)).OrderByDescending(x => x.BoundingBox.Bottom))
        {
            var existing = lines.FirstOrDefault(line => Math.Abs(line[0].BoundingBox.Bottom - word.BoundingBox.Bottom) <= 3.5);
            if (existing is null) lines.Add([word]); else existing.Add(word);
        }
        return string.Join(Environment.NewLine, lines.OrderByDescending(x => x[0].BoundingBox.Bottom).Select(line => string.Join(" ", line.OrderBy(x => x.BoundingBox.Left).Select(x => x.Text))));
    }
}
