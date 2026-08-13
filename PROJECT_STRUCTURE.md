# ReimBox 项目结构说明

本文档用于快速了解 ReimBox 的代码框架、各目录职责，以及主要文件在程序中的作用。

## 一、项目总体结构

```text
ReimBox/
├── App.xaml
├── App.xaml.cs
├── AssemblyInfo.cs
├── GlobalUsings.cs
├── ReimbursementAssistant.csproj
├── ReimbursementAssistant.slnx
├── PROJECT_STRUCTURE.md
├── Configuration/
├── Models/
├── Services/
├── ViewModels/
├── Views/
├── PaddleWorker/
├── Tools/
└── test/
```

其中：

- `Configuration/`：软件配置，例如报销阈值、PaddleOCR 本地运行配置。
- `Models/`：核心数据模型，例如发票记录、附件记录、枚举状态。
- `Services/`：业务服务层，例如文件导入、PDF 解析、发票字段提取、分类、规则校验、导出。
- `ViewModels/`：WPF 的 MVVM 视图模型，负责把界面操作连接到业务逻辑。
- `Views/`：WPF 界面文件。
- `PaddleWorker/`：C# 调用 PaddleOCR-VL 的本地 Python 桥接脚本。
- `Tools/`：辅助开发/测试工具，目前包含识别准确率验证程序。
- `test/`：本地测试样例，不属于正式程序逻辑。

`bin/`、`obj/`、`.venv_paddleocr*`、`.paddle-*.log` 等是编译产物、本地 Python 环境或调试日志，不是主要源码。

## 二、根目录文件

### `ReimbursementAssistant.slnx`

Visual Studio 解决方案文件。用 Visual Studio 打开这个文件即可加载主程序和测试工具。

### `ReimbursementAssistant.csproj`

主 WPF 项目文件。这里定义了：

- 输出类型为 Windows 桌面程序；
- 目标框架为 `.NET 10 + WPF`；
- 引用 `PdfPig` 用于 PDF 原生文本解析；
- 排除 `Tools/` 下的测试工具源码，避免被主程序重复编译；
- 将 `PaddleWorker/paddle_vl_worker.py` 复制到输出目录，方便运行时调用。

### `App.xaml`

WPF 应用入口配置。定义程序启动时打开哪个窗口。

### `App.xaml.cs`

WPF 应用的后台代码，目前主要保留默认应用生命周期逻辑。

### `AssemblyInfo.cs`

WPF 程序的程序集属性配置。

### `GlobalUsings.cs`

全局 using 声明。用于减少每个 C# 文件重复写常用命名空间。

## 三、Configuration

```text
Configuration/
└── ReimbursementSettings.cs
```

### `ReimbursementSettings.cs`

报销规则和本地识别引擎配置。

当前主要配置包括：

- `ConsumablePaymentThreshold = 1000`：耗材超过 1000 元需要支付凭证。阈值集中放在配置里，后续规则变化时不需要改 UI 代码。
- `EnablePaddleOcrVl`：是否启用 PaddleOCR-VL 能力。
- `EnablePaddleAutoFallback`：导入时是否自动 fallback 到 Paddle，当前默认关闭，避免 CPU 模式太慢。
- `PaddlePythonExecutable`：本地 Paddle Python 环境路径。
- `PaddleWorkerScript`：Python worker 脚本路径。
- `PaddleDevice`：Paddle 运行设备，默认 `auto`。
- `PaddleTimeoutSeconds`：Paddle 识别超时时间。

## 四、Models

```text
Models/
├── AttachmentRecord.cs
├── Enums.cs
├── InvoiceRecord.cs
└── ValidationIssue.cs
```

### `Enums.cs`

定义核心枚举：

- `InvoiceCategory`：一级分类，包括待确认、耗材、差旅、打印费、其他。界面下拉框不显示“待确认”，未识别好的材料只在材料状态中显示待确认。
- `TravelSubCategory`：差旅二级分类，包括飞机、火车、住宿、出租车、租车、路桥费、燃油费等。
- `AttachmentType`：附件类型，包括发票、支付记录截图、订单页面、3D 打印明细、其他材料。
- `RecordStatus`：材料状态，包括分析中、完整、缺材料、待确认、识别失败、已忽略。
- `RecognitionSource`：识别来源，包括 PDF 原生解析、PaddleOCR-VL、Windows OCR。
- `ValidationSeverity`：字段校验级别，包括警告和错误。

