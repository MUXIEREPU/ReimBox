using System.Windows;
using System.Windows.Controls;
using ReimbursementAssistant.Models;
using ReimbursementAssistant.Services;

namespace ReimbursementAssistant.Views;

public partial class NamingRuleWindow : Window
{
    private readonly IReadOnlyList<InvoiceRecord> _records;
    private readonly InvoiceNamingService _naming = new();
    private bool _isInitializing = true;

    public NamingRuleWindow(IReadOnlyList<InvoiceRecord> records, InvoiceNamingRule currentRule)
    {
        InitializeComponent();
        _records = records;
        LoadRule(currentRule);
        RefreshPreview();
    }

    public InvoiceNamingRule Rule { get; private set; } = InvoiceNamingRule.Default();
    public NamingApplyTarget ApplyTarget => RenameOriginalRadio.IsChecked == true ? NamingApplyTarget.RenameOriginalFiles : NamingApplyTarget.ExportOnly;

    private void LoadRule(InvoiceNamingRule rule)
    {
        _isInitializing = true;
        NumberCheck.IsChecked = rule.Fields.Contains(NamingField.Number);
        CategoryCheck.IsChecked = rule.Fields.Contains(NamingField.Category);
        ItemCheck.IsChecked = rule.Fields.Contains(NamingField.ItemDescription);
        AmountCheck.IsChecked = rule.Fields.Contains(NamingField.TotalAmount);
        DateCheck.IsChecked = rule.Fields.Contains(NamingField.InvoiceDate);
        SellerCheck.IsChecked = rule.Fields.Contains(NamingField.SellerName);
        InvoiceNumberCheck.IsChecked = rule.Fields.Contains(NamingField.InvoiceNumber);

        SeparatorBox.SelectedIndex = rule.Separator switch
        {
            "-" => 1,
            " " => 2,
            _ => 0
        };
        _isInitializing = false;
        Rule = BuildRuleFromUi();
    }

    private InvoiceNamingRule BuildRuleFromUi()
    {
        var rule = new InvoiceNamingRule
        {
            Separator = ParseSeparator((SeparatorBox.SelectedItem as ComboBoxItem)?.Content?.ToString())
        };

        AddIfChecked(rule, NumberCheck, NamingField.Number);
        AddIfChecked(rule, CategoryCheck, NamingField.Category);
        AddIfChecked(rule, ItemCheck, NamingField.ItemDescription);
        AddIfChecked(rule, AmountCheck, NamingField.TotalAmount);
        AddIfChecked(rule, DateCheck, NamingField.InvoiceDate);
        AddIfChecked(rule, SellerCheck, NamingField.SellerName);
        AddIfChecked(rule, InvoiceNumberCheck, NamingField.InvoiceNumber);
        return rule;
    }

    private static void AddIfChecked(InvoiceNamingRule rule, CheckBox checkBox, NamingField field)
    {
        if (checkBox.IsChecked == true)
        {
            rule.Fields.Add(field);
        }
    }

    private static string ParseSeparator(string? display)
    {
        return display switch
        {
            { } text when text.StartsWith("-") => "-",
            { } text when text.StartsWith("空格") => " ",
            _ => "_"
        };
    }

    private void Rule_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        Rule = BuildRuleFromUi();
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (_isInitializing || _records is null)
        {
            return;
        }

        var sampleRecords = _records.Take(80).ToList();
        PreviewHintText.Text = sampleRecords.Count < _records.Count ? $"预览前 {sampleRecords.Count} / {_records.Count} 个" : $"共 {_records.Count} 个";
        PreviewGrid.ItemsSource = sampleRecords.Select(record => new NamingPreviewRow(record.OriginalFileName, _naming.BuildFileName(record, Rule))).ToList();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        Rule = BuildRuleFromUi();
        if (Rule.Fields.Count == 0)
        {
            MessageBox.Show("请至少选择一个命名字段。", "命名规则不完整", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private sealed record NamingPreviewRow(string OriginalName, string NewName);
}
