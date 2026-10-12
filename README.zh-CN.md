# DeskBuddy

[English](README.md) | **简体中文**

[![CI](https://github.com/coldhighsun/DeskBuddy/actions/workflows/ci.yml/badge.svg)](https://github.com/coldhighsun/DeskBuddy/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/github/license/coldhighsun/DeskBuddy)](LICENSE)
[![Release](https://img.shields.io/github/v/release/coldhighsun/DeskBuddy?include_prereleases)](https://github.com/coldhighsun/DeskBuddy/releases)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)

一只住在办公电脑桌面上的桌面宠物。它会根据你的操作做出反应，同时尽量不打扰你。

> **状态：** 早期开发阶段，功能和接口可能随时变动。

## 功能

- 小巧的动画宠物：会走动、睡觉、疲倦，被拖动时也有反应。
- 皮肤由普通 PNG 帧和 `manifest.json` 组成（见 [skins/robot](skins/robot)）。
- 适合程序员的像素风机器人皮肤（[skins/robot](skins/robot)）：当开发工具（IDE、编辑器或终端）在前台且你正在操作时，
  它会坐到桌前敲代码。开发工具列表可在设置里修改，只读取前台进程名，不读取窗口标题和按键内容。没有 `Coding`
  状态的皮肤会照常站着；机器人帧可用 `tools/GenerateRobotSkin.ps1` 重新生成。
- 机器人还会对开发者的日常做出反应，每一项都可以在设置里单独关闭：构建时显示加载转圈、结束时欢呼（无法判断构建成败）；
  git 与远程同步时寄快递；深夜连续开发数小时后喝咖啡、顶着黑眼圈；刚写完代码就去浏览器查资料时戴上眼镜、拿起放大镜。
  会议软件（默认 Zoom、腾讯会议，列表可改，Teams 等聊天软件可自行加入）在前台时，它会戴上耳机安静陪着；离开超过半小时再回来会挥手欢迎；
  用电池且电量低于 20% 时举着空电池，插上电源后开心充电。只读取进程名，不读取命令行和窗口标题。
  时间也会触发动作：饭点端起碗筷、到下班时间看表（默认 18:00，可改）、元旦／春节／程序员节／圣诞节戴派对帽，
  整点敲钟报时需要在设置里手动开启。
  站着不动时眼睛会跟着鼠标转；用力晃鼠标它会头晕；把它拖到屏幕左右边缘松手，它会挂在边缘上。
  在设置里打开"攀爬屏幕边缘"（默认关闭，仅对带有对应图片的皮肤生效，如机器人）后，宠物还会随机沿屏幕两侧上下攀爬、
  挂在屏幕顶部横向移动，随后松手撑伞落下或爬回地面；把它拖到顶部或两侧松手会立即开始。
  这些图案都带小字标签（BLD、GIT、MTG、LOW 等），鼠标停在宠物上还会用文字说明它在做什么。
- 支持简体中文和英文：界面默认跟随系统语言，也可以在设置里手动选择（重启后完全生效）。翻译文件在
  `src/DeskBuddy.App/Resources`（`Strings.resx` 为中文，`Strings.en.resx` 为英文）。用 `--Preview true` 启动应用，右键菜单里可以逐个预览所有动画。
- 托盘图标与单实例保护。

## 构建

需要 Windows 和 [.NET 10 SDK](https://dotnet.microsoft.com/download)。

```bash
dotnet build DeskBuddy.slnx
dotnet test DeskBuddy.slnx
dotnet run --project src/DeskBuddy.App
```

版本号由 [MinVer](https://github.com/adamralph/minver) 根据 git tag（`v1.2.3`）自动生成。

## 许可证

[MIT](LICENSE)
