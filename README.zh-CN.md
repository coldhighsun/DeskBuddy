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
- 皮肤由普通 PNG 帧和 `manifest.json` 组成（见 [skins/default](skins/default)）。
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
