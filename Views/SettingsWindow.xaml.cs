using System.Globalization;
using System.Windows;
using ReimbursementAssistant.Configuration;

namespace ReimbursementAssistant.Views;

public partial class SettingsWindow : Window
{
    private readonly ReimbursementSettings _originalSettings;

    public SettingsWindow(ReimbursementSettings settings, int learnedCorrectionCount)
    {
        InitializeComponent();
        _originalSettings = settings.Clone();
        LearningCountText.Text = $"本地已学习 {learnedCorrectionCount} 个销售方的分类习惯。";
        ApplyToControls(settings);
    }

    public ReimbursementSettings ResultSettings { get; private set; } = new();
    public bool ClearLearningRequested { get; private set; }

    private void ApplyToControls(ReimbursementSettings settings)
    {
        ThresholdBox.Text = settings.ConsumablePaymentThreshold.ToString("0.##", CultureInfo.InvariantCulture);
        ThreeDDetailsCheck.IsChecked = settings.RequireThreeDPrintDetails;
        FlightOrderCheck.IsChecked = settings.RequireFlightOrderPage;
        FlightPaymentCheck.IsChecked = settings.RequireFlightPaymentProof;
        TrainOrderCheck.IsChecked = settings.RequireTrainOrderPage;
        IncompleteExportCheck.IsChecked = settings.AllowIncompleteExport;
        UpdateCheck.IsChecked = settings.CheckForUpdatesOnStartup;
        PaddleAutoCheck.IsChecked = settings.EnablePaddleAutoFallback;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(ThresholdBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var threshold) || threshold < 0 || threshold > 10_000_000)
        {
            MessageBox.Show("请输入 0～10000000 之间的有效金额。", "金额格式不正确", MessageBoxButton.OK, MessageBoxImage.Warning);
            ThresholdBox.Focus();
            return;
        }
        ResultSettings = _originalSettings.Clone();
        ResultSettings.ConsumablePaymentThreshold = threshold;
        ResultSettings.RequireThreeDPrintDetails = ThreeDDetailsCheck.IsChecked == true;
        ResultSettings.RequireFlightOrderPage = FlightOrderCheck.IsChecked == true;
        ResultSettings.RequireFlightPaymentProof = FlightPaymentCheck.IsChecked == true;
        ResultSettings.RequireTrainOrderPage = TrainOrderCheck.IsChecked == true;
        ResultSettings.AllowIncompleteExport = IncompleteExportCheck.IsChecked == true;
        ResultSettings.CheckForUpdatesOnStartup = UpdateCheck.IsChecked == true;
        ResultSettings.EnablePaddleAutoFallback = PaddleAutoCheck.IsChecked == true;
        DialogResult = true;
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e) => ApplyToControls(new ReimbursementSettings());
    private void ClearLearning_Click(object sender, RoutedEventArgs e)
    {
        ClearLearningRequested = true;
        LearningCountText.Text = "保存设置后将清除本地分类学习记录。";
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
