using System.Windows;
using ReimbursementAssistant.Services;

namespace ReimbursementAssistant.Views;

public partial class UpdateAvailableWindow : Window
{
    public UpdateAvailableWindow(UpdateCheckResult result)
    {
        InitializeComponent();
        VersionText.Text = $"ReimBox {result.Tag}";
        CurrentVersionText.Text = $"当前版本：v{result.CurrentVersion.ToString(3)}";
        NotesText.Text = string.IsNullOrWhiteSpace(result.ReleaseNotes) ? "该版本暂未填写更新说明。" : result.ReleaseNotes.Trim();
    }
    public bool IgnoreThisVersion => IgnoreCheck.IsChecked == true;
    private void Later_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Download_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
