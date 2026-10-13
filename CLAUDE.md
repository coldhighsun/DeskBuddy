# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

DeskBuddy 是一个 Windows 桌面宠物（WPF，.NET 10，仅 Windows 可构建）。版本号由 MinVer 根据 git tag（`v1.2.3`）生成。

## 常用命令

```bash
dotnet build DeskBuddy.slnx
dotnet test DeskBuddy.slnx
dotnet test --project tests/DeskBuddy.Domain.Tests --filter "FullyQualifiedName~PetSettingsTests"   # 运行单个测试类/方法
dotnet run --project src/DeskBuddy.App
powershell -NoProfile -File tools/Build-Installer.ps1   # 发布并用 Inno Setup 7 打包安装程序（输出到 artifacts/installer）
powershell -NoProfile -File tools/GenerateRobotPet.ps1 # 重新生成 robot 宠物的 PNG 帧
powershell -NoProfile -File tools/GenerateCatPet.ps1   # 重新生成 cat 宠物的 PNG 帧（两个生成脚本共用 tools/PetDrawing.ps1）
powershell -NoProfile -File tools/GenerateAppIcon.ps1   # 重新生成应用/托盘图标 src/DeskBuddy.App/Assets/DeskBuddy.ico
```

- 构建产物统一在 `artifacts/`（`UseArtifactsOutput`），不要提交。
- CI（`.github/workflows/ci.yml`）在 windows-latest 上以 Release 配置构建并测试。
- 本机 `dotnet test` 带还原时可能因 NuGet 路径（`Y:\nuget\...`）失败，此时加 `--no-restore`。
- 改过 `.cs` 之后单独跑 `dotnet test --project tests/...`，创建真实窗口的测试（如 `UmbrellaMovementTests`、`ThemeServiceTests` 里的气泡用例）可能报“找不到程序集 `DeskBuddy, Version=1.0.0.0`”：WPF 标记编译在地址里写了 `;V1.0.0.0`，而 MinVer 生成的程序集版本是 `0.0.0.0`。先 `dotnet build DeskBuddy.slnx` 再测；还不行就删掉 `artifacts/obj/DeskBuddy.App` 后重新构建。

## 架构

四层，依赖方向 App → Application → Domain，Infrastructure 实现 Application 定义的接口：

- **Domain**：纯逻辑，不依赖任何基础设施。宠物行为状态机（`Behavior/PetBehaviorStateMachine`）、移动策略（`Movement/`，docked / roam / 自由落体 / 沿屏幕边缘爬行，通过 `IScreenEdges` 抽象屏幕）、场景选择（`Scenarios/`）、宠物模型（`Pets/`）、设置（`Settings/PetSettings`）、久坐提醒等。所有宠物状态是 `PetState` 枚举。
- **Application**：用例与服务。`PetBrain` 汇总传感器输入和用户交互，是 UI 询问“宠物现在该做什么”的唯一入口；`SettingsService` 等；`Abstractions/` 定义传感器与存储的接口（`ISettingsStore`、`IPetLoader` 等）。
- **Infrastructure**：接口的 Windows 实现——传感器（前台进程、进程列表、电源状态等）、JSON 存储（设置）、注册表开机自启、日志、`FolderPetLoader`/`FolderPetCatalog`。
- **App**：WPF 外壳。`App.xaml.cs` 用通用主机（Generic Host）组装全部依赖；`Rendering/` 负责宠物帧缓存与热切换（`PetSwitcher`）、面板预览图解码（`PetPreviewFrames`）；面板第一个标签是“宠物”（`PetsTabViewModel`/`PetCardViewModel`）：列出 `pets/` 下所有宠物，动画预览只轮播宠物自己定义的状态，能力标签由 `Domain/Pets/PetCapabilities` 从 manifest 推导，不写死任何宠物；`Windows/`、`ViewModels/`、`Interop/`（Win32 互操作）、`TrayIconService`、`SingleInstanceGuard`（单实例互斥）。

## 宠物

- 宠物是 `pets/<name>/` 下的 PNG 帧加 `manifest.json`（`name`、`frameSize`、`states`：每个状态的 `fps` 与 `frames`；`Idle.Left/Right/Up/Down` 为视线方向帧）。`pets/` 在构建时被复制到 App 输出目录。
- 默认宠物为 `robot`（`PetSettings.DefaultPetName`）。宠物找不到时回退到默认宠物。
- 宠物不必提供全部状态：缺失的状态通过 `Domain/Pets/PetStateFallback` 回退到相近状态。给 `PetState` 新增状态时，要同时考虑回退规则，并更新 robot 的 manifest 与 `GenerateRobotPet.ps1`。
- `tests/DeskBuddy.Infrastructure.Tests/Pets/ShippedPetsTests` 会校验随仓库发布的宠物。

## 主题（深色 / 浅色）

- 设置项 `PetSettings.Theme`（`AppTheme`：跟随系统 / 浅色 / 深色），在“外观”标签里切换，立即生效；Windows 高对比度主题优先于这个选择。
- `App/Presentation/ThemeService` 负责应用主题：把 `Themes/Colors.{Light,Dark,HighContrast}.xaml`（DeskBuddy 自绘的颜色）合并进应用资源，并让通过 `Register(...)` 注册的窗口 / 菜单使用 WPF 的 Fluent 主题（窗口设 `ThemeMode`，菜单合并 `Fluent.*.xaml`）。选哪套色板由纯函数 `ThemeResolver.Resolve` 决定。
- **不要设置应用级的 `Application.ThemeMode`**：宠物、雨伞、气泡是透明窗口，应用级 Fluent 主题会给它们画上背景。这几个窗口不要注册，也不要用 Fluent 样式；气泡按钮用 `Themes/BubbleButton.xaml` 里自己的样式。
- 自定义颜色资源的 key **不能与 Fluent 主题的 key 同名**（窗口资源优先于应用资源，会被悄悄盖住，曾导致宠物卡片边框消失）。新 key 加 `Bubble` / `PetCard` 这类前缀，`ThemeResourcesTests` 会检查冲突，三套色板的 key 也必须一致。
- 给控件写自己的 `Style` 时要加 `BasedOn`，或把样式放到外层元素上，否则会丢掉主题样式。
- `ThemeMode` 在 .NET 10 里仍是实验性 API，项目对 `WPF0001` 做了 `NoWarn`。

## 本地化

界面文字在 `src/DeskBuddy.App/Resources`：`Strings.resx` 是中文（默认），`Strings.en.resx` 是英文。新增文案两个文件都要加。

## 设计要求

- 【必须遵守】软件设计必须便于方便地扩展各种宠物：新增宠物只需在 `pets/` 下放入 PNG 帧和 `manifest.json`，不得修改代码；任何功能都不得写死某个具体宠物的名称、尺寸、帧数或朝向，一律通过宠物 manifest 与 `Pet` 模型获取，并为宠物缺少的动画/状态提供合理回退。
