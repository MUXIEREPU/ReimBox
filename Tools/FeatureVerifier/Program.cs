using ReimbursementAssistant.Configuration;
using ReimbursementAssistant.Models;
using ReimbursementAssistant.Services;

var failures = new List<string>();
var checkCount = 0;
var temporaryRoot = Path.Combine(Path.GetTempPath(), $"ReimBox-FeatureVerifier-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);

try
{
    var invoicePath = Path.Combine(temporaryRoot, "invoice.pdf");
    var attachmentPath = Path.Combine(temporaryRoot, "payment.png");
    File.WriteAllText(invoicePath, "test invoice");
    File.WriteAllText(attachmentPath, "test attachment");

    var record = new InvoiceRecord
    {
        OriginalFilePath = invoicePath,
        FileHash = "hash-1",
        DisplayIndex = 1,
        InvoiceNumber = "12345678901234567890",
        InvoiceDate = new DateTime(2026, 8, 12),
        SellerName = "测试科技有限公司",
        ItemDescription = "测试耗材",
        TotalAmount = 1280m,
        Category = InvoiceCategory.Consumable,
        ManualOverride = true
    };
    record.AttachedDocuments.Add(new AttachmentRecord { FilePath = invoicePath, AttachmentType = AttachmentType.Invoice });
    record.AttachedDocuments.Add(new AttachmentRecord { FilePath = attachmentPath, AttachmentType = AttachmentType.PaymentProof });

    var rules = new ReimbursementRuleEngine(new ReimbursementSettings());
    rules.Evaluate(record);
    Check(record.Status == RecordStatus.Complete, "规则引擎：1280 元耗材附支付凭证后应完整");

    var flight = new InvoiceRecord
    {
        OriginalFilePath = invoicePath,
        Category = InvoiceCategory.Travel,
        SubCategory = TravelSubCategory.Flight,
        TotalAmount = 860m
    };
    flight.AttachedDocuments.Add(new AttachmentRecord { FilePath = invoicePath, AttachmentType = AttachmentType.Invoice });
    rules.Evaluate(flight);
    Check(flight.MissingTypes().Contains(AttachmentType.OrderPage) && flight.MissingTypes().Contains(AttachmentType.PaymentProof), "规则引擎：飞机票应要求订单页面和支付记录截图");

    var threeDPrinting = new InvoiceRecord
    {
        OriginalFilePath = invoicePath,
        Category = InvoiceCategory.Consumable,
        TotalAmount = 100m,
        IsThreeDPrinting = true
    };
    threeDPrinting.AttachedDocuments.Add(new AttachmentRecord { FilePath = invoicePath, AttachmentType = AttachmentType.Invoice });
    rules.Evaluate(threeDPrinting);
    Check(threeDPrinting.MissingTypes().Contains(AttachmentType.ThreeDPrintDetails), "规则引擎：3D 打印耗材应要求打印明细");

    var classifier = new InvoiceClassificationService();
    const string invoiceEvidence = "电子发票（普通发票） 发票号码：26442000000000000001 开票日期：2026年08月09日 价税合计：100.00 ";
    var printFee = new InvoiceRecord { OriginalFilePath = invoicePath, ItemDescription = "文件打印及装订服务" };
    classifier.Classify(printFee, invoiceEvidence + "项目名称：文件打印及装订服务");
    Check(printFee.Category == InvoiceCategory.PrintFee, "自动分类：文件打印应归入打印费");
    var shippingFee = new InvoiceRecord { OriginalFilePath = invoicePath, ItemDescription = "国内快递服务" };
    classifier.Classify(shippingFee, invoiceEvidence + "销售方：顺丰速运有限公司 项目名称：国内快递服务");
    Check(shippingFee.Category == InvoiceCategory.ShippingFee, "自动分类：快递服务应归入邮寄费");
    var dispatchServiceFee = new InvoiceRecord { OriginalFilePath = invoicePath, ItemDescription = "收派服务费" };
    classifier.Classify(dispatchServiceFee, invoiceEvidence + "项目名称：*生产生活服务*收派服务费");
    Check(dispatchServiceFee.Category == InvoiceCategory.ShippingFee, "自动分类：收派服务费应归入邮寄费");
    var threeDPrintPriority = new InvoiceRecord { OriginalFilePath = invoicePath, ItemDescription = "3D打印材料快速成型服务" };
    classifier.Classify(threeDPrintPriority, invoiceEvidence + "项目名称：3D打印材料快速成型服务");
    Check(threeDPrintPriority.Category == InvoiceCategory.Consumable && threeDPrintPriority.IsThreeDPrinting, "自动分类：3D 打印优先归入耗材");

    var mealFee = new InvoiceRecord { OriginalFilePath = invoicePath, ItemDescription = "餐费", SellerName = "某酒店管理有限公司" };
    classifier.Classify(mealFee, invoiceEvidence + "*餐饮服务*餐费");
    Check(mealFee.Category == InvoiceCategory.Travel && mealFee.SubCategory == TravelSubCategory.Meal, "自动分类：酒店开具的餐费仍应归入伙食费");

    var groceryMeal = new InvoiceRecord { OriginalFilePath = invoicePath, ItemDescription = "无穷盐焗鸡蛋" };
    classifier.Classify(groceryMeal, invoiceEvidence + "*熟肉制品*无穷盐焗鸡蛋");
    Check(groceryMeal.Category == InvoiceCategory.Travel && groceryMeal.SubCategory == TravelSubCategory.Meal, "自动分类：超市食品发票应归入伙食费");

    var highSpeedTrain = new InvoiceRecord { OriginalFilePath = invoicePath, ItemDescription = "高铁宁波至杭州东" };
    classifier.Classify(highSpeedTrain, invoiceEvidence + "交通运输服务 高铁 宁波至杭州东");
    Check(highSpeedTrain.Category == InvoiceCategory.Travel && highSpeedTrain.SubCategory == TravelSubCategory.Train, "自动分类：高铁字段应归入火车票");

    var coachTicket = new InvoiceRecord { OriginalFilePath = Path.Combine(temporaryRoot, "10月24日-大巴-上海-舟山-150元.pdf"), ItemDescription = "大巴客票" };
    classifier.Classify(coachTicket, "扫描票据文字不完整");
    Check(coachTicket.Category == InvoiceCategory.Travel && coachTicket.SubCategory == TravelSubCategory.OtherTravel, "自动分类：文件名中的大巴客票应归入其他差旅");

    var taxiFee = new InvoiceRecord { OriginalFilePath = invoicePath, ItemDescription = "客运服务费", SellerName = "滴滴出行科技有限公司" };
    classifier.Classify(taxiFee, invoiceEvidence + "交通工具类型 出租车 到达地 某酒店 顺丰速运");
    Check(taxiFee.Category == InvoiceCategory.Travel && taxiFee.SubCategory == TravelSubCategory.Taxi, "自动分类：打车目的地中的酒店或顺丰不得抢占分类");

    var aviationEquipment = new InvoiceRecord { OriginalFilePath = invoicePath, ItemDescription = "航空航天设备飞控" };
    classifier.Classify(aviationEquipment, invoiceEvidence + "*航空航天设备*飞控");
    Check(aviationEquipment.Category == InvoiceCategory.Consumable, "自动分类：航空航天设备和飞控不得判为机票");

    var fuelFee = new InvoiceRecord { OriginalFilePath = invoicePath, ItemDescription = "92号车用汽油" };
    classifier.Classify(fuelFee, invoiceEvidence + "*汽油*92号车用汽油 成品油");
    Check(fuelFee.Category == InvoiceCategory.Travel && fuelFee.SubCategory == TravelSubCategory.Fuel, "自动分类：成品油和汽油应归入燃油费");

    var otherFee = new InvoiceRecord { OriginalFilePath = invoicePath, ItemDescription = "会议注册费" };
    classifier.Classify(otherFee, invoiceEvidence + "*会展服务*会议注册费");
    Check(otherFee.Category == InvoiceCategory.Other, "自动分类：会议费和注册费应归入其他");

    var nonInvoice = new InvoiceRecord { OriginalFilePath = Path.Combine(temporaryRoot, "机票订单说明.pdf"), ItemDescription = "机票订单说明" };
    classifier.Classify(nonInvoice, "这是订单说明，不是发票");
    Check(nonInvoice.Category == InvoiceCategory.Unknown, "自动分类：非发票说明文件应保持待确认");

    Check((int)TravelSubCategory.OtherTravel == 8 && (int)TravelSubCategory.Meal == 9, "兼容性：其他差旅枚举值保持为 8，伙食费使用新值 9");

    var extractTotal = typeof(InvoiceAnalysisService).GetMethod("ExtractTotalAmount", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    decimal? ExtractTotalForTest(string text) => (decimal?)extractTotal!.Invoke(null, [text]);
    var combinedInvoiceText = "发票号码：26327000001412750828 价税合计（大写）玖佰玖拾圆整（小写）¥990.00 " +
                              "发票号码：26327000001412750829 价税合计（大写）壹拾肆圆整（小写）¥14.00 " +
                              "发票号码：26957000000215376260 价税合计（大写）叁拾圆整（小写）￥30.00";
    Check(ExtractTotalForTest(combinedInvoiceText) == 1034m, "金额识别：同一 PDF 中多张发票应汇总价税合计");
    var aviationInvoiceText = "电子发票（航空运输电子客票行程单） 发票号码:26318018111056784697 票价 燃油附加费 增值税税率 增值税税额 民航发展基金 其他税费 合计 CNY 521.10 CNY 91.74 9% CNY 55.16 CNY 50.00 CNY 0.00 CNY 718.00 电子客票号码:0182353920397";
    Check(ExtractTotalForTest(aviationInvoiceText) == 718m, "金额识别：航空运输电子客票应读取 CNY 合计");
    var extractFileNameAmount = typeof(InvoiceAnalysisService).GetMethod("ExtractAmountFromFileName", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    var scannedAmount = (decimal?)extractFileNameAmount!.Invoke(null, [Path.Combine(temporaryRoot, "出租车发票-40元.pdf")]);
    Check(scannedAmount == 40m, "金额识别：扫描票面无法识别时可从明确的文件名金额安全兜底");
    var combinedValidation = new InvoiceRecord { OriginalFilePath = invoicePath, ExtractedText = combinedInvoiceText, Amount = 933.96m, Tax = 56.04m, TotalAmount = 1034m, Confidence = 1 };
    new InvoiceValidationService().Validate(combinedValidation);
    Check(combinedValidation.ValidationIssues.Any(issue => issue.Code == "MULTIPLE_INVOICES") && combinedValidation.ValidationIssues.All(issue => issue.Code != "AMOUNT_MISMATCH"), "字段校验：多发票 PDF 应提醒核对且不产生错误的金额勾稽异常");
    Check(InvoiceOptionCatalog.TravelSubCategories.Any(option => option.Value == TravelSubCategory.Meal), "选项：差旅二级分类应包含伙食费");

    var importPdfPath = Path.Combine(temporaryRoot, "import.pdf");
    var unsupportedPath = Path.Combine(temporaryRoot, "not-an-invoice.png");
    File.WriteAllText(importPdfPath, "pdf");
    File.WriteAllText(unsupportedPath, "image");
    var importedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var importResult = new FileImportService(new HashService()).Import([importPdfPath, unsupportedPath], importedHashes);
    Check(importResult.Records.Count == 1 && importResult.SkippedUnsupported == 1, "导入：只导入 PDF 并统计被跳过的其他格式");
    var duplicateImportResult = new FileImportService(new HashService()).Import([importPdfPath], importedHashes);
    Check(duplicateImportResult.Records.Count == 0 && duplicateImportResult.SkippedDuplicate == 1, "导入：相同文件 Hash 不重复导入");

    var recursiveRoot = Path.Combine(temporaryRoot, "recursive-import");
    var recursiveLevel1 = Path.Combine(recursiveRoot, "一级目录");
    var recursiveLevel2 = Path.Combine(recursiveLevel1, "二级目录");
    Directory.CreateDirectory(recursiveLevel2);
    var rootPdf = Path.Combine(recursiveRoot, "根目录发票.pdf");
    var level1Pdf = Path.Combine(recursiveLevel1, "一级目录发票.PDF");
    var level2Pdf = Path.Combine(recursiveLevel2, "二级目录发票.pdf");
    var nestedImage = Path.Combine(recursiveLevel2, "不是发票.png");
    File.WriteAllText(rootPdf, "root invoice");
    File.WriteAllText(level1Pdf, "level 1 invoice");
    File.WriteAllText(level2Pdf, "level 2 invoice");
    File.WriteAllText(nestedImage, "unsupported attachment");
    var recursiveImport = new FileImportService(new HashService()).Import(
        [recursiveRoot],
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    Check(
        recursiveImport.Records.Count == 3 &&
        recursiveImport.Records.Select(item => item.OriginalFilePath).ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals([rootPdf, level1Pdf, level2Pdf]),
        "导入文件夹：应递归导入所有层级中的 PDF");
    Check(recursiveImport.SkippedUnsupported == 1, "导入文件夹：子目录中的非 PDF 应跳过并计数");

    var projectPath = Path.Combine(temporaryRoot, "roundtrip.reimbox");
    var projectService = new ProjectService();
    var namingRule = InvoiceNamingRule.Default();
    projectService.Save(projectPath, [record], new ReimbursementInfo("测试用户", "20260001", "测试说明"), namingRule);
    var loaded = projectService.Load(projectPath);
    Check(loaded.Records.Count == 1, "项目往返：记录数量");
    Check(loaded.Records[0].TotalAmount == 1280m, "项目往返：金额");
    Check(loaded.Records[0].SupplementAttachments.Count() == 1, "项目往返：附件");
    Check(loaded.ReimbursementInfo.PersonName == "测试用户", "项目往返：报销人");

    var settings = new ReimbursementSettings
    {
        ConsumablePaymentThreshold = 2500m,
        RequireFlightPaymentProof = false,
        IgnoredUpdateTag = "v9.9.9",
        PaddleDevice = "cpu"
    };
    var settingsCopy = settings.Clone();
    Check(settingsCopy.ConsumablePaymentThreshold == 2500m && !settingsCopy.RequireFlightPaymentProof && settingsCopy.IgnoredUpdateTag == "v9.9.9" && settingsCopy.PaddleDevice == "cpu", "设置：复制时保留规则、更新和 Paddle 配置");
    Check(typeof(ReimbursementSettings).Assembly.GetManifestResourceNames().Contains("ReimBox.PaddleWorker.paddle_vl_worker.py"), "发布：PaddleWorker 已嵌入 ReimBox 程序集");

    var duplicate = new InvoiceRecord
    {
        OriginalFilePath = Path.Combine(temporaryRoot, "duplicate.pdf"),
        InvoiceNumber = record.InvoiceNumber,
        InvoiceDate = record.InvoiceDate,
        SellerName = record.SellerName,
        TotalAmount = record.TotalAmount
    };
    var duplicateCount = new DuplicateInvoiceService().Evaluate([record, duplicate]);
    Check(duplicateCount == 2 && record.IsPossibleDuplicate && duplicate.IsPossibleDuplicate, "重复检测：发票号码相同应标记两条");
    duplicate.DuplicateDismissed = true;
    new DuplicateInvoiceService().Evaluate([record, duplicate]);
    Check(!record.IsPossibleDuplicate && !duplicate.IsPossibleDuplicate, "重复检测：用户排除后不再提示");

    var exportBase = Path.Combine(temporaryRoot, "export");
    Directory.CreateDirectory(exportBase);
    var exportRoot = new ExportService().Export([record], exportBase, new ReimbursementInfo("测试用户", "20260001", "自动验证"));
    Check(File.Exists(Path.Combine(exportRoot, "README.txt")), "导出：生成 README.txt");
    Check(Directory.EnumerateDirectories(exportRoot, "带附件发票", SearchOption.AllDirectories).Any(), "导出：带附件发票进入专用目录");
    Check(Directory.EnumerateFiles(exportRoot, "*支付记录截图*", SearchOption.AllDirectories).Any(), "导出：发票与支付附件一同复制");

    var ignored = new InvoiceRecord { OriginalFilePath = invoicePath, UserIgnored = true, TotalAmount = 999m, Category = InvoiceCategory.Other };
    try
    {
        new ExportService().Export([ignored], temporaryRoot, new ReimbursementInfo("测试用户", "20260001", null));
        failures.Add("导出：全部忽略时应拒绝导出");
    }
    catch (InvalidOperationException)
    {
    }

    Console.WriteLine($"Feature checks: {checkCount - failures.Count}/{checkCount} passed");
    foreach (var failure in failures) Console.WriteLine("FAIL " + failure);
    return failures.Count == 0 ? 0 : 1;
}
finally
{
    try { Directory.Delete(temporaryRoot, true); } catch { }
}

void Check(bool condition, string message)
{
    checkCount++;
    if (!condition) failures.Add(message);
}