### `InvoiceRecord.cs`

发票主数据模型，是整个程序最核心的对象。

主要保存：

- 原始文件路径和文件名；
- 发票号码、日期、销售方、购买方；
- 商品说明、金额、税额、价税合计；
- 一级分类、二级分类、识别置信度；
- 所需附件、已关联附件、缺失附件；
- 当前状态、人工修改标记、识别摘要、字段校验结果。

同时还提供一些界面显示用属性，例如：

- `CategoryDisplay`
- `SubCategoryDisplay`
- `StatusDisplay`
- `RequirementDisplays`
- `ValidationDisplays`

### `AttachmentRecord.cs`

附件数据模型。

用于表示支付截图、订单截图或其他补充材料。主要字段包括：

- 附件原始路径；
- 附件文件名；
- 附件类型；
- 关联的发票 ID；
- 可选的金额、日期和匹配置信度。

### `ValidationIssue.cs`

字段校验结果模型。

例如：

- 未识别价税合计；
- 未识别开票日期；
- 金额 + 税额与价税合计不一致；
- 识别置信度较低。

## 五、Services

```text
Services/
├── ExportService.cs
├── FileImportService.cs
├── HashService.cs
├── InvoiceAnalysisService.cs
├── InvoiceClassificationService.cs
├── InvoiceItemExtractionService.cs
├── InvoiceNamingService.cs
├── InvoiceValidationService.cs
├── OperationLogService.cs
├── PaddleOcrVlService.cs
├── PdfPreviewService.cs
├── PdfTextService.cs
├── ReimbursementRuleEngine.cs
├── WindowsOcrService.cs
└── XlsxReportService.cs
```

### `FileImportService.cs`

负责导入文件和文件夹。

主要能力：

- 支持递归扫描文件夹；
- 发票入口只接受 PDF；
- 通过文件 Hash 跳过重复文件；
- 对非 PDF 和重复文件返回跳过数量，供界面提示用户；
- 为每个新 PDF 创建 `InvoiceRecord`。

注意：右侧“补充材料”入口可以添加任意格式附件，和发票入口的 PDF 限制是分开的。

### `HashService.cs`

计算文件 Hash。

用于判断同一个文件是否已经导入，避免重复报销材料进入列表。

### `PdfTextService.cs`

PDF 原生文本读取服务。

当前使用 `PdfPig` 读取 PDF 内嵌文字，并按页面上的视觉顺序重组文本行。对电子发票非常重要，因为很多 PDF 的原始文本顺序和肉眼看到的顺序不一致。

### `InvoiceAnalysisService.cs`

发票识别和字段结构化提取服务。

当前流程：

1. 优先使用 `PdfTextService` 做 PDF 原生解析；
2. 如果文本太少，可按需调用 PaddleOCR-VL 深度识别；
3. 如果 Paddle 不可用，再尝试 Windows 本地 OCR；
4. 从文本中提取发票号码、开票日期、销售方、商品名称、金额、税额、价税合计；
5. 调用分类服务自动判断耗材/差旅；
6. 调用字段校验服务生成校验提示。

### `InvoiceItemExtractionService.cs`

商品名称/说明提取服务。

它不再只用单条正则抓第一段文字，而是优先解析发票明细表：

- 定位 `项目名称/规格型号/单位/数量/单价/金额/税率/税额` 明细区域；
- 将同一个商品被 PDF 拆开的多行文字合并；
- 去掉规格型号、单位、数量、单价、金额、税率、税额等表格列；
- 清理 `【活动价】`、`【优惠价】`、`【狂欢价】` 等营销前缀；
- 清理断行造成的型号残留，例如 `M3*9*1`、`TMC2225/2208/2209`；
- 输出更适合界面显示和导出命名的商品关键词。

### `InvoiceClassificationService.cs`

发票分类服务。

主要基于关键词规则判断：

- 航空、机票、航班：差旅/飞机；
- 铁路、火车票、中国铁路：差旅/火车；
- 酒店、住宿：差旅/住宿；
- 出租车、网约车、滴滴：差旅/出租车；
- 材料、配件、电机、螺丝、电子元件：耗材。

