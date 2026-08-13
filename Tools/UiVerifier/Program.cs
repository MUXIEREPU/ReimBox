using System.IO;
using System.Windows;
using ReimbursementAssistant.Configuration;
using ReimbursementAssistant.Models;
using ReimbursementAssistant.Views;

namespace UiVerifier;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var temporaryFile = Path.Combine(Path.GetTempPath(), $"ReimBox-ui-{Guid.NewGuid():N}.txt");
        File.WriteAllText(temporaryFile, "preview");
        var record = new InvoiceRecord
        {
            OriginalFilePath = temporaryFile,
            TotalAmount = 1m,
            Category = InvoiceCategory.Unknown,
            Status = RecordStatus.NeedConfirmation
        };

        try
        {
            var windows = new Window[]
            {
                new MainWindow(),
                new BulkEditWindow(2),
                new QuickReviewWindow([record]),
                new SettingsWindow(new ReimbursementSettings(), 0),
                new AttachmentPreviewWindow(temporaryFile),
                new ReimbursementInfoWindow("", "", ""),
                new NamingRuleWindow([record], InvoiceNamingRule.Default()),
                new ExportReviewWindow(1, 0, 1, 1m, ["测试提示"]),
                new UpdateAvailableWindow(new ReimbursementAssistant.Services.UpdateCheckResult(true, new Version(0, 2, 0), new Version(0, 3, 0), "v0.3.0", "https://example.com", "测试更新说明"))
            };
            Console.WriteLine($"UI checks: {windows.Length}/{windows.Length} windows loaded");
            foreach (var window in windows) window.Close();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            Application.Current.Shutdown();
            try { File.Delete(temporaryFile); } catch { }
        }
    }
}
