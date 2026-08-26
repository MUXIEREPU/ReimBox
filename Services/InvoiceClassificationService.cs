using System.Text.RegularExpressions;
using ReimbursementAssistant.Models;

namespace ReimbursementAssistant.Services;

public sealed class InvoiceClassificationService
{
    private static readonly Regex TaxCategoryPattern = new(@"\*(?<category>[^*\r\n]{1,40})\*", RegexOptions.Compiled);
    private static readonly Regex FlightTransportPattern = new(@"(?:航空运输(?:服务)?|交通工具类型[\s\S]{0,80}?(?:飞机|航空))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TrainTransportPattern = new(@"(?:铁路运输(?:服务)?|交通工具类型[\s\S]{0,80}?(?:火车|高铁|动车))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TaxiTransportPattern = new(@"(?:交通工具类型[\s\S]{0,80}?出租车)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] AviationExclusions =
    [
        "航空插头", "航插", "航空连接器", "航空接头", "航空插座", "航空线缆",
        "航空航天设备", "航天设备", "航模", "无人机", "飞控", "接插件", "连接器",
        "插头", "插座", "接头", "端子", "线缆", "电缆", "电子元件"
    ];

    private static readonly string[] ThreeDPrintKeywords =
        ["3D打印", "3D 打印", "三维打印", "增材制造", "快速成型"];

    private static readonly string[] ShippingItemKeywords =
    [
        "邮寄费", "邮递费", "快递费", "快递服务", "国内快递", "收派服务", "派收服务",
        "寄递服务", "邮政服务", "物流服务", "配送服务", "货物运输服务", "货物运输费",
        "运输费", "运费", "邮费", "寄件服务"
    ];

    private static readonly string[] ShippingSellerKeywords =
    [
        "顺丰速运", "顺泰速运", "中国邮政", "邮政速递", "中通快递", "圆通速递",
        "申通快递", "韵达快递", "京东物流", "德邦快递", "极兔速递", "货拉拉"
    ];

    private static readonly string[] PrintFeeKeywords =
    [
        "打印费", "文件打印", "资料打印", "文本打印", "文印服务", "图文快印", "数码快印",
        "黑白打印", "彩色打印", "打印装订", "打印服务", "打印清单", "印刷费", "复印费",
        "装订费", "复印", "装订服务", "晒图", "扫描服务"
    ];

    private static readonly string[] MealKeywords =
    [
        "餐饮服务", "餐饮费", "餐费", "伙食费", "伙食", "用餐", "堂食", "餐厅服务",
        "快餐", "饭菜", "餐饮", "食堂用餐", "熟肉制品", "乳制品", "焙烤食品", "方便食品",
        "冷冻饮品", "软饮料", "水果", "果类加工品", "糖果类食品"
    ];

    private static readonly string[] MealSellerKeywords =
        ["餐饮", "餐厅", "餐馆", "饭店", "酒楼", "小吃", "快餐", "食堂", "火锅", "烧烤"];

    private static readonly string[] MealExclusions =
        ["餐饮设备", "食堂设备", "厨房设备", "餐具", "食品加工设备"];

    private static readonly string[] FlightKeywords =
        ["机票", "飞机票", "航空运输", "航空旅客运输", "航空运输电子客票", "电子客票", "客票"];

    private static readonly string[] TrainKeywords =
        ["火车票", "高铁票", "动车票", "高铁", "动车", "铁路电子客票", "铁路运输", "中国铁路", "12306"];

    private static readonly string[] HotelKeywords =
        ["住宿服务", "住宿费", "酒店住宿", "客房费", "房费", "宾馆住宿", "代订住宿"];

    private static readonly string[] TaxiKeywords =
        ["出租车", "网约车", "滴滴", "打车", "客运服务费", "客运服务", "网络预约出租汽车"];

    private static readonly string[] TaxiSellerKeywords =
        ["滴滴出行", "曹操出行", "首汽约车", "享道出行", "T3出行", "高德打车"];

    private static readonly string[] RentalCarKeywords =
        ["汽车租赁", "车辆租赁", "租车费", "租车服务", "自驾租车"];

    private static readonly string[] TollKeywords =
        ["通行费", "过路费", "高速费", "高速公路通行", "道路通行服务", "ETC通行"];

    private static readonly string[] FuelKeywords =
        ["成品油", "车用汽油", "汽油", "柴油", "燃油费", "油费", "加油费", "加油服务"];

    private static readonly string[] OtherTravelKeywords =
        ["大巴", "长途汽车", "汽车票", "公路客票", "客运票", "轮渡", "船票"];

    private static readonly string[] OtherKeywords =
        ["会议费", "会议服务", "会展服务", "展览服务", "注册费", "报名费", "会费", "展位费", "技术参观费", "TECHNICAL TOUR"];

    private static readonly string[] ConsumableKeywords =
    [
        "材料", "配件", "电机", "螺丝", "零件", "工具", "耗材", "电子元件", "电子元器件",
        "加工件", "办公用品", "实验用品", "螺母", "螺栓", "螺钉", "垫圈", "垫片", "紧固件",
        "密封圈", "密封件", "O型圈", "橡胶", "硅胶", "电缆", "电线", "电源线", "信号线",
        "连接线", "线束", "电源模块", "降压模块", "稳压模块", "转换器", "逆变器", "开发板",
        "开发套件", "载板", "单片机", "传感器", "电容", "电阻", "连接器", "芯片", "模块",
        "继电器", "开关电源", "散热器", "导热片", "导热膏", "硅脂", "亚克力", "有机玻璃",
        "铝合金", "铜板", "黄铜", "紫铜", "五金", "不锈钢", "涂料", "油漆", "稀释剂",
        "清洗剂", "环氧树脂", "聚脲", "防水防腐", "机加工", "数控", "车床", "铣床",
        "精密加工", "定制加工", "钣金", "喷枪", "喷漆枪", "电钻", "螺丝刀", "扳手",
        "钳子", "刀具", "量具", "模型", "油泥", "雕刻", "翻模", "试验器", "实验器材",
        "教学仪器", "航空航天设备", "航天设备", "航模", "无人机", "飞控", "电池", "电芯",
        "燃料电池", "电路板", "外壳", "防水壳", "干燥剂", "碳纤维", "焊接设备", "插头",
        "插座", "接头", "航插", "管夹", "天线", "水表", "充电器", "存储卡", "SD卡"
    ];

    public void Classify(InvoiceRecord record, string text)
    {
        if (record.ManualOverride) return;

        record.IsThreeDPrinting = false;
        var item = record.ItemDescription ?? "";
        var fileName = Path.GetFileNameWithoutExtension(record.OriginalFileName);
        var seller = record.SellerName ?? "";
        var taxCategories = string.Join(" ", TaxCategoryPattern.Matches(text).Select(match => match.Groups["category"].Value));
        var primary = $"{item} {fileName} {taxCategories}";
        var immediateFolder = Path.GetFileName(Path.GetDirectoryName(record.OriginalFilePath) ?? "");
        var invoiceEvidence = !string.IsNullOrWhiteSpace(record.InvoiceNumber) ||
                              (ContainsAny(text, "发票号码", "电子发票", "增值税发票") &&
                               ContainsAny(text, "开票日期", "价税合计", "税率/征收率"));

        if (!invoiceEvidence)
        {
            if (!TryFileNameFallback(record, fileName))
                Set(record, InvoiceCategory.Unknown, TravelSubCategory.None, 0);
            return;
        }

        if (ContainsAny(primary, ThreeDPrintKeywords))
        {
            record.IsThreeDPrinting = true;
            Set(record, InvoiceCategory.Consumable, TravelSubCategory.None, 0.96);
            return;
        }

        if (ContainsAny(primary, ShippingItemKeywords) || ContainsAny(seller, ShippingSellerKeywords))
        {
            Set(record, InvoiceCategory.ShippingFee, TravelSubCategory.None, 0.94);
            return;
        }

        if (ContainsAny(primary, PrintFeeKeywords))
        {
            Set(record, InvoiceCategory.PrintFee, TravelSubCategory.None, 0.94);
            return;
        }

        if (ContainsAny(primary, MealKeywords) && !ContainsAny(primary, MealExclusions))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Meal, 0.94);
            return;
        }

        if (!ContainsAny(primary, AviationExclusions) &&
            (ContainsAny(primary, FlightKeywords) || FlightTransportPattern.IsMatch(text) ||
             ContainsAny(seller, "航空公司", "航空股份有限公司")))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Flight, 0.94);
            return;
        }

        if (ContainsAny(primary, TrainKeywords) || TrainTransportPattern.IsMatch(text))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Train, 0.94);
            return;
        }

        if (ContainsAny(primary, TaxiKeywords) || ContainsAny(seller, TaxiSellerKeywords) || TaxiTransportPattern.IsMatch(text))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Taxi, 0.94);
            return;
        }

        if (ContainsAny(primary, HotelKeywords))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Hotel, 0.94);
            return;
        }

        if (ContainsAny(primary, RentalCarKeywords))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.RentalCar, 0.92);
            return;
        }

        if (ContainsAny(primary, TollKeywords))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Toll, 0.94);
            return;
        }

        if (ContainsAny(primary, FuelKeywords))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Fuel, 0.94);
            return;
        }

        if (ContainsAny(primary, OtherTravelKeywords))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.OtherTravel, 0.88);
            return;
        }

        if (ContainsAny(primary, OtherKeywords))
        {
            Set(record, InvoiceCategory.Other, TravelSubCategory.None, 0.88);
            return;
        }

        if (ContainsAny(primary, ConsumableKeywords))
        {
            Set(record, InvoiceCategory.Consumable, TravelSubCategory.None, 0.86);
            return;
        }

        if (ContainsAny(seller, MealSellerKeywords) && !ContainsAny(primary, MealExclusions))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Meal, 0.78);
            return;
        }

        if (ContainsAny(seller, "酒店", "宾馆", "旅馆"))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Hotel, 0.76);
            return;
        }

        if (TryFolderFallback(record, immediateFolder)) return;

        Set(record, InvoiceCategory.Unknown, TravelSubCategory.None, 0);
    }

    private static bool TryFileNameFallback(InvoiceRecord record, string fileName)
    {
        if (ContainsAny(fileName, "说明", "订单", "支付", "报销单", "附件", "清单"))
            return false;

        if (ContainsAny(fileName, TollKeywords))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Toll, 0.55);
            return true;
        }
        if (ContainsAny(fileName, FuelKeywords))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Fuel, 0.55);
            return true;
        }
        if (!ContainsAny(fileName, AviationExclusions) && ContainsAny(fileName, FlightKeywords))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Flight, 0.55);
            return true;
        }
        if (ContainsAny(fileName, TrainKeywords) || ContainsAny(fileName, "高铁", "动车"))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Train, 0.55);
            return true;
        }

        if (ContainsAny(fileName, OtherTravelKeywords))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.OtherTravel, 0.55);
            return true;
        }

        if (!ContainsAny(fileName, "发票")) return false;
        if (ContainsAny(fileName, TaxiKeywords))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Taxi, 0.55);
            return true;
        }
        if (ContainsAny(fileName, HotelKeywords))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Hotel, 0.55);
            return true;
        }
        if (ContainsAny(fileName, MealKeywords))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Meal, 0.55);
            return true;
        }
        if (ContainsAny(fileName, PrintFeeKeywords))
        {
            Set(record, InvoiceCategory.PrintFee, TravelSubCategory.None, 0.55);
            return true;
        }
        if (ContainsAny(fileName, ShippingItemKeywords))
        {
            Set(record, InvoiceCategory.ShippingFee, TravelSubCategory.None, 0.55);
            return true;
        }
        return false;
    }

    private static bool TryFolderFallback(InvoiceRecord record, string folder)
    {
        if (ContainsAny(folder, "餐饮", "餐费", "伙食", "吃饭"))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Meal, 0.62);
            return true;
        }
        if (ContainsAny(folder, "机票", "飞机票"))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Flight, 0.62);
            return true;
        }
        if (ContainsAny(folder, "火车票", "高铁票", "铁路"))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Train, 0.62);
            return true;
        }
        if (ContainsAny(folder, "打车", "滴滴", "出租车", "网约车"))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Taxi, 0.62);
            return true;
        }
        if (ContainsAny(folder, "住宿", "酒店"))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Hotel, 0.62);
            return true;
        }
        if (ContainsAny(folder, "租车"))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.RentalCar, 0.62);
            return true;
        }
        if (ContainsAny(folder, "通行费", "过路费", "高速费"))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Toll, 0.62);
            return true;
        }
        if (ContainsAny(folder, "油费", "加油费", "燃油费"))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.Fuel, 0.62);
            return true;
        }

        var fullPath = record.OriginalFilePath;
        if (ContainsAny(fullPath, "打印费", "文印费"))
        {
            Set(record, InvoiceCategory.PrintFee, TravelSubCategory.None, 0.58);
            return true;
        }
        if (ContainsAny(fullPath, "邮寄费", "快递费"))
        {
            Set(record, InvoiceCategory.ShippingFee, TravelSubCategory.None, 0.58);
            return true;
        }
        if (ContainsAny(fullPath, "耗材"))
        {
            Set(record, InvoiceCategory.Consumable, TravelSubCategory.None, 0.58);
            return true;
        }
        if (ContainsAny(fullPath, "差旅"))
        {
            Set(record, InvoiceCategory.Travel, TravelSubCategory.OtherTravel, 0.55);
            return true;
        }
        return false;
    }

    private static void Set(InvoiceRecord record, InvoiceCategory category, TravelSubCategory subCategory, double confidence)
    {
        record.Category = category;
        record.SubCategory = subCategory;
        record.ClassificationConfidence = confidence;
    }

    private static bool ContainsAny(string? source, params string[] keywords) =>
        !string.IsNullOrWhiteSpace(source) &&
        keywords.Any(keyword => source.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}
