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
powershell -NoProfile -File tools/GenerateDogPet.ps1   # 重新生成 dog / rabbit / penguin / ghost 的 PNG 帧（对应 GenerateRabbitPet / GeneratePenguinPet / GenerateGhostPet；这四个共用 tools/PetAnimations.ps1 里的动画模板，各脚本只画姿势）
powershell -NoProfile -File tools/GenerateAppIcon.ps1   # 重新生成应用/托盘图标 src/DeskBuddy.App/Assets/DeskBuddy.ico
```

- 构建产物统一在 `artifacts/`（`UseArtifactsOutput`），不要提交。
- CI（`.github/workflows/ci.yml`）在 windows-latest 上以 Release 配置构建并测试。
- 本机 `dotnet test` 带还原时可能因 NuGet 路径（`Y:\nuget\...`）失败，此时加 `--no-restore`。
- 改过 `.cs` 之后单独跑 `dotnet test --project tests/...`，创建真实窗口的测试（如 `UmbrellaMovementTests`、`ThemeServiceTests` 里的气泡用例）可能报“找不到程序集 `DeskBuddy, Version=1.0.0.0`”：WPF 标记编译在地址里写了 `;V1.0.0.0`，而 MinVer 生成的程序集版本是 `0.0.0.0`。先 `dotnet build DeskBuddy.slnx` 再测；还不行就删掉 `artifacts/obj/DeskBuddy.App` 后重新构建。

## 架构

四层，依赖方向 App → Application → Domain，Infrastructure 实现 Application 定义的接口：

- **Domain**：纯逻辑，不依赖任何基础设施。宠物行为状态机（`Behavior/PetBehaviorStateMachine`）、移动策略（`Movement/`，沿任务栏行走 / 自由落体 / 沿屏幕边缘爬行 / 跳跃，通过 `IScreenEdges` 抽象屏幕）、场景选择（`Scenarios/`）、宠物模型（`Pets/`）、设置（`Settings/PetSettings`）、久坐提醒等。所有宠物状态是 `PetState` 枚举。
- **Application**：用例与服务。`PetBrain` 汇总传感器输入和用户交互，是 UI 询问“宠物现在该做什么”的唯一入口；按目录分组：`Settings/`（`SettingsService`、`SettingsApplier`）、`Scenarios/`（`DevScenarios`、`SystemScenarios`）、`Sensors/`（带缓存/节流的传感器包装）、`Hotkeys/`（`BossKeyController`）；`Abstractions/` 定义传感器与存储的接口（`ISettingsStore`、`IPetLoader` 等）。
- **Infrastructure**：接口的 Windows 实现——传感器（前台进程、进程列表、电源状态等）、JSON 存储（设置）、注册表开机自启、日志、`FolderPetLoader`/`FolderPetCatalog`。
- **App**：WPF 外壳。`App.xaml.cs` 用通用主机（Generic Host）组装全部依赖；`Rendering/` 负责宠物帧缓存与热切换（`PetSwitcher`）、面板预览图解码（`PetPreviewFrames`）；面板第一个标签是“宠物”（`PetsTabViewModel`/`PetCardViewModel`）：列出 `pets/` 下所有宠物，动画预览只轮播宠物自己定义的状态（每次先显示 Idle，其余按随机顺序，每轮不重复、不连着重复；`PetShowcase.RandomOrder`），能力标签由 `Domain/Pets/PetCapabilities` 从 manifest 推导，不写死任何宠物；`Windows/`、`ViewModels/`、`Interop/`（Win32 互操作）、`TrayIconService`、`SingleInstanceGuard`（单实例互斥）。

- **长驻状态会“溜达”**：Coding 和长驻的情境状态（`PetStateExtensions.CanStroll`：Coding / Building / Reading / NightCoding / Meeting / Chatting；Tired、LowBattery 与瞬时状态不算）在状态机里会一直钉住，宠物不动。为避免几小时一动不动，`PetBehaviorStateMachine` 在这些状态里每隔 `StrollIntervalMin..Max`（默认 10–20 秒）临时进入普通 `Walk`（时长同普通散步，期间也会 Hop），走完再回到原状态，窗口层无需任何改动。开关是设置项 `PetSettings.StrollWhileBusy`（“行为”标签，默认开），经 `SettingsApplier` 写入 `PetBehaviorOptions.StrollEnabled`。新增长驻情境状态时，要同时决定它是否加进 `CanStroll`。

## 宠物

- 宠物是 `pets/<name>/` 下的一张精灵图集 `atlas.png`（所有帧按相同大小的格子排成网格）加 `manifest.json`，只有这一种写法，不再支持“一帧一个 PNG”（`name`、可选的 `names`（各语言的显示名，键为语言如 `zh`、`en` 或区域如 `zh-CN`，面板和托盘按界面语言取，缺失回退到 `name`）、`frameSize`、必填的 `atlas`（`image` 图集文件名、`cell` 单格像素大小 `[宽,高]`、可选 `columns` 每行格数，缺省取图集宽度能放下的列数）、`states`：每个状态的 `fps` 与 `frames`，`frames` 是图集格子编号，按行从左上角开始数；`Idle.Left/Right/Up/Down` 为视线方向帧）。`pets/` 在构建时被复制到 App 输出目录。
- 帧在 Domain 里仍是字符串 id：`Domain/Pets/FrameId` 把“图集路径 + 格子矩形”编码成 `路径#x,y,w,h`（`FolderPetLoader` 生成，并用 PNG 头校验图集尺寸和格子编号）；`PetFrameCache` / `PetPreviewFrames` 共用 `Rendering/FrameImages`：每个图集只解码一次，再按矩形切出独立复制的帧（不拖着整张图集）；面板预览用的帧由 `PetPreviewFrames.PreloadAsync` 在专用 STA 线程提前切好（`PetsTabViewModel`、`InstalledPets` 加载宠物时调用），界面线程不解码图集。加载器还会校验图集格子与 `frameSize` 比例一致。托盘图标用哪张图由纯函数 `Presentation/TrayIconModel.Choose` 决定，托盘图标等宠物列表读完后才创建，避免先显示默认图标再变。
- 宠物文件夹里可放 `icon.png` 作为托盘图标（只画头部，尽量撑满；`InstalledPets.IconFileName`）。没有它时托盘图标回退为 Idle 第一帧的上部（`AppIcon.FocusArea`）。随仓库的宠物由各自的生成脚本一并生成它（`PetDrawing.ps1` 的 `Save-PetIcon`，头部范围写在脚本里）。生成脚本先画出单帧 PNG，最后由 `tools/PackPetAtlas.ps1` 按内容去重拼成 `atlas.png`、把 manifest 的帧名换成格子编号并删掉单帧文件，所以每只宠物目录里只有 `manifest.json`、`atlas.png`、`icon.png`。
- 默认宠物为 `robot`（`PetSettings.DefaultPetName`）。宠物找不到时回退到默认宠物。
- 宠物不必提供全部状态：缺失的状态通过 `Domain/Pets/PetStateFallback` 回退到相近状态。给 `PetState` 新增状态时，要同时考虑回退规则，并更新 robot 的 manifest 与 `GenerateRobotPet.ps1`。例外是可选状态：`Hop`（步行中随机跳一下）只有 manifest 里定义了它的宠物才会跳，没有定义就根本不会进入该状态（时长取自宠物自己的 `Hop` 动画，由窗口在播完时调用 `PetBrain.OnHopEnded`），所以 robot 故意没有 `Hop`，`PetCapabilities` 也不把它算进未定义宠物的状态总数。
- `tests/DeskBuddy.Infrastructure.Tests/Pets/ShippedPetsTests` 会校验随仓库发布的宠物。

