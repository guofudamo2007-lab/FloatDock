# FloatDock

独立的 Windows 浮动图标栏，默认透明、无文字标签。鼠标悬停时图标和相邻图标放大抬升；当前应用保持轻微上浮并显示底部状态点；点击有短回弹。右键可显示圆角底板。

## Architecture

- .NET 10 / WPF: transparent, borderless desktop window; WPF transforms animate icons.
- FloatDock.Core: pinned/running application merge, window cycling, motion targets and viewport sizing. No Windows dependencies.
- FloatDock.Windows: Win32 window discovery, icon extraction, foreground activation, WPF view and local JSON preferences.
- FloatDock.Core.Tests: dependency-free executable regression suite; nonzero exit on failure.

The dock stays on the primary display, above the work-area bottom with a 12 DIP gap. When there are too many icons it scrolls horizontally rather than extending beyond the display. It refreshes visible top-level windows every second and foreground state every 150 ms; fullscreen foreground apps hide the dock. The dock never edits Explorer, the registry or taskbar visibility. Users can enable Windows taskbar auto-hide themselves.

Pins point to local EXE files. Running windows with the same executable share an icon; repeated clicks cycle their windows. A process whose executable is inaccessible remains switchable as a separate window. App titles appear only in tooltips and accessibility names. No telemetry, elevation or autostart. Networking is disabled by default; v0.2 optionally queries LRCLIB after the user enables online lyrics and sees the metadata disclosure.

Preferences live in `%LOCALAPPDATA%/FloatDock/settings.json`. Invalid preferences fall back in memory with a visible warning; the invalid original is preserved until an explicit settings change. Writes use a temporary file and replacement. Closing FloatDock stops timers and releases the single-instance mutex.

v0.2 adds an optional media capsule beside the icon area. `FloatDock.Core.Media` owns source selection, timeline calculation and LRC parsing; the Windows layer connects GSMTC, cover art, WPF controls and opt-in lyric lookup. Media uses its own one-second metadata polling and a 100 ms local timeline/lyric clock. Windows commands target a selected session and honor its advertised capabilities. See [media-capsule.md](media-capsule.md).

## Validation and limitations

Build Release, execute core tests, publish Windows x64, and perform a bounded application startup check. Human acceptance is required for animation feel, real-window switching, DPI/multi-display arrangements and fullscreen behavior. v0.1 does not claim full taskbar parity: system tray, taskbar previews, jump lists, drag reorder, secondary-monitor docks and robust packaged-app grouping are future work.

## Alternatives considered

WPF provides the smallest locally buildable native Windows prototype. WinUI 3 adds packaging/runtime overhead for this initial scope. Modifying Explorer directly would couple behavior to Windows internals; the user selected the independent dock option.

Reference: https://learn.microsoft.com/dotnet/desktop/wpf/graphics-multimedia/animation-overview
