using System.Windows;
using ReimbursementAssistant.Models;

namespace ReimbursementAssistant.Views;

public partial class BulkEditWindow : Window
{
    public BulkEditWindow(int selectedCount)
    {
        InitializeComponent();
        CountText.Text = $"已选择 {selectedCount} 张发票";
        CategoryBox.ItemsSource = new[]
        {
            new EnumOption<InvoiceCategory>(InvoiceCategory.Consumable, "耗材"),
            new EnumOption<InvoiceCategory>(InvoiceCategory.Travel, "差旅"),
            new EnumOption<InvoiceCategory>(InvoiceCategory.PrintFee, "打印费"),
            new EnumOption<InvoiceCategory>(InvoiceCategory.ShippingFee, "邮寄费"),
            new EnumOption<InvoiceCategory>(InvoiceCategory.Other, "其他")
        };
        CategoryBox.SelectedIndex = 0;
        SubCategoryBox.ItemsSource = new[]
        {
            new EnumOption<TravelSubCategory>(TravelSubCategory.None, "无"),
            new EnumOption<TravelSubCategory>(TravelSubCategory.Flight, "飞机"),
            new EnumOption<TravelSubCategory>(TravelSubCategory.Train, "火车"),
            new EnumOption<TravelSubCategory>(TravelSubCategory.Hotel, "住宿"),
            new EnumOption<TravelSubCategory>(TravelSubCategory.Taxi, "出租车/网约车"),
            new EnumOption<TravelSubCategory>(TravelSubCategory.RentalCar, "租车"),
            new EnumOption<TravelSubCategory>(TravelSubCategory.Toll, "路桥费"),
            new EnumOption<TravelSubCategory>(TravelSubCategory.Fuel, "燃油费"),
            new EnumOption<TravelSubCategory>(TravelSubCategory.OtherTravel, "其他差旅")
        };
        SubCategoryBox.SelectedIndex = 0;
        AttachmentTypeBox.ItemsSource = new[]
        {
            new EnumOption<AttachmentType>(AttachmentType.PaymentProof, "支付记录截图"),
            new EnumOption<AttachmentType>(AttachmentType.OrderPage, "订单页面"),
            new EnumOption<AttachmentType>(AttachmentType.ThreeDPrintDetails, "3D打印明细"),
            new EnumOption<AttachmentType>(AttachmentType.Other, "其他材料")
        };
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