如果路径里包含 `耗材` 或 `差旅`，也会作为低置信度辅助判断。

### `InvoiceValidationService.cs`

字段校验服务。

检查发票识别结果是否可靠，例如：

- 价税合计是否存在；
- 日期、销售方、发票号码是否识别出来；
- 金额 + 税额是否等于价税合计；
- 字段完整度是否太低。

### `InvoiceNamingService.cs`

发票批量命名服务。

用于根据用户选择的命名字段生成文件名，当前支持：

- 编号；
- 费用项；
- 发票开票内容/商品说明；
- 总金额；
- 开票时间；
- 销售方；
- 发票号码。

它同时负责：

- 清理 Windows 文件名非法字符；
- 避免重复命名；
- 在用户明确确认后，直接重命名原始 PDF 文件；
- 为导出的报销文件夹生成规范 PDF 文件名。

### `ReimbursementRuleEngine.cs`

报销材料规则引擎。

当前已实现：

- 差旅中的火车票必须有订单页面；
- 差旅中的飞机票必须有订单页面和支付记录截图；
- 耗材超过 `ConsumablePaymentThreshold` 必须有支付凭证；
- 识别到 3D 打印相关耗材时，要求上传 3D 打印明细；
- 根据字段错误、分类状态、附件缺失情况更新材料状态。

### `ExportService.cs`

导出服务。

负责复制原始材料到新的报销文件夹，不会移动、删除或修改原文件。

当前导出规则：

- 输出根目录使用 `yyyyMMdd_姓名_总金额元`，如果已存在会自动追加 `_01`、`_02`；
- 按分类生成 `01_耗材_金额元`、`02_差旅_金额元`、`03_打印费_金额元`、`04_其他_金额元`、`05_待确认_金额元`；
- 普通无附件发票直接平铺在分类目录下；
- 所有带补充附件的发票会进入分类目录下的 `带附件发票` 文件夹，再按单张发票创建子文件夹，并把发票和附件放在一起；
- 发票文件名可以使用“批量命名”中设置的规则。默认只影响导出文件夹；如果用户明确选择，也可以直接重命名原始 PDF；
- 根目录生成 `README.txt`，写入报销人、学号/工号、说明、总金额和逐项明细；
- 根目录生成 `报销清单.xlsx`，便于财务或用户二次核对。

### `PdfPreviewService.cs`

PDF 预览服务。

当前使用 Windows 原生 PDF 渲染能力生成页面图片，供右侧 `PDF预览` 面板显示。支持：

- 获取 PDF 页数；
- 渲染指定页；
- 配合界面实现上一页/下一页、滚轮缩放、左键拖动画布。

### `XlsxReportService.cs`

Excel 报销清单生成服务。

不依赖 Excel 客户端，直接生成 `.xlsx` 文件。当前清单字段包括：

- 序号；
- 分类；
- 商品/说明；
- 销售方；
- 发票号码；
- 开票日期；
- 金额；
- 材料状态；
- 缺失材料；
- 附件数；
- 原文件名。

### `PaddleOcrVlService.cs`

C# 到 PaddleOCR-VL 的桥接服务。

职责：

- 启动本地 Python worker；
- 传入待识别文件路径；
- 等待 PaddleOCR-VL 输出 JSON；
- 从 JSON 中收集可用文字；
- 处理超时、环境缺失、识别失败等情况。

### `WindowsOcrService.cs`

Windows 本地 OCR fallback。

当 PDF 原生文本不足且 Paddle 不可用时，尝试使用 Windows 系统 OCR 能力读取图片文字。

### `OperationLogService.cs`

本地操作日志服务。

记录导入、识别、添加附件、导出等操作。日志保存在本机，不上传服务器。

## 六、ViewModels

```text
ViewModels/
├── MainViewModel.cs
└── RelayCommand.cs
```

### `MainViewModel.cs`

主窗口视图模型，是界面和业务服务之间的协调层。

主要负责：

- 文件/文件夹导入；
- 后台异步分析；
- 选中当前发票；
- 手动修改分类和金额；
- 附件拖拽关联；
- 重新分析当前发票；
- 手动触发 Paddle 深度识别；
- 重新检查材料规则；
- 导出报销材料；
- 刷新顶部统计和进度提示。

