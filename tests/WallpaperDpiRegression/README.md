# Wallpaper mixed-DPI regression

Run on an unlocked Windows desktop with at least two monitors using different scale factors. Include a monitor above or left of the primary monitor to cover negative coordinates. Exit the normal wallpaper application first; the test holds its single-instance mutex.

```powershell
dotnet run --project tests/WallpaperDpiRegression/WallpaperDpiRegression.csproj -c Release
```

This is an interactive integration test: it temporarily displays wallpaper windows and opens/closes Settings three times. It does not save settings or change login startup. It uses the application's generated startup configuration and actual WPF window-close dispatch, so rebuilding runs inside the window's native DPI callback context. It then rebuilds from unaware, system-aware, per-monitor and per-monitor-v2 caller contexts and checks that each caller context is restored.

Expected geometry is independently enumerated in physical pixels. Every monitor must have exactly one wallpaper window matching its full rectangle and Explorer parent. Nonzero exit status indicates a mismatch or test error. A single-monitor run cannot establish mixed-monitor coverage. Check desktop appearance separately; matching rectangles alone does not establish visible layer order or rendering quality. Actual Save-button input, sign-in startup and Explorer restart are not exercised by this test.
