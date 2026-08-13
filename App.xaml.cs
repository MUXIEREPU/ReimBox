using System.Windows;
using System.Windows.Threading;
namespace ReimbursementAssistant;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReimBox");
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(
                Path.Combine(logDirectory, "crash.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{e.Exception}\n{new string('-', 72)}\n");
        }
        catch
        {
            // 写入诊断日志失败时仍继续显示友好提示。
        }

        MessageBox.Show(
            $"ReimBox 遇到了一个未预期的问题，但原始文件没有被修改。\n\n{e.Exception.Message}\n\n你可以关闭此提示后重试；如果问题重复出现，请保留出错文件并联系开发者。",
            "ReimBox 运行提示",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Current.Shutdown(-1);
    }
}
