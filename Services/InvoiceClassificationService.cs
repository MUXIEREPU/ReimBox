using ReimbursementAssistant.Models;

namespace ReimbursementAssistant.Services;

public sealed class InvoiceClassificationService
{
    // “航空”常见于机票发票（航空公司名称），但“航空插头/插座/接头”是电子元件，需要排除。
    private static readonly string[] AviationExclusions =
    [
        "插头", "插座", "接头", "接插件", "连接器", "插件", "端子", "航插",
        "线缆", "电缆", "电线", "电器", "电子", "接插"
    ];

    private static readonly (TravelSubCategory Type, string[] Keywords, string[]? Exclusions)[] TravelRules =
    [
        (TravelSubCategory.Flight, ["航空", "机票", "航班", "电子客票"], AviationExclusions),
        (TravelSubCategory.Train, ["铁路", "火车票", "中国铁路"], null),
        (TravelSubCategory.Hotel, ["酒店", "宾馆", "住宿"], null),
        (TravelSubCategory.Taxi, ["出租车", "网约车", "滴滴", "打车"], null),
        (TravelSubCategory.RentalCar, ["租车"], null),
        (TravelSubCategory.Toll, ["高速公路", "通行费", "路桥"], null),
        (TravelSubCategory.Fuel, ["燃油", "加油"], null),
        (TravelSubCategory.Meal, ["餐饮", "餐费", "伙食", "用餐", "食堂", "饮食服务", "餐厅服务"], null)
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
    private static readonly string[] ConsumableKeywords =
    [
        // 原有
        "材料", "配件", "电机", "螺丝", "零件", "工具", "耗材", "电子元件", "加工件", "办公用品", "实验用品",
        "3D打印", "3D 打印", "三维打印",
        // 紧固件
        "螺母", "螺栓", "螺钉", "垫圈", "垫片", "紧固件",
        // 密封件 / 橡胶
        "密封圈", "密封件", "O型圈", "o型圈", "橡胶", "硅胶", "氟橡胶", "丁腈",
        // 线缆 / 电源
        "电缆", "电线", "电源线", "护套线", "信号线", "连接线", "线束",
        "电源模块", "降压模块", "稳压模块", "转换器", "逆变器",
        // 电子元器件 / 开发板
        "开发板", "开发套件", "载板", "单片机", "传感器", "电容", "电阻", "连接器",
        "芯片", "模块", "继电器", "开关电源", "散热器", "导热片", "导热膏", "硅脂",
        // 材料
        "亚克力", "有机玻璃", "铝合金", "铜板", "黄铜", "紫铜", "五金", "不锈钢",
        "涂料", "油漆", "稀释剂", "清洗剂", "环氧树脂", "聚脲", "防水防腐",
        // 加工 / 工具
        "机加工", "数控", "车床", "铣床", "精密加工", "定制加工", "钣金",
        "喷枪", "喷漆枪", "电钻", "螺丝刀", "扳手", "钳子", "刀具", "量具",
        // 模型 / 实验
        "模型", "油泥", "雕刻", "翻模", "试验器", "实验器材", "教学仪器"
    ];

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

        foreach (var (type, keywords, exclusions) in TravelRules)
        {
            if (keywords.Any(k => KeywordMatches(source, k, exclusions)))
            {
                record.Category = InvoiceCategory.Travel;
                record.SubCategory = type;
                record.Confidence = 0.85;
                return;
            }
        }

        if (ConsumableKeywords.Any(k => source.Contains(k, StringComparison.OrdinalIgnoreCase))) { record.Category = InvoiceCategory.Consumable; record.SubCategory = TravelSubCategory.None; record.Confidence = 0.7; return; }
        if (record.OriginalFilePath.Contains("耗材", StringComparison.OrdinalIgnoreCase)) { record.Category = InvoiceCategory.Consumable; record.SubCategory = TravelSubCategory.None; record.Confidence = 0.65; return; }
        if (record.OriginalFilePath.Contains("差旅", StringComparison.OrdinalIgnoreCase)) { record.Category = InvoiceCategory.Travel; record.SubCategory = TravelSubCategory.OtherTravel; record.Confidence = 0.65; return; }
        if (record.OriginalFilePath.Contains("打印费", StringComparison.OrdinalIgnoreCase)) { record.Category = InvoiceCategory.PrintFee; record.SubCategory = TravelSubCategory.None; record.Confidence = 0.65; return; }
        if (record.OriginalFilePath.Contains("邮寄费", StringComparison.OrdinalIgnoreCase)) { record.Category = InvoiceCategory.ShippingFee; record.SubCategory = TravelSubCategory.None; record.Confidence = 0.65; return; }
        record.Category = InvoiceCategory.Unknown; record.SubCategory = TravelSubCategory.None; record.Confidence = 0;
    }

    /// <summary>
    /// 判断关键词是否在文本中出现，且附近没有排除词。
    /// 排除词用于避免“航空插头”等工业术语被误判为机票。
    /// </summary>
    private static bool KeywordMatches(string source, string keyword, string[]? exclusions)
    {
        if (exclusions is null || exclusions.Length == 0)
            return source.Contains(keyword, StringComparison.OrdinalIgnoreCase);

        var searchFrom = 0;
        while (searchFrom < source.Length)
        {
            var idx = source.IndexOf(keyword, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            // 检查关键词前后各 10 个字符范围内是否有排除词
            var contextStart = Math.Max(0, idx - 10);
            var contextEnd = Math.Min(source.Length, idx + keyword.Length + 10);
            var context = source.Substring(contextStart, contextEnd - contextStart);
            if (!exclusions.Any(e => context.Contains(e, StringComparison.OrdinalIgnoreCase)))
                return true;

            searchFrom = idx + keyword.Length;
        }
        return false;
    }
}
