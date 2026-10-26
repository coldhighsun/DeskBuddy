# DeskBuddy

**English** | [简体中文](README.zh-CN.md)

[![CI](https://github.com/coldhighsun/DeskBuddy/actions/workflows/ci.yml/badge.svg)](https://github.com/coldhighsun/DeskBuddy/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/github/license/coldhighsun/DeskBuddy)](LICENSE)
[![Release](https://img.shields.io/github/v/release/coldhighsun/DeskBuddy?include_prereleases)](https://github.com/coldhighsun/DeskBuddy/releases)
[![Downloads](https://img.shields.io/github/downloads/coldhighsun/DeskBuddy/total)](https://github.com/coldhighsun/DeskBuddy/releases)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)

A desktop pet for work computers. It lives on your desktop, reacts to what you are doing, and stays out of the way.

> **Status:** early development. Features and APIs may change without notice.

## Features

**The pet**

- A small animated pet that walks along the taskbar, sleeps, gets tired and reacts when you drag it. Its eyes follow the
  mouse while it stands around, and it gets dizzy when you shake the mouse.
- Drop it at the left or right screen edge and it hangs on. Drop it from high above the taskbar and it floats down under
  an umbrella or a parachute. With "climb the screen edges" switched on (off by default), it also climbs the sides of the
  screen and crawls along the top edge now and then.
- While it is held in a long-running state (coding, a meeting...) it still takes a short stroll every few seconds
  instead of standing frozen for hours; switch this off with "stroll while busy" under Behavior.
- Pets that have the pictures for it hop now and then while they walk. With "say something unprompted" switched on (off
  by default) the pet makes a casual remark from time to time.

**It notices what you are doing**

Only the foreground process name is read, never window titles, command lines or keystrokes. Every reaction can be
switched off in the settings, and the process lists are editable. A pet only acts out the situations it has pictures
for; anything missing falls back to a similar state.

- Developing: sits down and codes when an IDE, editor or terminal is in front; shows a loading ring while a build runs
  and cheers when it ends (it cannot tell whether the build succeeded); sends a parcel when git talks to a remote;
  drinks coffee after hours of coding late at night; puts on glasses when you look something up right after coding.
- Meetings and chat: wears a headset and stays quiet while a meeting application is in front (Zoom and Tencent Meeting
  by default), and chats along while a chat application is.
- Away and power: waves when you come back after half an hour away; shows an empty battery below 20% on battery power
  and charges up when you plug in.
- The clock: a bowl of rice at lunch and dinner time, a glance at the watch when the working day ends (18:00 by default),
  a party hat on New Year's Day, Chinese New Year, Programmers' Day and Christmas, and, if you switch it on, a bell on
  the hour.
- Each of these pictures carries a small label (BLD, GIT, MTG, LOW...), and resting the mouse on the pet explains in
  words what it is doing.

**Pets**

- Six pets come with the app: robot (the default), cat, dog, rabbit, penguin and ghost.

| **robot** | **cat** | **dog** | **rabbit** | **penguin** | **ghost** |
| :---: | :---: | :---: | :---: | :---: | :---: |
| <img src="docs/pets/robot.png" width="96" alt="robot"> | <img src="docs/pets/cat.png" width="96" alt="cat"> | <img src="docs/pets/dog.png" width="96" alt="dog"> | <img src="docs/pets/rabbit.png" width="96" alt="rabbit"> | <img src="docs/pets/penguin.png" width="96" alt="penguin"> | <img src="docs/pets/ghost.png" width="96" alt="ghost"> |

- The first tab of the panel lists every installed pet with a live preview, its picture size and what it can do (follows
  the mouse, climbs screen edges, keeps you company while developing, acts out situations), so you can compare and pick
  one. The tray menu switches the pet and its size as well.

**The app**

- English and Simplified Chinese: the interface follows the system language, or choose one in the settings (a change
  takes full effect after a restart). Translations live in `src/DeskBuddy.App/Resources` (`Strings.resx` is Chinese,
  `Strings.en.resx` is English).
- Light and dark themes: the panel, tray menu and speech bubble follow the Windows app theme and switch as it changes, or
  choose light or dark under Appearance in the settings (takes effect at once). A Windows high-contrast theme is respected.
- The settings are a draft: changes take effect when you click Save, and Revert discards them.
- Tray icon, an About window and single-instance protection.

## Add your own pet

A pet is a folder under `pets/` with a sprite sheet `atlas.png` (all frames in equal cells) and a `manifest.json` that
lists the states, their frame numbers and speeds. Drop the folder in and it shows up in the panel; no code changes are
needed. An optional `icon.png` (just the head) is used as the tray icon. See [pets/robot](pets/robot) for an example.
The bundled pets are drawn by the scripts in [tools](tools) (for example `tools/GenerateRobotPet.ps1`).

## Install

Download `DeskBuddy-Setup-x.y.z.exe` from the [Releases](https://github.com/coldhighsun/DeskBuddy/releases) page and run it. It installs per user and needs no administrator rights or .NET runtime. Installing a newer version over an old one upgrades it, and the uninstaller asks whether to keep your settings and logs.

## Build

Requires Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build DeskBuddy.slnx
dotnet test DeskBuddy.slnx
dotnet run --project src/DeskBuddy.App
```

To build the installer, install [Inno Setup 7](https://jrsoftware.org/isinfo.php) and run:

```bash
powershell -NoProfile -File tools/Build-Installer.ps1
```

Versions are derived from git tags (`v1.2.3`) by [MinVer](https://github.com/adamralph/minver). Pushing a `v*` tag makes
the CI workflow build and test, build the installer and publish it as a GitHub release (a tag with a hyphen, such as
`v1.2.0-rc.1`, is marked as a pre-release).

## License

[MIT](LICENSE)
