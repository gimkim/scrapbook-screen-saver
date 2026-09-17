# Scrapbook 1.2

## New

- Separate Live Wallpaper application: photo collages behind desktop icons, with multi-monitor support.
- System tray controls for Settings, Pause and Exit.
- Optional **Run on startup / login** setting for the current Windows user.
- New stacked-photo application icon for both applications, settings windows and the wallpaper tray.

## Fixes

- Support for newer Windows desktop layers so wallpaper windows sit above the static background and below icons.
- Keyboard input in Live Wallpaper Settings, including integer and decimal fields.

## Downloads

- **ScrapbookSaver.scr**: Windows screen saver. Right-click and choose Install, or open to configure.
- **ScrapbookSaver.exe**: executable version of the screen saver/settings application.
- **ScrapbookWallpaper.exe**: standalone live wallpaper. Use its tray menu to configure or exit.

All binaries are Windows x64 single-file applications requiring the **.NET 10 Desktop Runtime**. Keep the wallpaper EXE in a permanent location before enabling startup. Wallpaper settings are stored separately from screen saver settings; existing saved preferences are preserved. No debug symbols are included in the release assets.

Validation: both Release builds, desktop attachment/rendering checks on three monitors, real keyboard-entry checks in Settings, and embedded icon checks. Automatic startup after a real sign-in and recovery after a real Explorer restart have not been exercised.