### `RelayCommand.cs`

WPF MVVM 命令封装。

让按钮可以绑定到 ViewModel 里的方法，例如导入、导出、重新分析。

## 七、Views

```text
Views/
├── ExportReviewWindow.xaml
├── ExportReviewWindow.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── NamingRuleWindow.xaml
├── NamingRuleWindow.xaml.cs
├── ReimbursementInfoWindow.xaml
└── ReimbursementInfoWindow.xaml.cs
```

### `MainWindow.xaml`

主界面布局文件。

当前界面包括：

- 顶部标题和导入/导出按钮；
- 拖拽提示区和识别进度；
- 左侧发票列表；
- 右侧发票详情；
- 右侧支持在 `发票详情` 和 `PDF预览` 两个模式之间切换；
- 分类和二级分类下拉框；
- 金额、商品说明可编辑输入框；
- 所需材料、字段校验、已关联附件列表；
- 附件拖拽区域；
- 快速重新分析、Paddle 深度识别、重新检查材料按钮。

### `MainWindow.xaml.cs`

主窗口后台事件处理。

主要负责 WPF 拖拽事件：

- 主窗口拖入文件/文件夹时调用导入；
- 右侧附件区域拖入文件/文件夹时调用附件关联；
- 阻止附件拖拽事件冒泡到主窗口，避免附件被误当作新发票导入。
- PDF 预览区的滚轮缩放、左键拖动；
- Delete 键删除当前选中发票；
- 附件打开、删除、类型修改后重新规则检查。
- 顶部“批量命名”按钮打开命名规则窗口。

### `ReimbursementInfoWindow.xaml` / `ReimbursementInfoWindow.xaml.cs`

“添加说明”弹窗。

用户导出前必须填写：

- 报销人姓名；
- 学号或工号。

可选填写补充说明。这些内容会写入导出根目录的 `README.txt`。

### `ExportReviewWindow.xaml` / `ExportReviewWindow.xaml.cs`

导出前检查窗口。

显示：

- 总笔数；
- 完整数量；
- 待处理数量；
- 总金额；
- 缺材料、待确认、识别失败等需要注意的项目。

如果仍有问题，用户可以返回修改，也可以选择仍然导出。

### `NamingRuleWindow.xaml` / `NamingRuleWindow.xaml.cs`

批量命名规则窗口。

用户可以勾选字段组合，例如：

```text
编号 + 费用项 + 发票开票内容 + 总金额 + 开票时间
```

窗口会实时预览原文件名和新文件名。用户可以选择：

- 只修改导出文件夹中的发票命名（推荐）；
- 直接重命名原始 PDF 文件。

如果选择直接重命名原始 PDF，主窗口会再次弹窗确认，避免误改原始发票文件夹。

## 八、PaddleWorker

```text
PaddleWorker/
└── paddle_vl_worker.py
```

### `paddle_vl_worker.py`

本地 PaddleOCR-VL Python worker。

C# 程序不会直接在 .NET 里加载 Paddle，而是启动这个脚本。脚本负责：

- 初始化 `PaddleOCRVL`；
- 使用 `pipeline_version = v1.6`；
- 对输入 PDF/图片执行版面和文字识别；
- 将每页识别结果写成 JSON；
- 供 `PaddleOcrVlService` 回读文本。

当前由于本机 Paddle 是 CPU 版本，深度识别可能比较慢，所以程序默认不会在批量导入时自动调用它，而是通过右侧 `Paddle 深度识别` 按钮手动触发。

## 九、Tools

```text
Tools/
└── RecognitionVerifier/
    ├── RecognitionVerifier.csproj
    └── Program.cs
```

### `RecognitionVerifier.csproj`

识别验证工具项目。

它引用主程序项目，用于在命令行里批量测试发票识别结果。

### `Program.cs`

测试工具入口。

当前用于读取 `test/耗材` 下的 PDF，并统计：

- 金额是否匹配文件名；
- 是否分类为耗材；
- 发票号码是否识别；
- 开票日期是否识别；
- 销售方是否识别；
- 金额 + 税额校验是否通过；
- 是否存在严重校验错误。

还支持调试单张发票：

