# ReimBox

ReimBox 是一款面向实验室、课题组和个人用户的 Windows 本地报销材料整理工具。它能够读取 PDF 发票、提取结构化字段、检查补充材料，并按规范生成报销文件夹。原始发票默认只读，导出时使用复制，不移动、不覆盖原文件。

## 主要功能

- PDF 原生文本解析，必要时可手动使用 PaddleOCR-VL 深度识别
- 发票金额、日期、号码、销售方、开票内容提取与规则校验
- 耗材、差旅、打印费、其他费用分类及人工修正
- 飞机票、火车票、3D 打印、千元以上耗材的附件完整性检查
- 项目保存、自动恢复、最近项目和本地自动保存
- 重复发票提示、待确认快速复核、批量修改与批量命名
- 附件预览、排序、删除和缺失文件提示
- PDF 预览、滚轮缩放、鼠标拖动和左右面板实时调整
- 导出前检查、README 明细、分类金额合计和规范目录生成
- 启动时检查 GitHub Release 新版本

## 下载与使用

1. 打开仓库右侧的 **Releases**。
2. 下载最新版本的 `ReimBox.exe`。
3. 双击运行，无需单独安装 .NET。
4. 将 PDF 发票拖入主界面，确认分类和材料状态后生成报销材料。

Windows 可能会对尚未进行代码签名的新软件显示安全提示。请确认文件来源是本仓库的 Release 页面后再运行。

## 识别策略

默认识别流程为：

```text
PDF 原生文本解析
  → 字段结构化提取
  → 关键词分类
  → 规则校验
  → 必要时人工纠正或 PaddleOCR-VL 深度识别
```

当前 Release EXE 的常规 PDF 识别不要求用户安装 Paddle。PaddleOCR-VL 属于可选的本地深度识别能力；如需在其他电脑上使用完整 Paddle 能力，还需要准备对应的 Python/Paddle 运行环境和模型。

## 数据与隐私

- 发票和附件只在本机处理，不上传服务器。
- 原始文件默认不修改、不移动、不删除。
- 设置、自动保存和学习记录保存在 `%LocalAppData%\ReimBox`。
- 测试发票和真实财务材料不纳入 Git 仓库。

## 开发环境

- Windows 10/11 x64
- Visual Studio 2022 或更高版本
- .NET 10 SDK
- C# / WPF

打开 `ReimbursementAssistant.slnx` 后，将 `ReimbursementAssistant` 设为启动项目即可运行。

### 构建

```powershell
dotnet build ReimbursementAssistant.slnx -c Release
```

### 自动验证

```powershell
dotnet run --project Tools/FeatureVerifier/FeatureVerifier.csproj -c Release
dotnet run --project Tools/UiVerifier/UiVerifier.csproj -c Release
dotnet run --project Tools/RecognitionVerifier/RecognitionVerifier.csproj -c Release -- "E:\Code\ReimBox\test\耗材"
```

### 生成 Release EXE

```powershell
.\build-release.ps1
```

输出文件：`release\win-x64\ReimBox.exe`。

如已安装 Inno Setup 6，还可以生成标准安装包：

```powershell
.\build-release.ps1 -Installer
```

安装包输出到 `installer\output`。

## 当前版本

v0.2.0
