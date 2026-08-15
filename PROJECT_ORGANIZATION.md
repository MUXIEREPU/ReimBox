# ReimBox 项目组织规范

本文档说明 ReimBox 当前代码仓库的目录组织形式、各类文件职责，以及后续开发时建议遵守的放置和命名规范。

## 一、总体原则

ReimBox 是一个 Windows 本地 WPF 桌面程序，核心原则是：

- 原始财务材料只读，不在原目录中移动、删除或覆盖用户文件。
- 界面、业务规则、文件处理、识别逻辑分层放置，不把所有逻辑堆在窗口代码里。
- 用户可人工纠错，自动识别结果不能直接替代用户判断。
- 新增功能优先复用已有 `Models`、`Services`、`ViewModels`、`Views` 分层。
- 临时文件、测试发票、构建产物和本地虚拟环境不应提交到 Git。

## 二、根目录文件

| 文件 | 作用 |
| --- | --- |
| `ReimbursementAssistant.slnx` | Visual Studio 解决方案文件，双击或用 Visual Studio 打开项目。 |
| `ReimbursementAssistant.csproj` | WPF 主项目配置，包含目标框架、应用名、版本号、图标、依赖包和资源配置。 |
| `App.xaml` / `App.xaml.cs` | WPF 应用入口和全局启动逻辑。 |
| `AssemblyInfo.cs` | 程序程序集相关配置。 |
| `GlobalUsings.cs` | 全局 using 声明，减少重复引用。 |
| `README.md` | 面向使用者和 GitHub 主页的项目介绍。 |
| `PROJECT_STRUCTURE.md` | 更详细的文件清单和模块说明。 |
| `PROJECT_ORGANIZATION.md` | 本文件，说明项目组织和开发规范。 |
| `build-release.ps1` | 发布打包脚本，用于生成 Release 版本。 |

## 三、核心目录说明

### `Models/`

存放纯数据模型和枚举，不应包含 UI 逻辑。

典型文件：

- `InvoiceRecord.cs`：发票记录，包含文件路径、金额、日期、分类、识别结果、附件状态等。
- `AttachmentRecord.cs`：附件记录，例如支付凭证、订单页面、3D 打印明细等。
- `Enums.cs`：一级分类、差旅二级分类、附件类型、状态等枚举。
- `BulkEditRequest.cs`：批量编辑窗口提交的操作请求。
- `InvoiceNamingRule.cs`：批量命名规则。
- `ReimbursementInfo.cs`：报销人姓名、学号或工号、补充说明。
- `ReimbursementProject.cs`：`.reimbox` 项目文件保存和加载的数据结构。
- `ValidationIssue.cs`：识别和规则校验产生的问题。

规范：

- 模型类命名使用名词，例如 `InvoiceRecord`、`AttachmentRecord`。
- 枚举值使用英文，界面显示中文通过转换或选项列表处理。
- 模型中避免直接引用 WPF 控件类型。

### `Services/`

存放业务服务和文件处理逻辑，是项目的主要业务层。

典型文件：

- `FileImportService.cs`：导入 PDF、扫描文件夹、过滤非 PDF 发票。
- `PdfTextService.cs`：PDF 原生文本提取。
- `PdfPreviewService.cs`：PDF 首页预览图生成。
- `PaddleOcrVlService.cs`：调用 Paddle OCR/VL 识别逻辑。
- `WindowsOcrService.cs`：Windows OCR 相关能力。
- `InvoiceAnalysisService.cs`：统一发票分析入口。
- `InvoiceItemExtractionService.cs`：提取商品、说明、商户等字段。
- `InvoiceClassificationService.cs`：发票分类判断。
- `ReimbursementRuleEngine.cs`：报销规则校验，例如飞机票附件、耗材金额阈值、3D 打印明细等。
- `InvoiceValidationService.cs`：字段完整性和异常校验。
- `ExportService.cs`：生成规范报销文件夹。
- `XlsxReportService.cs`：生成 Excel 报销清单。
- `ProjectService.cs`：`.reimbox` 项目保存、打开、自动保存、最近项目。
- `SettingsService.cs`：本地设置读写。
- `UpdateCheckService.cs`：检查 GitHub Release 新版本。
- `HashService.cs`：文件 Hash，用于重复检测。
- `DuplicateInvoiceService.cs`：重复发票检测。
- `CorrectionLearningService.cs`：用户分类修正学习。
- `OperationLogService.cs`：本地操作日志。
- `InvoiceNamingService.cs`：批量命名和导出命名规则。