```powershell
dotnet run --project Tools\RecognitionVerifier\RecognitionVerifier.csproj --no-build -- "E:\Code\ReimBox\test\耗材" --dump 27.78.pdf
```

它会打印该发票的商品提取结果、价税合计和 PDF 原生解析文本，方便定位某张发票识别不准的原因。

## 十、test

```text
test/
└── 耗材/
```

本地测试数据目录。

里面包含一批耗材发票 PDF 和测试过程中导出的报销材料目录。这个目录用于验证识别准确率和导出结构，不属于正式程序代码。

## 十一、当前业务流程

```text
拖入文件/文件夹
  ↓
递归扫描 PDF
  ↓
计算 Hash，跳过重复文件；非 PDF 发票入口不导入并提示用户
  ↓
创建发票记录
  ↓
PDF 原生解析 / 可选 Paddle 深度识别
  ↓
结构化提取字段
  ↓
关键词分类
  ↓
规则引擎判断所需附件
  ↓
用户拖入支付凭证/订单页面
  ↓
重新检查材料完整性
  ↓
导出前检查窗口
  ↓
一键复制导出报销材料
```

## 十二、当前重点规则

### 规则 1：火车票

如果：

```text
Category == Travel
SubCategory == Train
```

则必须有：

```text
OrderPage 订单页面
```

### 规则 2：飞机票

如果：

```text
Category == Travel
SubCategory == Flight
```

则必须有：

```text
OrderPage 订单页面
PaymentProof 支付记录截图
```

### 规则 3：耗材超过 1000 元

如果：

```text
Category == Consumable
TotalAmount > 1000
```

则必须有：

```text
PaymentProof 支付凭证
```

### 规则 4：3D 打印耗材

如果识别到 3D 打印相关发票，并且分类为耗材，则必须有：

```text
ThreeDPrintDetails 3D打印明细
```

导出时，不再按“超过1000元”单独建文件夹；现在统一按“是否带附件”归档。所有带附件的发票都会放入对应分类目录下的 `带附件发票` 文件夹，再按单张发票建子文件夹，把发票和附件放在一起。

## 十三、v0.2 新增模块

### 项目保存与恢复

- `Models/ReimbursementProject.cs`：定义 `.reimbox` 项目的持久化数据结构。
- `Services/ProjectService.cs`：负责项目读写、自动保存和最近项目列表。
- `ViewModels/MainViewModel.Project.cs`：连接项目命令、启动恢复和主界面状态。

### 检查与人工修正

- `Services/DuplicateInvoiceService.cs`：根据发票号码及关键字段提示疑似重复发票。
- `Services/CorrectionLearningService.cs`：在本机记录用户对销售方分类的人工修正。
- `Models/BulkEditRequest.cs`：描述批量修改操作。
- `Views/BulkEditWindow.xaml(.cs)`：批量分类、忽略、重新识别和移除记录。
- `Views/QuickReviewWindow.xaml(.cs)`：逐条复核待确认或识别失败的记录。

### 附件与设置

- `Converters/AttachmentThumbnailConverter.cs`：为图片附件生成不锁定原文件的缩略图。
- `Views/AttachmentPreviewWindow.xaml(.cs)`：预览 PDF、图片或打开其他附件。
- `Services/SettingsService.cs`：将规则选项保存到本机。
- `Views/SettingsWindow.xaml(.cs)`：编辑附件规则、导出限制和更新检查设置。

### 更新、发布与验证

- `Services/UpdateCheckService.cs`：检查 GitHub Release 最新版本。
- `Views/UpdateAvailableWindow.xaml(.cs)`：展示版本信息、更新说明和忽略选项。
- `build-release.ps1`：生成 Windows x64 自包含单文件 EXE，并检查发布命令是否成功。
- `installer/ReimBox.iss`：可选的 Inno Setup 安装包定义。
- `Tools/FeatureVerifier/`：验证项目读写、规则、重复检测和导出保护。
- `Tools/UiVerifier/`：逐一加载主要窗口，捕获 XAML 初始化错误。

### 本地数据目录

程序运行过程中产生的用户数据均位于：

```text
%LocalAppData%\ReimBox\
├── autosave.reimbox
├── settings.json
├── recent-projects.json
├── learned-corrections.json
└── crash.log
```

这些文件不会写入源码目录，也不包含在 Release 中。
