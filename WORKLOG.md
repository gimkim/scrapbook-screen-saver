# Worklog

## 2026-09-17 - Separate wallpaper build and raised desktop correction

- Added ScrapbookWallpaper build, shared collage rendering/settings UI, separate settings file, tray Settings/Pause/Exit and opt-in current-user login startup registration.
- User reported Windows static wallpaper remained visible despite passing initial parent/geometry checks.
- Live diagnostics confirmed Progman has WS_EX_NOREDIRECTIONBITMAP, a layered SHELLDLL_DefView and a child WorkerW. Old build placed wallpaper inside WorkerW.
- Corrected raised-desktop attachment to opaque layered children of Progman, below icons and above WorkerW; retained classic WorkerW fallback.
- Configure managed AllowTransparency before showing forms and attach after Show returns so WinForms handle recreation does not discard attachment.
- Rebuilt dist/wallpaper/ScrapbookWallpaper.exe and launched updated binary. Eight-second runtime test: all three windows remained attached to Progman at their correct screen rectangles and generated 8-9 cards each. Live hierarchy confirms icons, three layered wallpaper windows, then WorkerW.
- Screensaver build: zero warnings/errors. Visual confirmation requested from user; available computer-use window inventory does not expose Desktop for screenshot capture. Login startup and real Explorer restart were not exercised.

## 2026-09-17 - Wallpaper settings keyboard input

- Fixed modeless WPF Settings opened from the WinForms message loop by calling ElementHost.EnableModelessKeyboardInterop before Show.
- Added --settings launch option for direct access on first launch.
- Published and replaced dist/wallpaper/ScrapbookWallpaper.exe, then restarted the wallpaper.
- Computer-use verification on the actual Settings window: Ctrl+A and typing changed minimum width from 30 to 31 and animation duration from 0.5 to 0.7. Screenshots confirmed both edits. Cancel discarded these test values, preserving saved configuration.

## 2026-09-17 - Application artwork

- Generated transparent stacked-photo landscape icon with built-in imagegen; source and full prompt retained in assets.
- Added repeatable PowerShell ICO packaging with nine sizes from 16 to 256 pixels, preserving source alpha.
- Embedded icon resources in both EXEs and wired artwork into WPF settings/preview windows, screensaver form and wallpaper tray.
- Published both applications and updated dist/ScrapbookSaver.exe, dist/ScrapbookSaver.scr and dist/wallpaper/ScrapbookWallpaper.exe. Restarted wallpaper with updated binary.
- Verified Windows extracts embedded icons from both EXEs, and computer-use screenshot confirms new icon in actual wallpaper Settings title bar. Settings closed without saving changes.

## 2026-09-17 - Release 1.2 preparation

- Set both application versions to 1.2.0 and added release notes covering the wallpaper build, desktop attachment fix, keyboard input fix and artwork.
- Source is committed before publishing so binary informational versions identify the source commit. The subsequent distribution-only commit retains that source provenance.
- User requested pushing to main, publishing v1.2 and removing the v1.1 release. Preserve the v1.1 Git tag as a historical source reference.
