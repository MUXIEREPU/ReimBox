namespace ReimbursementAssistant.Services;
public sealed class OperationLogService
{
    private readonly string _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReimBox", "operations.log");
    public void Write(string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.AppendAllText(_filePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
    }
}
