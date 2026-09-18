# Separate wallpaper build and raised desktop correction

- Work date: 2026-09-17 (Asia/Bangkok)
- Type: retrospective; reconstructed 2026-09-18
- Evidence: original root WORKLOG.md preserved in commit 82cb4ec; source/release preparation in 847f6f9, distribution update in 82cb4ec.

## Historical record

- Added ScrapbookWallpaper build, shared collage rendering/settings UI, separate settings file, tray Settings/Pause/Exit and opt-in current-user login startup registration.
- User reported Windows static wallpaper remained visible despite passing initial parent/geometry checks.
- Live diagnostics confirmed Progman has WS_EX_NOREDIRECTIONBITMAP, a layered SHELLDLL_DefView and a child WorkerW. Old build placed wallpaper inside WorkerW.
- Corrected raised-desktop attachment to opaque layered children of Progman, below icons and above WorkerW; retained classic WorkerW fallback.
- Configure managed AllowTransparency before showing forms and attach after Show returns so WinForms handle recreation does not discard attachment.
- Rebuilt dist/wallpaper/ScrapbookWallpaper.exe and launched updated binary. Eight-second runtime test: all three windows remained attached to Progman at their correct screen rectangles and generated 8-9 cards each. Live hierarchy confirms icons, three layered wallpaper windows, then WorkerW.
- Screensaver build: zero warnings/errors. Visual confirmation requested from user; available computer-use window inventory does not expose Desktop for screenshot capture. Login startup and real Explorer restart were not exercised.

## Reconstruction verification and limitations

Original section text preserved below its heading. Verification claims above are historical reports, not checks rerun today. Requests or intentions do not establish completion of remote actions. Git records the 1.2 screen saver binary update in 82cb4ec; remote release publication/deletion was not independently checked.