规范：

- 业务规则放在 `Services`，不要写进 `Views`。
- 每个服务只负责一类事情，避免出现万能服务类。
- 文件操作必须默认复制，不直接修改用户原始发票。
- 新增规则优先放入 `ReimbursementRuleEngine.cs` 或配置类，不要写死在 UI 事件里。

### `ViewModels/`

存放界面状态和命令绑定，是 WPF MVVM 的连接层。

典型文件：

- `MainViewModel.cs`：主窗口主要状态、命令、列表数据、导入导出流程。
- `MainViewModel.Project.cs`：项目保存、打开、自动保存等项目功能拆分。
- `RelayCommand.cs`：WPF 命令封装。

规范：

- ViewModel 可以调用 Service，但不要直接写复杂文件解析算法。
- ViewModel 负责暴露给界面的属性和命令。
- 如果 `MainViewModel.cs` 继续变大，优先使用 `partial` 拆到独立文件，例如 `MainViewModel.Export.cs`、`MainViewModel.Search.cs`。

### `Views/`

存放 WPF 界面 XAML 和窗口后台代码。

典型文件：

- `MainWindow.xaml` / `.cs`：主界面、文件列表、右侧详情、PDF 预览、顶部按钮。
- `SettingsWindow.xaml` / `.cs`：设置窗口。
- `BulkEditWindow.xaml` / `.cs`：批量编辑窗口。
- `NamingRuleWindow.xaml` / `.cs`：批量命名规则窗口。
- `ReimbursementInfoWindow.xaml` / `.cs`：添加报销说明窗口。
- `ExportReviewWindow.xaml` / `.cs`：导出前检查窗口。
- `QuickReviewWindow.xaml` / `.cs`：快速复核窗口。
- `AttachmentPreviewWindow.xaml` / `.cs`：附件预览窗口。
- `UpdateAvailableWindow.xaml` / `.cs`：发现新版本提示窗口。

规范：

- XAML 负责布局和样式，`.xaml.cs` 只写轻量事件转发。
- 不要在 `.xaml.cs` 中写发票识别、规则判断、导出整理等核心业务逻辑。
- 弹窗样式尽量统一：浅灰蓝背景、白色圆角卡片、主按钮蓝色、危险操作橙色或红色提示。
- 控件名称使用清晰英文，例如 `CategoryBox`、`ApplyButton`、`DescriptionBox`。

### `Configuration/`

存放可配置规则。

- `ReimbursementSettings.cs`：耗材金额阈值、差旅附件要求、启动更新检查、Paddle 识别偏好等。

规范：

- 会变化的业务规则优先放进配置，不要硬编码。
- 默认值应符合当前报销规则，但允许用户在设置中修改。

### `Converters/`

存放 WPF 数据绑定转换器。

- `AttachmentThumbnailConverter.cs`：把附件文件转换为可显示的缩略图或图标。

规范：

- Converter 只做显示转换，不处理业务规则。

### `PaddleWorker/`

存放外部识别工作脚本。

- `paddle_vl_worker.py`：Paddle OCR/VL 识别工作进程。

规范：

- Python 脚本作为识别辅助，不直接依赖 UI。
- C# 调用 Python 时应考虑失败、超时、缺少环境等情况。

### `logo/`

存放程序图标资源。

- `icon.png`：窗口和界面使用的 PNG 图标。
- `app.ico`：Windows EXE 应用图标。

规范：

- 更新 logo 时需要同时检查 PNG 和 ICO。
- `app.ico` 应包含多尺寸图标，例如 16、24、32、48、64、128、256。
- 图标背景建议透明，避免在任务栏或窗口标题栏出现白底。

### `installer/`

存放安装包配置。

- `ReimBox.iss`：Inno Setup 安装脚本。

规范：

- 安装包输出目录不要提交。
- 修改应用名、版本号、图标后，应同步检查安装脚本。

### `Tools/`

存放开发和验证工具，不参与主程序编译。

典型目录：

- `FeatureVerifier/`：功能级验证。
- `UiVerifier/`：窗口加载和 UI 结构验证。
- `RecognitionVerifier/`：识别效果验证。

