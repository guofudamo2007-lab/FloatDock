# Changelog

## 0.3.0 preview — 2026-09-23

- Default to a visible control window with explicit Start media / Stop media / Exit. No taskbar change occurs on ordinary startup.
- Bundle pinned Windhawk Styler and Animation Plus sources and coordinated native-taskbar settings.
- Port AF Media Bar's free-space/visibility policies and GPL-derived taskbar child hosting. Native media mode is an experimental primary-horizontal-taskbar integration, not desktop-validated in this iteration.
- Port Animation Plus's cosine falloff and cumulative neighbor displacement into the optional WPF dock.
- Distribute combined derived FloatDock under GPL-3.0-or-later, preserving original MIT and upstream notices.
- In the optional standalone mode, put the dock at the bottom of the screen, temporarily auto-hide Windows taskbar, and reserve space for maximized windows.
- Restore the original taskbar preference on normal exit or UI process termination, with a separate recovery process and persistent recovery record.
- Add visible Exit and Settings buttons, Start menu access, clock, Ctrl+Alt+Q and a recovery command.
- Unify the dock into a compact rounded surface and collapse empty media to a small button.
- Default all WPF checks to offline-only unattached rendering; require explicit flags for desktop tests. Add control-window layout, icon spacing and media placement regressions.

## 0.2.0 preview — 2026-09-22

- Add an optional media capsule with artwork, metadata, hover transport controls and an expandable player card.
- Connect Windows GSMTC source selection, capability-aware controls and seeking.
- Add local LRC lyrics, enhanced LRC word highlighting and opt-in LRCLIB lookup with metadata disclosure.
- Follow dock fullscreen hiding and reduced-motion settings; cancel pending work on source changes and shutdown.
- Add core behavior, WPF rendering and opt-in native SMTC integration tests.
- Credit AF Media Bar as the media interaction reference. Audio routing, per-app volume and spectrum are not included.

## 0.1.0 preview — 2026-09-22

- Initial independent Windows floating dock with app icons, hover magnification, active lift and launch bounce.
- Pin apps, discover running windows, switch/cycle windows and hide during fullscreen use.
- Add MIT license, Windows CI, local build and startup checks.
