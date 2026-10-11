# DeskBuddy

**English** | [简体中文](README.zh-CN.md)

[![CI](https://github.com/coldhighsun/DeskBuddy/actions/workflows/ci.yml/badge.svg)](https://github.com/coldhighsun/DeskBuddy/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/github/license/coldhighsun/DeskBuddy)](LICENSE)
[![Release](https://img.shields.io/github/v/release/coldhighsun/DeskBuddy?include_prereleases)](https://github.com/coldhighsun/DeskBuddy/releases)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)

A desktop pet for work computers. It lives on your desktop, reacts to what you are doing, and stays out of the way.

> **Status:** early development. Features and APIs may change without notice.

## Features

- A small animated pet that walks, sleeps, gets tired, and reacts when you drag it.
- Skins made of plain PNG frames plus a `manifest.json` (see [skins/default](skins/default)).
- Tray icon and single-instance protection.

## Build

Requires Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build DeskBuddy.slnx
dotnet test DeskBuddy.slnx
dotnet run --project src/DeskBuddy.App
```

Versions are derived from git tags (`v1.2.3`) by [MinVer](https://github.com/adamralph/minver).

## License

[MIT](LICENSE)
