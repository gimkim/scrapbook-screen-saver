# Scrapbook Screen Saver

A Windows screen saver that adds randomly selected photos to a collage one at a time. Each photo keeps its aspect ratio during resizing. Position, size, and slight tilt are randomized. Older photos are removed when the maximum number on screen is reached, so the collage continues indefinitely.

In Settings, you can add multiple image folders, including their subfolders. Check or uncheck each folder to include or exclude its photos without removing it from the list. You can also choose the minimum and maximum photo width as a percentage of screen width, how far a card may extend past a screen edge (0–25% of the card's size), white border thickness, drop shadow, time between photos, and an entrance animation: None, Fade, Slide in, Zoom from center, or Random for each photo. Random selects Fade, Slide in, or Zoom independently for each photo. Animation duration ranges from 0.2 to 5 seconds; a shorter duration is faster.

The Settings window scales with display DPI and scrolls on smaller screens while keeping the action buttons visible.

The screen saver uses WPF hardware rendering when supported. It prepares up to five upcoming photo cards per display in the background, including resizing, white borders, and shadows, so they are ready before appearing on screen.

## Getting started

1. Open `ScrapbookSaver.scr` to add image folders and configure the appearance, then click **Save**.
2. Click **Preview** to test your settings. Press Esc to close the preview.
3. Right-click `ScrapbookSaver.scr` and choose **Install** to set it as your Windows screen saver.

The default image folder is `%USERPROFILE%\Pictures`. Choose another folder in Settings if this path does not exist. JPG, PNG, BMP, GIF, and TIFF files are supported, including files in subfolders.

Command-line options: `/s` for full-screen mode, `/c` for Settings, `/p <window-handle>` for the Windows preview, and `--preview` for a windowed preview.

## Build

```powershell
dotnet publish .\ScrapbookSaver\ScrapbookSaver.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\dist
Copy-Item .\dist\ScrapbookSaver.exe .\dist\ScrapbookSaver.scr
```

The .NET 10 Desktop Runtime is required on the computer where you run the screen saver.

## Separate Live Wallpaper build

```powershell
dotnet publish .\ScrapbookWallpaper\ScrapbookWallpaper.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\dist\wallpaper
```

Run `dist\wallpaper\ScrapbookWallpaper.exe` to display the collage behind desktop icons on all monitors. Right-click its system tray icon for Settings, Pause, or Exit. Double-click the tray icon to open Settings.

In Settings, tick **Run on startup / login** and click **Save** to start automatically at the current user's Windows sign-in. Untick and save to disable. Keep the executable in a permanent location; if you move it, save the startup option again from its new location. Startup is off by default.

Wallpaper settings are stored separately in `%LOCALAPPDATA%\ScrapbookWallpaper\settings.json`. The default folder follows Windows' Pictures known folder, including redirected Pictures folders. The wallpaper pauses while the session is locked, and retries desktop attachment after Explorer restarts or monitor layout changes. Desktop attachment relies on Explorer's undocumented WorkerW behavior and may need adjustment for future Windows versions.

The wallpaper binary also requires the .NET 10 Desktop Runtime. It does not install or replace the `.scr` screen saver.

On newer Windows raised desktops, the wallpaper uses opaque layered windows under Progman, between the desktop icons and the system wallpaper. Older desktop layouts use the separate WorkerW host. `--diagnose-shell <output-path>` writes the current desktop window hierarchy without starting another wallpaper instance; `--smoke-test <output-path>` runs an eight-second attachment/rendering check and exits. These checks do not substitute for verifying the visible desktop.
