# Agent notes

## Project concept

Scrapbook turns local photo folders into a continuously changing collage on Windows. Photos appear one at a time with preserved aspect ratio, randomized size, position and tilt, optional white frames and shadows, and entrance animations. Old cards leave when the configured maximum is reached. Keep the experience simple, smooth, configurable and compatible with multiple monitors and display scaling.

The repository builds two independent applications from shared code:

- **ScrapbookSaver**: Windows screen saver (`.scr`), settings and previews.
- **ScrapbookWallpaper**: live collage behind desktop icons, with tray controls and optional startup at user login. It has its own settings and does not replace the screen saver.

## Architecture and operation

- `ScrapbookSaver/Program.cs`: STA entry point, command routing, `SaverOptions`, screen saver forms/context, and the older GDI+ renderer used by some diagnostic commands. WinForms hosts WPF content through `ElementHost`.
- `ScrapbookSaver/GpuCollageView.cs`: main shared renderer. It catalogs enabled folders recursively, filters supported image formats, deduplicates paths and shuffles a deck of images. Background work decodes images, applies EXIF orientation, resizes proportionally and prepares borders/shadows. Up to five cards are prefetched per view, with two concurrent decoding slots shared across views. WPF composites sprites and animations with hardware acceleration when available. Resize generations prevent stale prepared cards from being reused.
- `ScrapbookSaver/SettingsWindow.cs`: WPF settings and preview UI, folder enable/disable, validation and saving. Keep controls usable at small window sizes and high DPI. Random animation chooses Fade, Slide or Zoom per photo.
- `ScrapbookWallpaper/WallpaperApplication.cs`: single-instance mutex, tray menu, per-monitor wallpaper windows, session lock handling, desktop discovery and recovery polling. Pause/lock disposes wallpaper windows; resume/unlock rebuilds them.
- `ScrapbookWallpaper/ScrapbookWallpaper.csproj`: links shared `ScrapbookSaver/*.cs` and defines `WALLPAPER` to select wallpaper entry behavior and settings storage.
- `ScrapbookSaver/AppArtwork.cs`, `assets/`, `tools/Build-Icon.ps1`: shared embedded artwork and repeatable ICO packaging.

Settings are JSON at `%LOCALAPPDATA%/ScrapbookSaver/settings.json` and `%LOCALAPPDATA%/ScrapbookWallpaper/settings.json`. Preserve compatibility with legacy `Folder`/`Folders` settings and current `ImageFolders` entries. Defaults use the Windows Pictures known folder. Startup is opt-in and uses the current user's registry Run key with the executable's actual path.

Desktop attachment uses undocumented Explorer behavior. Classic desktops use a WorkerW host; raised desktops use opaque layered children of Progman below `SHELLDLL_DefView` icons and above WorkerW. Configure `AllowTransparency` before showing wallpaper forms and attach after `Show()` to avoid handle recreation losing the parent. Modeless WPF settings opened from the WinForms loop require `ElementHost.EnableModelessKeyboardInterop`.

Wallpaper geometry must use physical pixels regardless of the caller's DPI context. WPF Settings callbacks and WinForms callbacks can use different contexts. `DesktopDpiScope` restores the caller after synchronous desktop work; never hold it across an await. Enumerate monitor rectangles afresh with `DesktopHost.GetMonitorBounds()` rather than using DPI-dependent cached `Screen.AllScreens` bounds. Create wallpaper HWNDs in Explorer's DPI context to avoid cross-process `SetParent` resetting awareness, then position them in a per-monitor-aware scope. Preserve negative monitor coordinates.

## Build and verification

Use the .NET 10 SDK on Windows. Published framework-dependent x64 binaries require the .NET 10 Desktop Runtime.

```powershell
dotnet publish .\ScrapbookSaver\ScrapbookSaver.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\dist
Copy-Item .\dist\ScrapbookSaver.exe .\dist\ScrapbookSaver.scr -Force
dotnet publish .\ScrapbookWallpaper\ScrapbookWallpaper.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\dist\wallpaper
```

Check each publish exit code before copying artifacts. Build both projects when shared code changes. `dist/ScrapbookSaver.scr` is tracked; other build outputs are mostly ignored. Inspect Git status first and preserve unrelated changes.

Use relevant existing diagnostics rather than adding tests that merely mirror code. Screen saver commands include `--gpu-info`, `--render-gpu-test`, `--render-gpu-random-test`, `--render-settings-test`, `--render-preview-window-test` and `--fullscreen-smoke-test`; inspect argument handling in `Program.cs` before running. Wallpaper offers `--diagnose-shell <output-path>` and `--smoke-test <output-path>`. Its single-instance mutex can prevent a smoke test from running when another instance is active.

For rendering/UI changes, inspect the actual visual result as well as diagnostic output. Parent/geometry checks alone do not prove wallpaper is visible above the static background. Report untested behavior explicitly, particularly real sign-in startup and Explorer restart recovery. Documentation-only changes need document/link and diff checks, not application rebuilds.

For wallpaper placement or DPI changes, run `dotnet run --project tests/WallpaperDpiRegression/WallpaperDpiRegression.csproj -c Release` with the normal wallpaper app stopped. This exercises repeated WPF Settings closure and four caller DPI contexts, independently checking physical monitor rectangles and context restoration. See its README for interactive test requirements and limits.

## Required worklog practice

For every work session/task in this repository, create a separate Markdown file under `worklog/` and finish updating it before the final response, including investigation-only or unsuccessful sessions. Continue updating that file within the same task; start a new file for a new task. Do not append new work to root `WORKLOG.md`.

- Name files `YYYY-MM-DD-NN-short-description.md` using the local Asia/Bangkok date and the next unused two-digit sequence for that date. Never overwrite another session's log.
- Record the request/objective, starting commit and relevant existing changes, work performed and affected files, commands/checks and actual results, and remaining limitations or follow-up.
- Distinguish completed work, plans and unverified claims. Never invent test results or claim a push/release happened merely because it was requested.
- For retrospective entries, label them as reconstructed, include the date reconstructed and supporting commits/documents, and distinguish historical evidence from checks performed now. Do not invent missing session times.
- Do not record secrets or unnecessary personal data. Use repository-relative paths in logs for portability.
- Keep `AGENTS.md` current when architecture, project concept or workflow changes. `worklog/README.md` describes the log format; root `WORKLOG.md` is only a navigation entry point.
