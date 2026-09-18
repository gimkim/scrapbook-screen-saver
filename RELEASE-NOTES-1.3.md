# Scrapbook 1.3

## Fixes

- Fixed Live Wallpaper placement when windows are recreated after Settings closes on monitors with different DPI scaling, including monitors above or left of the primary display.
- Monitor bounds and wallpaper placement now use physical pixels, independent of the calling UI's DPI context. Wallpaper windows are created with Explorer's DPI context, and the caller's context is restored afterwards.

## Development

- Added a Windows regression test covering repeated Settings closure, four caller DPI contexts, monitor geometry and context restoration.
- Added agent notes describing the project and architecture, plus individual session worklogs and historical summaries.

## Downloads

- **ScrapbookSaver.scr**: Windows screen saver; right-click to install or open to configure.
- **ScrapbookSaver.exe**: executable screen saver/settings application.
- **ScrapbookWallpaper.exe**: standalone live wallpaper with tray controls.

All binaries are version 1.3.0, Windows x64 single-file applications requiring the **.NET 10 Desktop Runtime**. Existing saved settings are preserved. Exit the running wallpaper before replacing its executable; retain its location if login startup is enabled.

Validation of the DPI fix: two-monitor geometry checks, three WPF Settings close/rebuild cycles, four caller DPI contexts and an eight-second rendering smoke test passed. Actual Save-button mouse input could not be verified because desktop input access was denied. Full-desktop visual confirmation, real sign-in startup, Explorer restart and classic WorkerW layouts remain untested.
