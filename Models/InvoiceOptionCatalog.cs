namespace ReimbursementAssistant.Models;

public static class InvoiceOptionCatalog
{
    public static IReadOnlyList<EnumOption<InvoiceCategory>> Categories { get; } =
    [
        new(InvoiceCategory.Consumable, "耗材"),
        new(InvoiceCategory.Travel, "差旅"),
        new(InvoiceCategory.PrintFee, "打印费"),
        new(InvoiceCategory.ShippingFee, "邮寄费"),
        new(InvoiceCategory.Other, "其他")
    ];

    public static IReadOnlyList<EnumOption<TravelSubCategory>> TravelSubCategories { get; } =
    [
        new(TravelSubCategory.None, "无"),
        new(TravelSubCategory.Flight, "飞机"),
        new(TravelSubCategory.Train, "火车"),
        new(TravelSubCategory.Hotel, "住宿"),
        new(TravelSubCategory.Taxi, "出租车/网约车"),
        new(TravelSubCategory.RentalCar, "租车"),
        new(TravelSubCategory.Toll, "路桥费"),
        new(TravelSubCategory.Fuel, "燃油费"),
        new(TravelSubCategory.Meal, "伙食费"),
        new(TravelSubCategory.OtherTravel, "其他差旅")
    ];

    public static IReadOnlyList<EnumOption<AttachmentType>> SupplementAttachmentTypes { get; } =
    [
        new(AttachmentType.PaymentProof, "支付记录截图"),
        new(AttachmentType.OrderPage, "订单页面"),
        new(AttachmentType.ThreeDPrintDetails, "3D打印明细"),
        new(AttachmentType.Other, "其他材料")
    ];
}
