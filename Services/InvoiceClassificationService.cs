using ReimbursementAssistant.Models;

namespace ReimbursementAssistant.Services;

public sealed class InvoiceClassificationService
{
    private static readonly (TravelSubCategory Type, string[] Keywords)[] TravelRules =
    [
        (TravelSubCategory.Flight, ["航空", "机票", "航班", "电子客票"]),
        (TravelSubCategory.Train, ["铁路", "火车票", "中国铁路"]),
        (TravelSubCategory.Hotel, ["酒店", "宾馆", "住宿"]),
        (TravelSubCategory.Taxi, ["出租车", "网约车", "滴滴", "打车"]),
        (TravelSubCategory.RentalCar, ["租车"]),
        (TravelSubCategory.Toll, ["高速公路", "通行费", "路桥"]),
        (TravelSubCategory.Fuel, ["燃油", "加油"])
    ];
    private static readonly string[] ThreeDPrintKeywords = ["3D打印", "3D 打印", "三维打印", "增材制造", "快速成型"];
    private static readonly string[] PrintFeeKeywords =
    [
        "打印费", "文件打印", "资料打印", "文本打印", "文印服务", "图文快印", "数码快印",
        "黑白打印", "彩色打印", "打印装订", "复印", "装订", "晒图", "扫描服务"
    ];
    private static readonly string[] ShippingFeeKeywords =
    [
        "邮寄费", "邮递费", "快递费", "快递服务", "国内快递", "收派服务", "派收服务", "寄递服务",
        "邮政服务", "物流服务", "配送服务", "运费", "邮费", "寄件",
        "顺丰", "中国邮政", "邮政速递", "EMS快递", "EMS邮寄", "中通快递", "圆通速递", "申通快递",
        "韵达快递", "京东物流", "德邦快递"
    ];
    private static readonly string[] ConsumableKeywords = ["材料", "配件", "电机", "螺丝", "零件", "工具", "耗材", "电子元件", "加工件", "办公用品", "实验用品", "3D打印", "3D 打印", "三维打印"];

    public void Classify(InvoiceRecord record, string text)
    {
        if (record.ManualOverride) return;
        var source = $"{record.OriginalFileName}\n{text}";
        if (ThreeDPrintKeywords.Any(k => source.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            record.Category = InvoiceCategory.Consumable;
            record.SubCategory = TravelSubCategory.None;
            record.IsThreeDPrinting = true;
            record.Confidence = 0.85;
            return;
        }

        record.IsThreeDPrinting = false;
        if (ShippingFeeKeywords.Any(k => source.Contains(k, StringComparison.OrdinalIgnoreCase))) { record.Category = InvoiceCategory.ShippingFee; record.SubCategory = TravelSubCategory.None; record.Confidence = 0.82; return; }
        if (PrintFeeKeywords.Any(k => source.Contains(k, StringComparison.OrdinalIgnoreCase))) { record.Category = InvoiceCategory.PrintFee; record.SubCategory = TravelSubCategory.None; record.Confidence = 0.82; return; }
        var travel = TravelRules.FirstOrDefault(x => x.Keywords.Any(k => source.Contains(k, StringComparison.OrdinalIgnoreCase)));
        if (travel.Keywords is not null) { record.Category = InvoiceCategory.Travel; record.SubCategory = travel.Type; record.Confidence = 0.85; return; }
        if (ConsumableKeywords.Any(k => source.Contains(k, StringComparison.OrdinalIgnoreCase))) { record.Category = InvoiceCategory.Consumable; record.SubCategory = TravelSubCategory.None; record.Confidence = 0.7; return; }
        if (record.OriginalFilePath.Contains("耗材", StringComparison.OrdinalIgnoreCase)) { record.Category = InvoiceCategory.Consumable; record.SubCategory = TravelSubCategory.None; record.Confidence = 0.65; return; }
        if (record.OriginalFilePath.Contains("差旅", StringComparison.OrdinalIgnoreCase)) { record.Category = InvoiceCategory.Travel; record.SubCategory = TravelSubCategory.OtherTravel; record.Confidence = 0.65; return; }
        if (record.OriginalFilePath.Contains("打印费", StringComparison.OrdinalIgnoreCase)) { record.Category = InvoiceCategory.PrintFee; record.SubCategory = TravelSubCategory.None; record.Confidence = 0.65; return; }
        if (record.OriginalFilePath.Contains("邮寄费", StringComparison.OrdinalIgnoreCase)) { record.Category = InvoiceCategory.ShippingFee; record.SubCategory = TravelSubCategory.None; record.Confidence = 0.65; return; }
        record.Category = InvoiceCategory.Unknown; record.SubCategory = TravelSubCategory.None; record.Confidence = 0;
    }
}
