namespace ReimbursementAssistant.Configuration;
public sealed class ReimbursementSettings
{
    public decimal ConsumablePaymentThreshold { get; set; } = 1000m;
    public bool EnablePaddleOcrVl { get; set; } = true;
    public bool EnablePaddleAutoFallback { get; set; }
    public string PaddlePythonExecutable { get; set; } = FindLocalPythonExecutable();
    public string PaddleWorkerScript { get; set; } = FindPaddleWorkerScript();
    // "auto" lets PaddleOCR choose GPU when available, otherwise CPU.
    public string PaddleDevice { get; set; } = "auto";
    // CPU inference and a first model initialization can take several minutes.
    public int PaddleTimeoutSeconds { get; set; } = 600;

    private static string FindLocalPythonExecutable()
    {
        var runtime = Path.Combine(Directory.GetCurrentDirectory(), ".venv_paddleocr_runtime", "Scripts", "python.exe");
        if (File.Exists(runtime)) return runtime;
        var local = Path.Combine(Directory.GetCurrentDirectory(), ".venv_paddleocr", "Scripts", "python.exe");
        return File.Exists(local) ? local : "python";
    }

    private static string FindPaddleWorkerScript()
    {
        var fromOutput = Path.Combine(AppContext.BaseDirectory, "PaddleWorker", "paddle_vl_worker.py");
        if (File.Exists(fromOutput)) return fromOutput;

        return Path.Combine(Directory.GetCurrentDirectory(), "PaddleWorker", "paddle_vl_worker.py");
    }
}