## 主题（深色 / 浅色）

- 设置项 `PetSettings.Theme`（`AppTheme`：跟随系统 / 浅色 / 深色），在“外观”标签里切换，立即生效；Windows 高对比度主题优先于这个选择。
- `App/Presentation/ThemeService` 负责应用主题：把 `Themes/Colors.{Light,Dark,HighContrast}.xaml`（DeskBuddy 自绘的颜色）合并进应用资源，并让通过 `Register(...)` 注册的窗口 / 菜单使用 WPF 的 Fluent 主题（窗口设 `ThemeMode`，菜单合并 `Fluent.*.xaml`）。选哪套色板由纯函数 `ThemeResolver.Resolve` 决定。
- **不要设置应用级的 `Application.ThemeMode`**：宠物、雨伞、气泡是透明窗口，应用级 Fluent 主题会给它们画上背景。这几个窗口不要注册，也不要用 Fluent 样式；气泡按钮用 `Themes/BubbleButton.xaml` 里自己的样式。
- 自定义颜色资源的 key **不能与 Fluent 主题的 key 同名**（窗口资源优先于应用资源，会被悄悄盖住，曾导致宠物卡片边框消失）。新 key 加 `Bubble` / `PetCard` 这类前缀，`ThemeResourcesTests` 会检查冲突，三套色板的 key 也必须一致。
- 给控件写自己的 `Style` 时要加 `BasedOn`，或把样式放到外层元素上，否则会丢掉主题样式。
- `ThemeMode` 在 .NET 10 里仍是实验性 API，项目对 `WPF0001` 做了 `NoWarn`。

## 设置与进程列表

