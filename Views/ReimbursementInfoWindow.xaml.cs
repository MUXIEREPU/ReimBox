using System.Windows;
using System.Windows.Controls;

namespace ReimbursementAssistant.Views;

public partial class ReimbursementInfoWindow : Window
{
    public ReimbursementInfoWindow(string personName, string personIdentifier, string description)
    {
        InitializeComponent();
        NameBox.Text = personName;
        IdentifierBox.Text = personIdentifier;
        DescriptionBox.Text = description;
        RefreshPlaceholder();
    }

    public string PersonName => NameBox.Text.Trim();
    public string PersonIdentifier => IdentifierBox.Text.Trim();
    public string Description => DescriptionBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PersonName))
        {
            MessageBox.Show("请填写报销人姓名。", "缺少必填项", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(PersonIdentifier))
        {
            MessageBox.Show("请填写学号或工号。", "缺少必填项", MessageBoxButton.OK, MessageBoxImage.Warning);
            IdentifierBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void DescriptionBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshPlaceholder();
    }

    private void RefreshPlaceholder()
    {
        if (DescriptionPlaceholder is null || DescriptionBox is null) return;
        DescriptionPlaceholder.Visibility = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }
}
