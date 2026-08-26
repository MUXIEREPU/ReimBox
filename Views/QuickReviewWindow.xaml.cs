using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReimbursementAssistant.Models;

namespace ReimbursementAssistant.Views;

public partial class QuickReviewWindow : Window
{
    private readonly List<InvoiceRecord> _records;
    private int _index;

    public QuickReviewWindow(IEnumerable<InvoiceRecord> records)
    {
        InitializeComponent();
        _records = records.ToList();
        CategoryBox.ItemsSource = InvoiceOptionCatalog.Categories;
        SubCategoryBox.ItemsSource = InvoiceOptionCatalog.TravelSubCategories;
        ShowCurrent();
    }

    public InvoiceRecord? LastReviewedRecord { get; private set; }
    public int ReviewedCount => _index;
    public bool CompletedAll => _index >= _records.Count;
    private InvoiceRecord Current => _records[_index];

    private void ShowCurrent()
    {
        if (_index >= _records.Count)
        {
            MessageBox.Show($"已完成 {_records.Count} 张发票的快速复核。", "复核完成", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            return;
        }
        var record = Current;
        ProgressText.Text = $"{_index + 1} / {_records.Count}";
        FileNameText.Text = record.OriginalFileName;
        PathText.Text = record.OriginalFilePath;
        AmountText.Text = $"¥{(record.TotalAmount ?? 0):N2}";
        MerchantText.Text = record.ProjectMerchantDisplay;
        IssueText.Text = record.ValidationDisplays.FirstOrDefault() ?? record.MaterialStatusLabel;
        CategoryBox.SelectedValue = record.Category == InvoiceCategory.Unknown ? InvoiceCategory.Consumable : record.Category;
        SubCategoryBox.SelectedValue = record.SubCategory;
        ConfirmButton.Content = _index == _records.Count - 1 ? "完成复核" : "确认并下一张";
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        Current.Category = (InvoiceCategory)(CategoryBox.SelectedValue ?? InvoiceCategory.Consumable);
        Current.SubCategory = Current.Category == InvoiceCategory.Travel ? (TravelSubCategory)(SubCategoryBox.SelectedValue ?? TravelSubCategory.None) : TravelSubCategory.None;
        Current.ManualOverride = true;
        Current.ClassificationConfidence = 1.0;
        LastReviewedRecord = Current;
        _index++;
        ShowCurrent();
    }

    private void CategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => SubCategoryBox.IsEnabled = CategoryBox.SelectedValue is InvoiceCategory.Travel;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is >= Key.D1 and <= Key.D5)
        {
            CategoryBox.SelectedIndex = e.Key - Key.D1;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter) { Confirm_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
    }
}
