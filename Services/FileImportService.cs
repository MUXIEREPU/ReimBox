using ReimbursementAssistant.Models;
namespace ReimbursementAssistant.Services;
public sealed class FileImportService(HashService hashes)
{
    public FileImportResult Import(IEnumerable<string> paths, ISet<string> knownHashes)
    {
        var records = new List<InvoiceRecord>();
        var skippedUnsupported = 0;
        var skippedDuplicate = 0;

        foreach (var path in Expand(paths))
        {
            if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                skippedUnsupported++;
                continue;
            }

            var hash = hashes.Compute(path);
            if (!knownHashes.Add(hash))
            {
                skippedDuplicate++;
                continue;
            }

            records.Add(new InvoiceRecord { OriginalFilePath = path, FileHash = hash, Status = RecordStatus.NeedConfirmation });
        }

        return new FileImportResult(records, skippedUnsupported, skippedDuplicate);
    }
    private static IEnumerable<string> Expand(IEnumerable<string> paths) => paths.SelectMany(p => Directory.Exists(p) ? Directory.EnumerateFiles(p, "*.*", SearchOption.AllDirectories) : [p]).Where(File.Exists);
}

public sealed record FileImportResult(IReadOnlyList<InvoiceRecord> Records, int SkippedUnsupported, int SkippedDuplicate);
