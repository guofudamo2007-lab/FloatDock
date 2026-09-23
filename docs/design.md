# FloatDock v0.3 architecture

Default entrypoint is FusionWindow, a visible control window. Its constructor has no native taskbar mutations. Start media creates NativeMediaHost; the optional standalone button creates DockWindow. The two modes are mutually exclusive. Closing the control window closes its child surfaces.

## Native fusion

Windhawk separately owns native Explorer styling and animation, using vendored pinned modules and the YAML profile under integrations/windhawk. Installing/enabling/disabling those modules is explicit in Windhawk. FloatDock exit does not manage their lifecycle.

NativeMediaHost adapts AF Media Bar's GPL-derived WS_CHILD/SetParent attachment. It attaches only after explicit Show. A single asynchronous UI Automation probe records taskbar buttons/list items and the native tray; stale/failed results hide the surface. Pure NativeTaskbarPolicy merges occupied intervals, chooses a sufficient free interval, preserves stable placement, and waits for stable geometry after movement. A 32 DIP buffer reduces contact with animated neighbors. Hidden parents, missing/vertical taskbars, undersized intervals and failed placement hide the surface and close the popup. This conservative policy is not proof of compatibility with every Explorer/customization version.

The host changes its own styles/parent only; it does not edit taskbar preferences, registry or Explorer code. Its Window child is detached on close. Windhawk's independently enabled modules do execute within Explorer.

## Optional independent dock

Explicit button or --standalone. DesktopPlacement registers an appbar and TaskbarGuard temporarily enables system taskbar auto-hide with an original-state recovery record and watchdog. The visible Exit closes this Dock; the control window, if present, remains available. --restore-taskbar uses a recovery record only, and is a no-op without one.

WPF icons use ported cosine falloff and cumulative neighbor displacement. App enumeration, window cycling, original launch bounce, fullscreen hiding and local preferences remain available. Reduced motion resets scale, lift, bounce and displacement. System tray/Jump List/previews are supplied by Windows in native mode, not reimplemented by the standalone mode.

## Media and validation

FloatDock.Core.Media owns source selection, timelines and LRC parsing; WindowsMediaBackend supplies GSMTC and MediaCapsule supplies WPF views. Online lyrics remain opt-in. See media-capsule.md and THIRD-PARTY-NOTICES.md.

Core tests exercise source-independent behavior. Default Windows tests construct unattached controls and rasterize fixtures without Window.Show or live media requests. Native startup/placement and real taskbar recovery tests require explicit desktop flags and are excluded from the normal pipeline.

This iteration was built and checked offline only. Live native media attachment, Explorer restart, fullscreen, DPI, animation clipping, accessibility interaction and appearance acceptance remain unverified. Source review and successful compilation are not desktop acceptance.