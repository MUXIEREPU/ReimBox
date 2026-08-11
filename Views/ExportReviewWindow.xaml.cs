using System.Windows;

namespace ReimbursementAssistant.Views;

public partial class ExportReviewWindow : Window
{
    public ExportReviewWindow(int totalCount, int completeCount, int pendingCount, decimal totalAmount, IEnumerable<string> issues)
    {
        InitializeComponent();
        TotalCountText.Text = totalCount.ToString();
        CompleteCountText.Text = completeCount.ToString();
        PendingCountText.Text = pendingCount.ToString();
        TotalAmountText.Text = $"¥{totalAmount:N2}";

        var issueList = issues.ToList();
        IssueList.ItemsSource = issueList.Count == 0 ? ["✓ 当前没有发现缺失材料或待确认项。"] : issueList;
        HintText.Text = issueList.Count == 0 ? "材料检查通过。" : $"仍有 {issueList.Count} 项需要注意。";
        ContinueButton.Content = issueList.Count == 0 ? "开始导出" : "仍然导出";
    }

    private void Return_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
