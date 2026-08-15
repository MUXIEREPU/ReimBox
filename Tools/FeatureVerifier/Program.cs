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
    var printFee = new InvoiceRecord { OriginalFilePath = invoicePath };
    classifier.Classify(printFee, "项目名称：文件打印及装订服务");
    Check(printFee.Category == InvoiceCategory.PrintFee, "自动分类：文件打印应归入打印费");
    var shippingFee = new InvoiceRecord { OriginalFilePath = invoicePath };
    classifier.Classify(shippingFee, "销售方：顺丰速运有限公司 项目名称：国内快递服务");
    Check(shippingFee.Category == InvoiceCategory.ShippingFee, "自动分类：快递服务应归入邮寄费");
    var dispatchServiceFee = new InvoiceRecord { OriginalFilePath = invoicePath };
    classifier.Classify(dispatchServiceFee, "项目名称：*生产生活服务*收派服务费");
    Check(dispatchServiceFee.Category == InvoiceCategory.ShippingFee, "自动分类：收派服务费应归入邮寄费");
    var threeDPrintPriority = new InvoiceRecord { OriginalFilePath = invoicePath };
    classifier.Classify(threeDPrintPriority, "项目名称：3D打印材料快速成型服务");
    Check(threeDPrintPriority.Category == InvoiceCategory.Consumable && threeDPrintPriority.IsThreeDPrinting, "自动分类：3D 打印优先归入耗材");

    var importPdfPath = Path.Combine(temporaryRoot, "import.pdf");
    var unsupportedPath = Path.Combine(temporaryRoot, "not-an-invoice.png");
    File.WriteAllText(importPdfPath, "pdf");
    File.WriteAllText(unsupportedPath, "image");
    var importedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var importResult = new FileImportService(new HashService()).Import([importPdfPath, unsupportedPath], importedHashes);
    Check(importResult.Records.Count == 1 && importResult.SkippedUnsupported == 1, "导入：只导入 PDF 并统计被跳过的其他格式");
    var duplicateImportResult = new FileImportService(new HashService()).Import([importPdfPath], importedHashes);
    Check(duplicateImportResult.Records.Count == 0 && duplicateImportResult.SkippedDuplicate == 1, "导入：相同文件 Hash 不重复导入");

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
