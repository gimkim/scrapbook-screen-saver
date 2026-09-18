# Wallpaper settings keyboard input

- Work date: 2026-09-17 (Asia/Bangkok)
- Type: retrospective; reconstructed 2026-09-18
- Evidence: original root WORKLOG.md preserved in commit 82cb4ec; source/release preparation in 847f6f9, distribution update in 82cb4ec.

## Historical record

- Fixed modeless WPF Settings opened from the WinForms message loop by calling ElementHost.EnableModelessKeyboardInterop before Show.
- Added --settings launch option for direct access on first launch.
- Published and replaced dist/wallpaper/ScrapbookWallpaper.exe, then restarted the wallpaper.
- Computer-use verification on the actual Settings window: Ctrl+A and typing changed minimum width from 30 to 31 and animation duration from 0.5 to 0.7. Screenshots confirmed both edits. Cancel discarded these test values, preserving saved configuration.

## Reconstruction verification and limitations

Original section text preserved below its heading. Verification claims above are historical reports, not checks rerun today. Requests or intentions do not establish completion of remote actions. Git records the 1.2 screen saver binary update in 82cb4ec; remote release publication/deletion was not independently checked.
