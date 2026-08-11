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
    private static readonly string[] PrintFeeKeywords = ["打印费", "文件打印", "资料打印", "文本打印", "复印", "装订", "扫描"];
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
        var travel = TravelRules.FirstOrDefault(x => x.Keywords.Any(k => source.Contains(k, StringComparison.OrdinalIgnoreCase)));
        if (travel.Keywords is not null) { record.Category = InvoiceCategory.Travel; record.SubCategory = travel.Type; record.Confidence = 0.85; return; }
        if (PrintFeeKeywords.Any(k => source.Contains(k, StringComparison.OrdinalIgnoreCase))) { record.Category = InvoiceCategory.PrintFee; record.SubCategory = TravelSubCategory.None; record.Confidence = 0.75; return; }
        if (ConsumableKeywords.Any(k => source.Contains(k, StringComparison.OrdinalIgnoreCase))) { record.Category = InvoiceCategory.Consumable; record.SubCategory = TravelSubCategory.None; record.Confidence = 0.7; return; }
        if (record.OriginalFilePath.Contains("耗材", StringComparison.OrdinalIgnoreCase)) { record.Category = InvoiceCategory.Consumable; record.SubCategory = TravelSubCategory.None; record.Confidence = 0.65; return; }
        if (record.OriginalFilePath.Contains("差旅", StringComparison.OrdinalIgnoreCase)) { record.Category = InvoiceCategory.Travel; record.SubCategory = TravelSubCategory.OtherTravel; record.Confidence = 0.65; return; }
        record.Category = InvoiceCategory.Unknown; record.SubCategory = TravelSubCategory.None; record.Confidence = 0;
    }
}
