# Wallpaper placement after settings on mixed-DPI monitors

- Work date: 2026-09-18 (Asia/Bangkok)
- Type: current session
- Starting commit: `82cb4ec`; existing documentation changes and rebuilt `dist/ScrapbookSaver.scr` preserved.

## Request
Fix wallpaper placement after saving Settings when monitors have different DPI scales. Initial launch is correct; the upper monitor shifts after windows are rebuilt.

## Investigation
- Read agent notes, wallpaper lifecycle and shared settings code.
- Captured the running application's desktop hierarchy with `--diagnose-shell`: upper wallpaper rectangle was `165,-720 - 2725,720`, while the desktop extends to Y=-1440. This agrees with the supplied screenshot showing the upper wallpaper shifted downward.
- The pre-fix integration harness also exposed inconsistent coordinates: its initial upper window was `662,-2880 - 5782,0` instead of `331,-1440 - 2891,0`. WPF close dispatch changed its subsequent placement. This is a related context-dependent failure, not a claim that the harness reproduced the user's exact Save-button sequence.

## Work performed
- `ScrapbookWallpaper/WallpaperApplication.cs`: introduced synchronous DPI scopes that restore the caller's context; enumerate monitor rectangles in physical pixels independently of WinForms' cached Screen bounds; create wallpaper HWNDs in Explorer's DPI context; position them in a per-monitor-aware scope. Layout polling and diagnostics use the same physical coordinate convention.
- Added `tests/WallpaperDpiRegression/` with a standalone Windows integration harness and run instructions. It uses the actual application initializer, closes WPF Settings through native window dispatch three times, then exercises rebuilds from four DPI contexts and checks restoration. It does not write settings.
- Updated `AGENTS.md` with the physical-pixel invariant and regression command.
- Published `dist/wallpaper/ScrapbookWallpaper.exe` and restarted the normal wallpaper application after tests. No shared screen saver source changes; the previous tracked `.scr` modification was preserved.

## Verification
- Pre-fix regression exited 1 with an upper-monitor geometry mismatch.
- `dotnet build tests/WallpaperDpiRegression/WallpaperDpiRegression.csproj -c Release`: zero warnings/errors.
- Fixed regression exited 0: initial launch, three WPF Settings closures, and unaware/system-aware/per-monitor/per-monitor-v2 rebuild callers all matched physical monitor bounds. Caller DPI contexts were restored.
- Actual monitor/window rectangles: upper `331,-1440 - 2891,0` (2560x1440); primary `0,0 - 3200,2000` (3200x2000). Exactly one wallpaper per monitor, parented to Explorer.
- Release win-x64 single-file publish succeeded.
- Published binary's eight-second `--smoke-test` exited normally: two correctly parented windows, matching rectangles, three rendered photo cards per window from 370 source images; startup remained off.
- Computer-use inspection rendered the real Settings window. Clicking Save was not completed: first activation failed for the hidden launch; an interactive relaunch then returned `GetCursorPos failed: Access is denied. (0x80070005)`. Did not treat these attempts as a successful Save.
- `git diff --check` passed. Runtime reports are local ignored root `.txt` artifacts: `wallpaper-dpi-baseline.txt`, `wallpaper-dpi-fixed.txt`, `wallpaper-dpi-smoke.txt` and desktop hierarchy dumps. The file named `wallpaper-fixed-after-save.txt` was captured after a failed click attempt, so it is NOT evidence of a completed Save.

## Technical references
- [Microsoft: mixed-mode DPI contexts](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-improvements-for-desktop-applications): message callbacks use their window's DPI context; native coordinate APIs can be virtualized.
- [Microsoft: SetParent](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent): mismatched cross-process DPI awareness can reset the child process's awareness.
- [Microsoft: SetThreadDpiAwarenessContext](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setthreaddpiawarenesscontext): temporarily switch and restore thread context.

## Remaining work / limitations
Actual Save-button input and a fresh full-desktop visual confirmation remain unverified because native input failed and the available computer-use window list does not expose Desktop. The regression covers the same Settings Closed/rebuild handler used after Save, and the renderer smoke check passed. Real sign-in, Explorer restart, other monitor arrangements and the classic WorkerW desktop were not tested. No commit or push performed.
