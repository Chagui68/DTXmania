## What is DTXManiaNX?
DTXManiaNX is a program that replicates gameplay from Konami's music video game, Gitadora - Drummania/GuitarFreaks. This project was forked from the DTXManiaXG verK SVN repository. It processes DTX files (including older formats such as BMS/BME or GDA/G2D) and allows playing of custom created charts with a use of a game, keyboard or MIDI controller.

For more information regarding creation of DTX files and its data formats, do visit the original [DTXMania Wiki](https://osdn.net/projects/dtxmania/wiki/DTX%20data%20format). Various video tutorials are available from [APPROVED DTX Gaming's YouTube page](https://youtu.be/9GlSk62pgGw) or [
Furukon Rhythm Gaming's YouTube page](https://www.youtube.com/playlist?list=PLj22ny7-DS2V-l0pWLhp8cLRYLF3jskCs).

## Original and Ongoing Forks
* [DTXMania](https://osdn.net/projects/dtxmania) (yyagi)

https://osdn.net/projects/dtxmania

* [DTXMania2](https://dtxmania.net) ([ＦＲＯＭ](https://github.com/DTXMania))

https://dtxmania.net

* [DTXMania AL](http://senamih.com/dtxal) (Sena)

http://senamih.com/dtxal

* [DTXManiaXG verK](https://osdn.net/projects/dtxmaniaxg-verk) ([kairera0467](https://github.com/kairera0467))

https://osdn.net/projects/dtxmaniaxg-verk

## Installation
1. Download the [latest release](https://github.com/limyz/DTXmaniaXG/releases) of DTXMania and extract it to a location of your choice

2. Download and install the [.NET Framework 4.7.1](https://dotnet.microsoft.com/download/dotnet-framework/net471) (if prompted)

3. Download and install the [DirectX End-User Runtime (DirectX v9.0c)](https://www.microsoft.com/en-us/download/details.aspx?displaylang=en&id=35)

## Running the game
After extracting the release (or building from source, see below), start the game by running `DTXManiaNX.exe` from the extracted folder.

- `DTXManiaNX.exe` is the game itself; `DTXCreator.exe` is the bundled chart editor.
- `Config.ini` (created next to the executable on first run) stores all settings, including the folders that are scanned for songs/charts.
- The game must be able to find its `dll\` folder (BASS audio engine, SharpDX/Direct3D, etc.), the `System\` resources and the `ja-JP\` localization files; keep the folder layout intact and do not move the executable out of its folder.

## Adding songs
The game scans for charts **recursively** below the paths set in `Config.ini` under `DTXPath=`. By default it is `.\` (the game folder), but you can point it to several locations separated by `;`, e.g.:

```ini
DTXPath=.\;D:\MisCanciones\DTX\;E:\Clone Hero Songs\
```

Each song must live in its own folder (one song per folder). Supported chart files:

| Format | Description |
| --- | --- |
| `.dtx` | Native DTXMania format (also `.bms`/`.bme`/`.gda`/`.g2d` legacy formats). |
| `.chart` | Rock Band / Clone Hero charts (`notes.chart`). |
| `.mid` / `.smf` | Clone Hero MIDI charts (`notes.mid`). |

For a Clone Hero song folder (`notes.chart` or `notes.mid`), the game imports the **drums (Expert tab)**, reads `song.ini` for the title/artist and difficulty, and plays the audio from `song.ogg`, `song.mp3` or `song.wav`. Synthetic percussion samples (`_dtxmania_*.wav`) are generated automatically in the song folder on first load. Album art (`album.png`/`album.jpg`) is shown in the song list when available.

Notes:
- `DTXPath` folders are scanned on startup (and re-scan on song list). Just drop the song folder in place and restart the game (or delete `songlist.db` to force a full rebuild).
- Audio is limited to `.wav`, `.mp3` and `.ogg`; `.opus` files (used by some Clone Hero packs) are not supported and will be silent.
- Guitar/bass tracks from `.chart`/`.mid` files are not imported (drums only).

## Building from source
Requirements:
- Visual Studio 2017 or newer (the solution uses MSBuild `ToolsVersion` 12.0/15.0), or an equivalent MSBuild installation.
- The **.NET Framework 4.7.1 Developer Pack / targeting pack** (reference assemblies, not just the runtime).

The solution `DTXMania.sln` contains four projects:

| Project | Output |
| --- | --- |
| `DTXMania` | `DTXManiaNX.exe` (the game) |
| `FDK` | `FDK.dll` (framework layer) |
| `libbjxa` | `libbjxa.dll` (BJXA decoder wrapper) |
| `DTXCreator` | `DTXCreator.exe` (chart editor) |

Steps:
1. Open `DTXMania.sln` in Visual Studio.
2. Select the `Release` configuration and build the solution (Build > Build Solution).
3. Every project is configured with `OutputPath = ..\Runtime\`, so the resulting binaries are written directly to the `Runtime\` folder. The native libraries under `Runtime\dll\` and the assets under `Runtime\System\` are prebuilt and are not part of the build.
4. Launch `Runtime\DTXManiaNX.exe` to run the game from the build output.

## Packaging (installer)
The Windows installer is produced with [Inno Setup](https://jrsoftware.org/isinfo.php) using the script `Installer setup.iss` (current version `1.4.3`, publisher `limyz`).

- **Destination:** the game is installed to `{userappdata}\DTXManiaNX` (no administrator rights are required for the app itself, only for the dependency installers).
- **Payload:** the `[Files]` section copies everything from `Runtime\`: `DTXManiaNX.exe(.config)`, `DTXCreator.exe(.config)`, `Manual.chm`, `readme_jp.txt`, the whole `dll\` folder, `System\Graphics`, `System\Sounds`, `ja-JP\` and `Licenses\`.
- **Dependencies:** the embedded [InnoDependencyInstaller](https://github.com/DomGries/InnoDependencyInstaller) code downloads and installs the required prerequisites on demand: **MSI 4.5**, **.NET Framework 4.7.1** and the **DirectX 9.0c** end-user runtime (`dxwebsetup.exe`). These can be toggled with the `UseMsi45`, `UseDotNet471` and `UseDirectX` defines at the top of the script.

To build the installer:
1. Build the solution in `Release` first so that `Runtime\` contains the up-to-date binaries.
2. Open `Installer setup.iss` in Inno Setup and compile it (or run `ISCC.exe "Installer setup.iss"` from the command line).
3. The resulting `DTXManiaNX-1.4.3.exe` installer is written to the `Output\` folder.

## One-command build
Instead of doing the steps above manually, you can build the game and generate the installer with the bundled script. This does **not** require an IDE, only MSBuild and Inno Setup (see the requirements in the previous sections).

```bat
build.bat                          :: Release|x86 + installer
powershell -File build.ps1 -Platform x64
powershell -File build.ps1 -Configuration Debug
powershell -File build.ps1 -SkipInstaller   :: compile only, no Inno Setup
```

The script auto-detects MSBuild (via `vswhere` or the common Visual Studio / Build Tools paths) and `ISCC.exe`, builds `DTXMania.sln` into `Runtime\`, and then compiles `Installer setup.iss`. If a tool is missing it prints what to install. The generated installer is written to the `Output\` folder of the repository root.

## Community Support
For additional help or support, ask away in DTXMania on Discord! 
[https://discord.gg/ST5MWHe](https://discord.gg/ST5MWHe)
