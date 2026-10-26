# DeskBuddy

[English](README.md) | **简体中文**

[![CI](https://github.com/coldhighsun/DeskBuddy/actions/workflows/ci.yml/badge.svg)](https://github.com/coldhighsun/DeskBuddy/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/github/license/coldhighsun/DeskBuddy)](LICENSE)
[![Release](https://img.shields.io/github/v/release/coldhighsun/DeskBuddy?include_prereleases)](https://github.com/coldhighsun/DeskBuddy/releases)
[![Downloads](https://img.shields.io/github/downloads/coldhighsun/DeskBuddy/total)](https://github.com/coldhighsun/DeskBuddy/releases)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)

一只住在办公电脑桌面上的桌面宠物。它会根据你的操作做出反应，同时尽量不打扰你。

> **状态：** 早期开发阶段，功能和接口可能随时变动。

## 功能

**宠物本身**

- 小巧的动画宠物：沿任务栏行走、睡觉、疲倦，被拖动时也有反应。站着不动时眼睛会跟着鼠标转，用力晃鼠标它会头晕。
- 把它拖到屏幕左右边缘松手，它会挂在边缘上；从任务栏上方高处松手，它会撑着雨伞或降落伞飘落。在设置里打开
  “攀爬屏幕边缘”（默认关闭）后，它还会随机沿屏幕两侧攀爬、挂在顶部横向移动。
- 处于长驻状态（写代码、开会等）时，它不会几个小时一动不动，而是每隔几秒短暂溜达一小段；可在“行为”里关闭“忙碌时溜达”。
- 带有对应图片的宠物走路时偶尔会跳一下。打开“主动说话”（默认关闭）后，宠物会时不时随口说一句。

**它会注意你在做什么**

只读取前台进程名，不读取窗口标题、命令行和按键内容。每一项反应都可以在设置里单独关闭，进程列表也可以修改。
宠物只会表演自己有图片的情境，缺少的状态会回退到相近状态。

- 开发：IDE、编辑器或终端在前台时坐到桌前敲代码；构建时显示加载转圈、结束时欢呼（无法判断构建成败）；
  git 与远程同步时寄快递；深夜连续开发数小时后喝咖啡；刚写完代码就去查资料时戴上眼镜。
- 会议与聊天：会议软件（默认 Zoom、腾讯会议）在前台时戴上耳机安静陪着；聊天软件在前台时会陪着聊天。
- 离开与电量：离开超过半小时再回来会挥手欢迎；用电池且电量低于 20% 时举着空电池，插上电源后开心充电。
- 时间：饭点端起碗筷、到下班时间看表（默认 18:00，可改）、元旦／春节／程序员节／圣诞节戴派对帽，整点敲钟报时需要在设置里手动开启。
- 这些图案都带小字标签（BLD、GIT、MTG、LOW 等），鼠标停在宠物上还会用文字说明它在做什么。

**宠物**

- 自带六只宠物：robot（默认）、cat、dog、rabbit、penguin、ghost。

| **robot** | **cat** | **dog** | **rabbit** | **penguin** | **ghost** |
| :---: | :---: | :---: | :---: | :---: | :---: |
| <img src="docs/pets/robot.png" width="96" alt="robot"> | <img src="docs/pets/cat.png" width="96" alt="cat"> | <img src="docs/pets/dog.png" width="96" alt="dog"> | <img src="docs/pets/rabbit.png" width="96" alt="rabbit"> | <img src="docs/pets/penguin.png" width="96" alt="penguin"> | <img src="docs/pets/ghost.png" width="96" alt="ghost"> |

- 面板的第一个标签列出所有已安装的宠物，带动画预览、画面尺寸和能力标签（跟随鼠标、攀爬屏幕边缘、开发陪伴、情境动作），
  方便对比后选择；托盘菜单也可以切换宠物和尺寸。

**应用**

- 支持简体中文和英文：界面默认跟随系统语言，也可以在设置里手动选择（重启后完全生效）。翻译文件在
  `src/DeskBuddy.App/Resources`（`Strings.resx` 为中文，`Strings.en.resx` 为英文）。
- 支持深色和浅色主题：面板、托盘菜单和对话气泡默认跟随 Windows 的应用主题并随之实时切换，也可以在设置的“外观”里手动选择浅色或深色（立即生效）。
  Windows 高对比度主题优先。
- 设置页是“草稿 + 保存”：改动点“保存”后才生效，“还原”会丢弃改动。
- 托盘图标、关于窗口与单实例保护。

## 添加你自己的宠物

宠物是 `pets/` 下的一个文件夹：一张精灵图集 `atlas.png`（所有帧按相同大小的格子排列）加一个 `manifest.json`
（列出各状态的帧编号和速度）。把文件夹放进去，宠物就会出现在面板里，不需要改任何代码。可选的 `icon.png`（只画头部）
会用作托盘图标。示例见 [pets/robot](pets/robot)。自带宠物由 [tools](tools) 里的脚本绘制（例如 `tools/GenerateRobotPet.ps1`）。

## 安装

从 [Releases](https://github.com/coldhighsun/DeskBuddy/releases) 页面下载 `DeskBuddy-Setup-x.y.z.exe` 并运行。按当前用户安装，无需管理员权限，也无需另装 .NET 运行时。覆盖安装新版本即可升级，卸载时会询问是否保留设置和日志。

## 构建

需要 Windows 和 [.NET 10 SDK](https://dotnet.microsoft.com/download)。

```bash
dotnet build DeskBuddy.slnx
dotnet test DeskBuddy.slnx
dotnet run --project src/DeskBuddy.App
```

打包安装程序需要先安装 [Inno Setup 7](https://jrsoftware.org/isinfo.php)，然后运行：

```bash
powershell -NoProfile -File tools/Build-Installer.ps1
```

版本号由 [MinVer](https://github.com/adamralph/minver) 根据 git tag（`v1.2.3`）自动生成。推送 `v*` tag 后，CI 会构建、测试、
打包安装程序并发布为 GitHub Release（带连字符的 tag，如 `v1.2.0-rc.1`，会标记为预发布）。

## 许可证

[MIT](LICENSE)
