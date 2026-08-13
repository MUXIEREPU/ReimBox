using ReimbursementAssistant.Models;
namespace ReimbursementAssistant.Services;
public sealed class FileImportService(HashService hashes)
{
    public FileImportResult Import(IEnumerable<string> paths, ISet<string> knownHashes)
    {
        var records = new List<InvoiceRecord>();
        var errors = new List<string>();
        var skippedUnsupported = 0;
        var skippedDuplicate = 0;

        foreach (var path in Expand(paths, errors))
        {
            if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                skippedUnsupported++;
                continue;
            }

            try
            {
                var hash = hashes.Compute(path);
                if (!knownHashes.Add(hash))
                {
                    skippedDuplicate++;
                    continue;
                }

                records.Add(new InvoiceRecord { OriginalFilePath = path, FileHash = hash, Status = RecordStatus.NeedConfirmation });
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(path)}：{ToFriendlyMessage(ex)}");
            }
        }

        return new FileImportResult(records, skippedUnsupported, skippedDuplicate, errors);
    }

    private static IEnumerable<string> Expand(IEnumerable<string> paths, ICollection<string> errors)
    {
        foreach (var inputPath in paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(inputPath))
            {
                yield return inputPath;
                continue;
            }

            if (!Directory.Exists(inputPath))
            {
                errors.Add($"{inputPath}：文件或文件夹不存在");
                continue;
            }

            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(inputPath);
            while (pendingDirectories.Count > 0)
            {
                var directory = pendingDirectories.Pop();
                string[] files;
                string[] childDirectories;
                try
                {
                    files = Directory.GetFiles(directory);
                    childDirectories = Directory.GetDirectories(directory);
                }
                catch (Exception ex)
                {
                    errors.Add($"{directory}：{ToFriendlyMessage(ex)}");
                    continue;
                }

                foreach (var file in files)
                {
                    yield return file;
                }

                foreach (var childDirectory in childDirectories)
                {
                    pendingDirectories.Push(childDirectory);
                }
            }
        }
    }

    private static string ToFriendlyMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "没有读取权限",
        IOException => "文件正在被占用或无法读取",
        _ => exception.Message
    };
}

public sealed record FileImportResult(
    IReadOnlyList<InvoiceRecord> Records,
    int SkippedUnsupported,
    int SkippedDuplicate,
    IReadOnlyList<string> Errors);
