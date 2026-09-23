# Native taskbar fusion implementation plan

Goal: combine native Windows taskbar styling and dock animation with a compact embedded media controller, without running or changing the user's desktop during development.

Architecture: Windhawk owns native taskbar appearance/animation; FloatDock owns a native taskbar child media surface and a visible control window. The previous standalone dock remains explicitly selectable. Source-derived algorithms stay separate from Win32 adapters so they can be checked offline.

Sources: pin Windhawk mods at 8b459f67315c49b41708d77606e37cb187931ba5 and AF Media Bar at 1495d6e8e180a51779b74a8942f8fdbbb98bd19f. AF Media Bar's root is MIT, but its TaskbarDockService explicitly derives from GPL-3.0-or-later FluentFlyout. Preserve this chain and distribute the combined derived work under GPL-3.0-or-later, retaining prior MIT notices.

- [x] Vendor the pinned animation/styler modules, author credits and applicable licenses; create one coordinated native settings profile.
- [x] Port AF's free-range and visibility policies; embed the existing media control in a taskbar child, avoiding occupied native controls and hiding on uncertain geometry.
- [x] Add a default control window with native mode, explicit start/stop, profile access and visible exit. Keep standalone mode optional.
- [x] Port the cosine magnification/neighbor displacement into the optional dock so both modes share the same motion direction.
- [x] Add offline behavior tests and unattached WPF layout checks. Do not use live SMTC, taskbar scripts, screenshots of the desktop or launch any user-facing app.
- [x] Build and review; provide a repeatable packaging script for app, source ZIP, presets and licenses. Mark live native integration and appearance acceptance as unverified.

Validation at implementation commit: Release build has zero warnings/errors; 15/15 core cases pass; offline WPF media/dock/control-window tests pass. Source review found a missing native-parent visibility check, now fixed so a hidden taskbar also closes the separate media popup. Package/release outputs are generated from the clean commit by scripts/package.ps1 and reported separately.

Deferred by explicit user constraint: installation/enabling Windhawk, native media mounting, live playback, Explorer/fullscreen/DPI integration and human appearance acceptance. None are inferred from offline tests.