规范：

- 工具项目用于开发验证，不应被主项目引用。
- 新增验证工具应放在 `Tools/工具名/` 下。
- 主项目 `.csproj` 已排除 `Tools/**`，避免验证代码进入正式程序。

## 四、不应提交的目录

以下目录是本地构建、测试或临时输出，原则上不提交 Git：

| 目录 | 原因 |
| --- | --- |
| `bin/` | 编译输出。 |
| `obj/` | 编译中间文件。 |
| `release/` | 发布输出。 |
| `installer/output/` | 安装包输出。 |
| `.buildcheck/` | 本地构建检查输出。 |
| `.uicheck/` | UI 验证输出。 |
| `.venv_paddleocr/` | 本地 Python/Paddle 环境。 |
| `.venv_paddleocr_runtime/` | 本地 Python/Paddle 运行环境。 |
| `.paddle-vl-smoke-output/` | Paddle 测试输出。 |
| `test/` | 测试发票和财务材料。 |
| `tmp/` | 临时图片、临时转换文件和调试输出。 |

注意：如果某个临时目录还没有写入 `.gitignore`，提交前应先检查 `git status`，避免把临时文件误提交。

## 五、命名规范

### C# 文件

- 类名和文件名保持一致。
- 使用 PascalCase，例如 `InvoiceAnalysisService.cs`。
- 服务类以 `Service` 结尾。
- 规则引擎以 `RuleEngine` 结尾。
- 窗口文件成对出现，例如 `SettingsWindow.xaml` 和 `SettingsWindow.xaml.cs`。
- ViewModel 以 `ViewModel` 结尾。

### XAML 控件名称

- 需要在 `.xaml.cs` 中访问的控件才设置 `x:Name`。
- 控件名称应表达用途，例如 `CategoryBox`、`InvoiceGrid`、`ApplyButton`。
- 不建议使用 `Button1`、`TextBox2` 这类无意义名称。

### 服务方法

- 异步方法以 `Async` 结尾，例如 `CheckAsync`。
- 导入、导出、保存等可能失败的方法应考虑异常处理和用户提示。
- 文件路径参数命名使用 `path`、`filePath`、`directoryPath`，不要混用含义。

## 六、新增功能应该放哪里

| 新功能类型 | 推荐位置 |
| --- | --- |
| 新增发票字段 | `Models/InvoiceRecord.cs` |
| 新增附件类型 | `Models/Enums.cs`、`AttachmentRecord.cs`、相关 UI 下拉选项 |
| 新增报销规则 | `Services/ReimbursementRuleEngine.cs`、`Configuration/ReimbursementSettings.cs` |
| 新增识别策略 | `Services/InvoiceAnalysisService.cs` 或独立识别服务 |
| 新增分类关键词 | `Services/InvoiceClassificationService.cs` |
| 新增导出结构 | `Services/ExportService.cs` |
| 新增 Excel 清单字段 | `Services/XlsxReportService.cs` |
| 新增主界面交互 | `Views/MainWindow.xaml`、`MainWindow.xaml.cs`、`MainViewModel.cs` |
| 新增弹窗 | `Views/新窗口.xaml`、`Views/新窗口.xaml.cs` |
| 新增设置项 | `Configuration/ReimbursementSettings.cs`、`SettingsWindow.xaml`、`SettingsService.cs` |
| 新增验证脚本 | `Tools/` |

## 七、开发流程建议

1. 先确认需求属于 UI、模型、服务、规则、导出还是验证。
2. 优先修改对应层，不跨层堆代码。
3. 修改后运行主项目构建。
4. 涉及窗口样式时运行 UI 验证。
5. 涉及识别、分类、导出时运行对应验证工具。
6. 提交前检查 `git status`，确认没有测试发票、临时文件、构建产物被误提交。

## 八、当前架构关系

```text
Views
  ↓ 绑定/事件
ViewModels
  ↓ 调用
Services
  ↓ 读写
Models / Configuration / 本地文件
```

简单理解：

- `Views` 负责用户看见什么、点哪里。
- `ViewModels` 负责界面状态和命令。
- `Services` 负责真正做事。
- `Models` 负责保存数据结构。
- `Configuration` 负责可修改规则。

这个分层保持清楚，后续功能会更容易维护，也更不容易出现“界面一改，识别逻辑跟着坏”的问题。