- **设置页是“草稿 + 保存”**：外观 / 行为 / 系统三个标签页的改动只写进 `Application/Settings/SettingsEditor` 的草稿（草稿 = 已保存设置 + 用户按字段的修改），点“保存”才经 `SettingsService.UpdateAsync` 生效，“还原”丢弃草稿；有未保存修改时关闭面板会询问。宠物标签页和托盘菜单仍是点击即生效。已保存设置在别处变化（如托盘改了尺寸）时，草稿里没改过的字段跟着变，改过的保持用户的值。有错误的输入（如不合法的分钟数、进程列表冲突）会阻止保存但保留已输入的文字；进程列表的互斥检查针对草稿。开机自启（注册表）和老板键（要向系统注册）不是纯设置：保存时才应用，失败时回滚并提示，其余设置照常保存。比较设置是否有改动用 `Domain/Settings/PetSettingsComparer`（record 自带的相等会按引用比较列表）。
- 设置存在 JSON 文件里（`Infrastructure/Storage/JsonSettingsStore`）。**默认值只在没有配置文件、第一次启动时有效**；只要配置文件已存在，文件里缺失的进程列表（开发、会议、聊天、全屏）和“关闭的场景”列表一律按空处理，不用默认值补，也不要在读取时再往里加新的默认项。单个值（开关、时间等）缺失时才用默认值。
- 进程名列表有两条规则，由 `Domain/Settings/ProcessListRules` 统一管理：进程名不能含空格；同一个进程只能在一个列表里。设置页输入时用 `Check` 校验（有错就不应用并在框下显示原因）；读取配置文件和每次修改设置时由 `SettingsService` 调用 `Enforce` 强制，重复的名字只保留在更重要的列表里（会议软件 > 聊天软件 > 开发工具 > 全屏程序），去掉的名字写日志，并在设置页的“识别程序”下提示。新增进程列表时要同步更新 `ProcessListKind`、`ProcessListRules` 里的取值/赋值/顺序和设置页。
- 新增场景（`PetState` 里的情境状态）要同时改：`ScenarioCatalog.All`（设置页的开关列表）和需要时的 `AmbientRanking`、`PetCapabilities` 的 `ScenarioStates`、`PetStateFallback`、两个 resx 里的 `Scenario*` / `StateHint*`、robot 的 manifest 与 `GenerateRobotPet.ps1`。
- 数字输入框用 `Presentation/DigitsOnly` 限制只能输入数字；时刻（如下班时间）用小时、分钟两个下拉框选，不让手输。

## 自动更新

- 更新流程整个交给 NuGet 包 `GitHubReleaseUpdater`（`GitHubReleaseUpdater.AutoUpdate` 命名空间的 `UpdateController`）：查 `coldhighsun/DeskBuddy` 的最新**正式版** Release（不含预发布），资产里挑名字含 `Setup` 的 `.exe`（`PatternAssetSelector("*Setup*.exe")`；默认选择器要求文件名带系统/架构，装机程序名不满足），下载并校验后以 Inno `/SILENT` 启动，应用随即退出；更新器给安装程序加 `/RELAUNCH=1`，`.iss` 的 `[Run]` 用 `Check: ShouldLaunchApp`：静默安装只有带这个开关才会重启应用，其他静默安装（winget、脚本）不启动应用。
- 只有 App 层有更新相关代码：`App/Presentation/AppUpdates.Create` 组装 `ReleaseUpdater` 和 `UpdateController`（版本号无法解析时返回 null，此时托盘不显示“检查更新”、关于窗口隐藏更新区）；`TrayUpdatePresenter` 实现 `IUpdatePresenter`（托盘气泡 + 确认框，`InstallerStarted` 时退出应用）。启动后 30 秒首次检查，之后每 24 小时；设置项 `AutoCheckUpdates` 经 `IsAutoCheckEnabled` 控制自动检查；开发版（没有 git tag 时 MinVer 给的 `0.0.0-alpha.0.N`，`AppUpdates.IsDevelopmentBuild`）永远不自动检查，免得提示把正在开发的进程换成正式版，手动检查仍可用。托盘菜单和关于窗口都有“检查更新”，手动检查无视 6 小时的节流（`update-state.json`，由包的 `JsonFileLastCheckStore` 写）。
- 新版本没有可下载的安装程序时 `HasInstaller` 为 false：托盘不显示“更新到 x”，关于窗口显示 `UpdateNoInstaller`。`CleanDownloadDirectory` 已打开，下载目录（临时目录下的 DeskBuddy/update）只给更新用。
- 发布流程只上传安装程序一个资产；`AppUpdates` 显式开启 `RequireVerifiedDownload`：校验依赖 GitHub API 返回的 asset `digest`，缺失时不会启动安装程序。包的异常信息是英文的，只写日志；给用户看的失败原因一律经 `UpdateErrorText.For` 换成界面语言（新增异常类型时在那里加一条）。

## 本地化

界面文字在 `src/DeskBuddy.App/Resources`：`Strings.resx` 是中文（默认），`Strings.en.resx` 是英文。新增文案两个文件都要加。

## 设计要求

- 【必须遵守】软件设计必须便于方便地扩展各种宠物：新增宠物只需在 `pets/` 下放入 `atlas.png` 和 `manifest.json`，不得修改代码；任何功能都不得写死某个具体宠物的名称、尺寸、帧数或朝向，一律通过宠物 manifest 与 `Pet` 模型获取，并为宠物缺少的动画/状态提供合理回退。
