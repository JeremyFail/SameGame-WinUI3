![SameGame Logo](https://jeremyfail.dev/projects/programs/same-game/img/hero.jpg)

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=for-the-badge)](https://opensource.org/licenses/MIT)

A modern **WinUI 3 (C#)** reimagining of the classic Windows 3.1 **Same Game** by Ikuo Hirohata (originally implemented in Java in the [SameGame](https://github.com/JeremyFail/SameGame) repository). Click groups of matching tiles, clear the board, and chase a high score - with undo/redo, multiple visual skins, and plenty of customization options.

## How to Play

SameGame is played on a grid of colored blocks.

1. **Click** a group of two or more adjoining blocks of the same color to highlight them. Notice the amount of points the selection is worth (bottom left corner of the screen).
2. **Click again** on the highlighted group to remove it and score the points displayed.
3. **Click elsewhere** to clear the highlight without removing blocks.
4. After removal, blocks **fall down** and empty columns **slide left**.
5. The game ends when no more groups of two or more can be removed.
6. Your goal is to remove as many blocks as possible - ideally all of them.

### Scoring

You score more points for removing larger groups at a time.
The formula for scoring points for removing *n* blocks in one move:

```
Points(n) = n² − 3n + 4
```

| Blocks removed | Points |
|----------------|--------|
| 2 | 2 |
| 3 | 4 |
| 4 | 8 |
| 5 | 14 |
| 6 | 22 |

Look for big clusters when you can!

## Features

- **Classic "Same Game" rules** - adjacent blocks score points, gravity, and column collapse
- **Undo / Redo** - experiment with different moves without starting over
- **High scores** - top 10 scores saved with board size, color count, and date
- **Board sizes** - Small (10×10), Normal (20×10), Large (20×20), or Custom (Any size from 5×5 to 50×50)
- **Three Difficulty levels** - Easy (clustered colors), Medium (random), Hard (scattered colors)
- **Optional countdown timer** - configurable time limit (default 180 seconds)
- **Multiple tile skins** - Modern, Classic, Marbles, Blockcraft, Bricks, Shapes, and Gems
- **Themes & backgrounds** - Light/Dark UI themes; black or green background
- **Custom colors** - pick your own palette (3–6 colors) in Advanced Options
- **Animations** - animated tile movement (click the board to skip); can be toggled on/off
- **Sound effects** - subtle audio cues for each action, enhancing the gameplay experience; can be toggled on/off.
- **Optional background music** - soothing background music (can be toggled on/off)
- **Internationalization** - language support (English included - feel free to contribute a translated language file!)

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| **F2** | New Game |
| **F1** | Help |
| **Ctrl+Z** | Undo |
| **Ctrl+Y** | Redo |

## Developer Requirements

You do **not** strictly need Visual Studio, but you do need the Windows desktop development toolchain.

### Required

1. **Windows 10 version 1809 (build 17763) or later** (Windows 11 recommended)
2. **[.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)** (9.0.300 or newer) - required to build; the app targets **.NET 8**
3. **Windows App SDK** - restored automatically via NuGet when you build (self-contained in the output folder)
4. **Windows App Runtime 2.x** (optional) - only needed if you disable self-contained deployment; install with `winget install Microsoft.WindowsAppRuntime.2.0`

### Recommended (easiest workflow)

- **[Visual Studio 2022](https://visualstudio.microsoft.com/) or newer** (Community edition is free) with the **WinUI Application Development** workload.

### Optional

- **Visual Studio Code** or other code editor - you can edit and build from the terminal with `dotnet build` / `dotnet run`

### First-time setup (command line instructions)

```powershell
# Install .NET 9 SDK for building (app targets .NET 8)
winget install Microsoft.DotNet.SDK.9

# Install WinUI project templates (optional; project is already scaffolded)
dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates
```

## Build

```powershell
dotnet build
```

## Run

```powershell
dotnet run
```

The default launch profile runs **unpackaged** with a self-contained Windows App SDK runtime. To run as an MSIX package (Visual Studio F5 uses this path when packaging is enabled):

```powershell
dotnet run --launch-profile "SameGame (Package)"
```

Note: the Package profile may fail from the CLI if multiple `.exe` files are present in the build output; use Visual Studio **F5** for packaged debugging, or stick with the default unpackaged profile.

Or open `SameGame-WinUI3.sln` in Visual Studio and press **F5**.

## Debugging

`dotnet run` launches a GUI app with no console, so unhandled exceptions often close the window without printing anything. Options:

- **Visual Studio / Cursor + C# Dev Kit** - press **F5** to run under the debugger; breakpoints and the Exception settings window show crashes immediately.
- **Crash log** - unhandled UI exceptions are written to `%LocalAppData%\SameGame\crash.log`.
- **Attach debugger** - start with `dotnet run`, then **Debug → Attach to Process** and select `SameGame.exe`.

## Project Layout

| Path | Purpose |
|------|---------|
| `Model/` | Game logic (board, session, scoring, generation) |
| `UI/` | Tile rendering |
| `Controls/` | Game board control |
| `Dialogs/` | Game over, about, advanced options |
| `Views/` | Objectives, high scores, help windows |
| `Persistence/` | Settings and high scores (JSON in `%LocalAppData%`) |
| `I18n/` | Localized strings |

## Differences from the Java Version

- Settings and high scores use JSON files instead of Java Preferences
- 3D OpenGL gem rendering is replaced with Win2D 2D gem drawing
- Packaged as a WinUI 3 / Windows App SDK desktop app

### Audio files

Sound effects (`remove*.mp3`, `game-end.mp3`) are committed to the repo. Background music (`background*.mp3`) is listed in `.gitignore` - copy your tracks into `Assets/sounds/` when building to include your preferred background audio tracks.

