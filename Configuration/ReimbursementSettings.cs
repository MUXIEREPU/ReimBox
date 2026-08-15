namespace ReimbursementAssistant.Configuration;
public sealed class ReimbursementSettings
{
    public decimal ConsumablePaymentThreshold { get; set; } = 1000m;
    public bool RequireFlightOrderPage { get; set; } = true;
    public bool RequireFlightPaymentProof { get; set; } = true;
    public bool RequireTrainOrderPage { get; set; } = true;
    public bool RequireThreeDPrintDetails { get; set; } = true;
    public bool AllowIncompleteExport { get; set; } = true;
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public string IgnoredUpdateTag { get; set; } = "";
    public bool EnablePaddleOcrVl { get; set; } = true;
    public bool EnablePaddleAutoFallback { get; set; }
    public string PaddlePythonExecutable { get; set; } = FindLocalPythonExecutable();
    public string PaddleWorkerScript { get; set; } = GetDefaultPaddleWorkerScriptPath();
    // "auto" lets PaddleOCR choose GPU when available, otherwise CPU.
    public string PaddleDevice { get; set; } = "auto";
    // CPU inference and a first model initialization can take several minutes.
    public int PaddleTimeoutSeconds { get; set; } = 600;

    public ReimbursementSettings Clone() => new()
    {
        ConsumablePaymentThreshold = ConsumablePaymentThreshold,
        RequireFlightOrderPage = RequireFlightOrderPage,
        RequireFlightPaymentProof = RequireFlightPaymentProof,
        RequireTrainOrderPage = RequireTrainOrderPage,
        RequireThreeDPrintDetails = RequireThreeDPrintDetails,
        AllowIncompleteExport = AllowIncompleteExport,
        CheckForUpdatesOnStartup = CheckForUpdatesOnStartup,
        IgnoredUpdateTag = IgnoredUpdateTag,
        EnablePaddleOcrVl = EnablePaddleOcrVl,
        EnablePaddleAutoFallback = EnablePaddleAutoFallback,
        PaddlePythonExecutable = PaddlePythonExecutable,
        PaddleWorkerScript = PaddleWorkerScript,
        PaddleDevice = PaddleDevice,
        PaddleTimeoutSeconds = PaddleTimeoutSeconds
    };

    private static string FindLocalPythonExecutable()
    {
        var runtime = Path.Combine(Directory.GetCurrentDirectory(), ".venv_paddleocr_runtime", "Scripts", "python.exe");
        if (File.Exists(runtime)) return runtime;
        var local = Path.Combine(Directory.GetCurrentDirectory(), ".venv_paddleocr", "Scripts", "python.exe");
        return File.Exists(local) ? local : "python";
    }

    public static string EnsureBundledPaddleWorker()
    {
        var fromOutput = Path.Combine(AppContext.BaseDirectory, "PaddleWorker", "paddle_vl_worker.py");
        if (File.Exists(fromOutput)) return fromOutput;

        var developmentCopy = Path.Combine(Directory.GetCurrentDirectory(), "PaddleWorker", "paddle_vl_worker.py");
        if (File.Exists(developmentCopy)) return developmentCopy;

        var destination = GetBundledPaddleWorkerPath();
        try
        {
            using var resource = typeof(ReimbursementSettings).Assembly.GetManifestResourceStream("ReimBox.PaddleWorker.paddle_vl_worker.py");
            if (resource is null) return destination;

            using var buffer = new MemoryStream();
            resource.CopyTo(buffer);
            var bundledBytes = buffer.ToArray();
            if (!File.Exists(destination) || !File.ReadAllBytes(destination).SequenceEqual(bundledBytes))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(destination, bundledBytes);
            }
        }
        catch
        {
            // Paddle is optional. The recognition service will show a clear message if extraction fails.
        }

        return destination;
    }

    private static string GetDefaultPaddleWorkerScriptPath()
    {
        var fromOutput = Path.Combine(AppContext.BaseDirectory, "PaddleWorker", "paddle_vl_worker.py");
        if (File.Exists(fromOutput)) return fromOutput;

        var developmentCopy = Path.Combine(Directory.GetCurrentDirectory(), "PaddleWorker", "paddle_vl_worker.py");
        return File.Exists(developmentCopy) ? developmentCopy : GetBundledPaddleWorkerPath();
    }

    private static string GetBundledPaddleWorkerPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ReimBox",
        "PaddleWorker",
        "paddle_vl_worker.py");
}
