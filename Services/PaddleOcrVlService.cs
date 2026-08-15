using System.Diagnostics;
using System.ComponentModel;
using System.Text.Json;
using ReimbursementAssistant.Configuration;

namespace ReimbursementAssistant.Services;

public sealed record PaddleOcrResult(string Text, string? FailureReason = null);

/// <summary>Executes the official local PaddleOCRVL Python API. No document leaves the computer.</summary>
public sealed class PaddleOcrVlService(ReimbursementSettings settings)
{
    public async Task<PaddleOcrResult> TryReadAsync(string filePath)
    {
        if (!settings.EnablePaddleOcrVl) return new PaddleOcrResult(string.Empty, "PaddleOCR‑VL 已在设置中关闭");
        var work = Path.Combine(Path.GetTempPath(), "ReimBox", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var workerScript = settings.PaddleWorkerScript;
            if (!File.Exists(workerScript))
            {
                workerScript = ReimbursementSettings.EnsureBundledPaddleWorker();
                settings.PaddleWorkerScript = workerScript;
            }
            if (!File.Exists(workerScript)) return new PaddleOcrResult(string.Empty, "PaddleOCR‑VL worker 未找到");
            var info = new ProcessStartInfo { FileName = settings.PaddlePythonExecutable, RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add(workerScript); info.ArgumentList.Add("--input"); info.ArgumentList.Add(filePath);
            info.ArgumentList.Add("--output"); info.ArgumentList.Add(work);
            info.ArgumentList.Add("--device"); info.ArgumentList.Add(settings.PaddleDevice);
            using var process = Process.Start(info);
            if (process is null) return new PaddleOcrResult(string.Empty, "无法启动 PaddleOCR‑VL");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.PaddleTimeoutSeconds));
            try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { try { process.Kill(true); } catch { } return new PaddleOcrResult(string.Empty, "PaddleOCR‑VL 识别超时"); }
            var error = await process.StandardError.ReadToEndAsync();
            if (process.ExitCode != 0) return new PaddleOcrResult(string.Empty, Shorten(error, "PaddleOCR‑VL 未就绪或识别失败"));
            var text = string.Join(Environment.NewLine, Directory.EnumerateFiles(work, "*.json", SearchOption.AllDirectories).Select(ReadRecognizedText).Where(x => !string.IsNullOrWhiteSpace(x)));
            return string.IsNullOrWhiteSpace(text) ? new PaddleOcrResult(string.Empty, "PaddleOCR‑VL 未返回可用结构化文字") : new PaddleOcrResult(text);
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException) { return new PaddleOcrResult(string.Empty, "未找到 PaddleOCR Python 环境；请完成本地引擎安装"); }
        catch (Exception ex) { return new PaddleOcrResult(string.Empty, Shorten(ex.Message, "PaddleOCR‑VL 调用失败")); }
        finally { try { Directory.Delete(work, true); } catch { } }
    }
    private static string ReadRecognizedText(string path)
    {
        try { using var doc = JsonDocument.Parse(File.ReadAllText(path)); var values = new List<string>(); Collect(doc.RootElement, values, null); return string.Join(Environment.NewLine, values.Distinct()); }
        catch { return string.Empty; }
    }
    private static void Collect(JsonElement element, ICollection<string> values, string? key)
    {
        if (element.ValueKind == JsonValueKind.Object) foreach (var property in element.EnumerateObject()) Collect(property.Value, values, property.Name);
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Collect(item, values, key);
        else if (element.ValueKind == JsonValueKind.String && key is not null && (key.Contains("text", StringComparison.OrdinalIgnoreCase) || key.Contains("content", StringComparison.OrdinalIgnoreCase) || key.Contains("markdown", StringComparison.OrdinalIgnoreCase))) values.Add(element.GetString() ?? string.Empty);
    }
    private static string Shorten(string text, string fallback) => string.IsNullOrWhiteSpace(text) ? fallback : text.Replace(Environment.NewLine, " ").Trim()[..Math.Min(120, text.Trim().Length)];
}
