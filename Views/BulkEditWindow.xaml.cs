using System.Windows;
using ReimbursementAssistant.Models;

namespace ReimbursementAssistant.Views;

public partial class BulkEditWindow : Window
{
    public BulkEditWindow(int selectedCount)
    {
        InitializeComponent();
        CountText.Text = $"已选择 {selectedCount} 张发票";
        CategoryBox.ItemsSource = InvoiceOptionCatalog.Categories;
        CategoryBox.SelectedIndex = 0;
        SubCategoryBox.ItemsSource = InvoiceOptionCatalog.TravelSubCategories;
        SubCategoryBox.SelectedIndex = 0;
        AttachmentTypeBox.ItemsSource = InvoiceOptionCatalog.SupplementAttachmentTypes;
        AttachmentTypeBox.SelectedIndex = 0;
    }

    public BulkEditRequest Request => new()
    {
        ChangeCategory = CategoryCheck.IsChecked == true,
        Category = (InvoiceCategory)(CategoryBox.SelectedValue ?? InvoiceCategory.Consumable),
        ChangeSubCategory = SubCategoryCheck.IsChecked == true,
        SubCategory = (TravelSubCategory)(SubCategoryBox.SelectedValue ?? TravelSubCategory.None),
        ChangeAttachmentType = AttachmentTypeCheck.IsChecked == true,
        AttachmentType = (AttachmentType)(AttachmentTypeBox.SelectedValue ?? AttachmentType.Other),
        MarkIgnored = IgnoreCheck.IsChecked == true,
        RestoreIgnored = RestoreCheck.IsChecked == true,
        Reanalyze = ReanalyzeCheck.IsChecked == true,
        RemoveFromList = RemoveCheck.IsChecked == true
    };

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (IgnoreCheck.IsChecked == true && RestoreCheck.IsChecked == true)
        {
            MessageBox.Show("“标记为忽略”和“恢复已忽略项目”不能同时选择。", "操作相互冲突", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!Request.ChangeCategory && !Request.ChangeSubCategory && !Request.ChangeAttachmentType && !Request.MarkIgnored && !Request.RestoreIgnored && !Request.Reanalyze && !Request.RemoveFromList)
        {
            MessageBox.Show("请至少勾选一项需要执行的操作。", "尚未选择操作", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
