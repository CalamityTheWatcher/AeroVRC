# AeroVRC (C# / .NET 9)

Native C# port of `VRChatWatchdog.ps1` (the PowerShell original, ~8,500 lines).
Same UI, same behavior, same `%APPDATA%\AeroVRC\config.json` — existing configs
load unchanged. Massively faster: window up in ~1.7 s (vs ~12 s for the PS2EXE
build) and ~1/3 the RAM.

## Layout

| File | Contents (mirrors the PS source's sections) |
|---|---|
| `Program.cs` | Entry point (DPI-unaware on purpose — matches the original's scaling) |
| `Config.cs` | Full config model + tolerant JSON load/save (PS-compatible) |
| `Ui.cs` | Palette, fonts, rounded paths, animated button styling, card factory, DWM dark title bar |
| `Controls/AeroControls.cs` | `AeroCheckBox`, `AeroStepper`, dark menu colors (verbatim from the embedded C#) |
| `Services/NativeInterop.cs` | Mic mute (CoreAudio COM) + `winsqlite3.dll` P/Invoke |
| `MainForm.cs` | Shell: nav rail, owner-drawn nav icons, pages infra, sparkles, log store, theming |
| `MainForm.VrcLog.cs` | VRChat output_log tail parsing (worlds, players, roster, avatars) |
| `MainForm.Vrcx.cs` | VRCX SQLite snapshot reader |
| `MainForm.Launch.cs` | Steam/exe launching, Start-VRChat, presets, auto-launch, panic, cache clear |
| `MainForm.Optimize.cs` | Priorities, affinity, Performance Mode, VRChat config.json, FSO/GameDVR |
| `MainForm.Hardware.cs` | CPU%, GPU/VRAM/temps (LHM via reflection or async nvidia-smi), ping health, crash hints, bedtime |
| `MainForm.Osc.cs` | OSC FPS listener + chatbox sender, Discord Rich Presence, bookmark ops |
| `MainForm.Session.cs` | Apply-config-to-UI, playtime tracking, dashboard refresh, monitoring toggle |
| `MainForm.Tick.cs` | The 1-second master tick (watchdog loop, guards, schedules) + shutdown |
| `MainForm.Toasts.cs` | Toast notifications, welcome splash, ~30 fps FX tick |
| `MainForm.ShotHarness.cs` | `AEROVRC_SHOTDIR` off-screen page-screenshot harness (verification only) |
| `Pages/*.cs` | One partial per page: Dashboard, Apps, Presets, Stats, VRCX, Logs, Settings, Performance, Bookmarks, Photos |

## Build

Requires the .NET 9 SDK (a local no-admin copy lives at
`%LOCALAPPDATA%\Microsoft\dotnet-sdk9\dotnet.exe`).

```powershell
dotnet build -c Debug                    # dev build
# Small exe; needs the .NET 9 Desktop Runtime installed on the target machine:
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true -o publish\fd --source https://api.nuget.org/v3/index.json
# Zero-dependency exe for sharing (bigger):
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true -o publish\sc --source https://api.nuget.org/v3/index.json
```

(The `--source` flag matters: this machine's NuGet has no configured sources.)

Ship `Logo.png` next to the exe (window/taskbar icon; falls back to the
embedded icon if missing).

## Verification harness

```powershell
$env:AEROVRC_CONFIGDIR = "C:\some\isolated\dir"   # keep the real config safe
$env:AEROVRC_SHOTDIR   = "C:\some\shots\dir"      # page PNGs land here
.\AeroVRC.exe                                     # renders all pages off-screen, exits
```

Note: with an empty `AEROVRC_CONFIGDIR`, the first-run migration will copy any
stale `%APPDATA%\Azure` / `%APPDATA%\VRChatWatchdog` config it finds - same
behavior as the original.
