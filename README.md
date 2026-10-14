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
- Pets made of plain PNG frames plus a `manifest.json` (see [pets/robot](pets/robot)).
- The first tab of the panel lists every installed pet with a live preview, its picture size and what it can do (follows
  the mouse, climbs screen edges, keeps you company while developing, acts out situations), so you can compare and pick one.
- A pixel-art robot pet for programmers ([pets/robot](pets/robot)): when a development tool (an IDE, an editor or a
  terminal) is in front and you are typing, it sits down at a desk and codes. The tools are editable in settings, and
  only the foreground process name is read, never window titles or keystrokes. Pets without a `Coding` state simply
  keep standing; regenerate the robot with `tools/GenerateRobotPet.ps1`.
- The robot also reacts to what you do as a developer, each reaction switchable in settings: a loading ring while a
  build runs and a cheer when it ends (it cannot tell whether the build succeeded), a parcel when git talks to a
  remote, coffee and heavy eyes when you code for hours late at night, and glasses with a magnifier when you look
  something up in a browser right after coding. It also puts on a headset and stays quiet while a meeting
  application (Zoom and Tencent Meeting by default; the list is editable, add Teams or others there) is in front, waves when you come back after
  half an hour away, shows an empty battery below 20% on battery power and charges up when you plug in. Only process
  names are read, never command lines or window titles. The clock has its say too: a bowl of rice at lunch and
  dinner time, a glance at the watch when the working day ends (18:00 by default, editable), a party hat on New Year's
  Day, Chinese New Year, Programmers' Day and Christmas, and, if you switch it on, a bell on the hour.
  Its eyes follow the mouse pointer while it stands around, it gets dizzy when you shake the mouse, and it hangs on
  to the screen edge when you drop it within a few pixels of the left or right side. Drop it from high above the
  taskbar and it floats down under an umbrella or a parachute, which folds away when it lands. With
  "climb the screen edges" switched on in the settings (off by default, and only for pets that have the pictures,
  such as the robot), the pet also climbs up and down the sides of the
  screen by chance, hangs from the top edge and crawls along it, then lets go and floats down or climbs back to the
  ground; dropping it at the top or a side makes it start right away. Each of these pictures carries a
  small label (BLD, GIT, MTG, LOW...), and resting the mouse on the pet explains in words what it is doing.
  Pets that have the pictures for it, such as the cat, also hop now and then while they walk, though never close to
  the edge of the screen they walk towards. If you switch on "say something unprompted" in the settings (off by
  default), the pet now and then makes a casual remark, shown briefly without raising the alert.
- English and Simplified Chinese: the interface follows the system language, or choose one in the settings (a change
  takes full effect after a restart). Translations live in `src/DeskBuddy.App/Resources` (`Strings.resx` is Chinese,
  `Strings.en.resx` is English).
- Light and dark themes: the panel, tray menu and speech bubble follow the Windows app theme and switch as it changes, or
  choose light or dark under Appearance in the settings (takes effect at once). A Windows high-contrast theme is respected.
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
