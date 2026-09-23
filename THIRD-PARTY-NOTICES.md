# Third-party notices

FloatDock v0.3 combines original FloatDock code and source-derived components. The combined FloatDock application is distributed under **GPL-3.0-or-later**. Earlier FloatDock code retains its original MIT permission and copyright notice in `licenses/FloatDock-original-MIT.txt`. Modified files and this integration were prepared on 2026-09-23. There is no upstream endorsement.

## Windhawk modules

Repository: https://github.com/ramensoftware/windhawk-mods

Pinned revision: `8b459f67315c49b41708d77606e37cb187931ba5`.

- `mods/windows-11-taskbar-styler.wh.cpp`, version 1.10, author **m417z**. Its source explicitly specifies GNU GPL v3.0; the vendored module remains under that upstream license. Full text: `licenses/GPL-3.0.txt`.
- `mods/taskbar-dock-animation-plus.wh.cpp`, version 2.0.1, author **incconutwo**, fork of **Ph0en1x-dev**'s Taskbar Dock Animation. No file-specific alternative license is declared; the upstream repository's [contribution license policy](https://github.com/ramensoftware/windhawk-mods/blob/8b459f67315c49b41708d77606e37cb187931ba5/README.md) assigns MIT to such mods. See `licenses/Taskbar-Dock-Animation-MIT.txt`.

Both complete modules are vendored without functional modifications under `integrations/windhawk/modules/`. Local serialized file hashes are recorded in `PINNED-SOURCES.json`. They run only when installed and enabled in Windhawk, and are not built into the FloatDock executable.

The cosine falloff and centered cumulative neighbor-displacement algorithms from Animation Plus's `CalculateScale` and `ApplyAnimation` are also ported to `FloatDock.Core/DockModel.cs`, with full neighbor spacing in the standalone dock. Its WPF transition and launch bounce remain FloatDock implementations.

Styling uses Styler's built-in **DockLike**, credited upstream to **Amber**, with FloatDock floating-margin, radius and animation settings. Reference: https://github.com/ramensoftware/windows-11-taskbar-styling-guide/blob/main/Themes/DockLike/README.md . No external screenshots or icons are bundled.

## AF Media Bar and FluentFlyout

Repository: https://github.com/Fervent-Tempo/AF-Media-Bar

Pinned revision: `1495d6e8e180a51779b74a8942f8fdbbb98bd19f`. Root MIT notice: **Copyright (c) 2026 AmorFate**, reproduced in `licenses/AF-Media-Bar-MIT.txt`.

`FloatDock.Core/NativeTaskbarPolicy.cs` adapts AF's free-range merging, stable placement and taskbar visibility/movement policies, including a stricter no-space/no-display decision. `FloatDock.Windows/Taskbar/NativeMediaHost.cs` adapts the child-window attach/detach approach from `TaskbarDockService.cs`, adding FloatDock's asynchronous UI Automation occupancy probe and WPF media surface.

**File-level license exception:** AF's `TaskbarDockService.cs` explicitly says its taskbar docking engine was ported from **ManualDinosaur/FluentFlyout**, **GPL-3.0-or-later**. This upstream declaration is retained in FloatDock's host header; the root MIT file must not be used to erase that GPL provenance. Original reference: https://github.com/ManualDinosaur/FluentFlyout (not independently retrievable when this integration was prepared). Source declaration at the pinned AF revision is the evidence used for this license chain. GPL text is included in `licenses/GPL-3.0.txt` and the root `LICENSE`.

FloatDock's GSMTC coordinator, lyric parser and media UI were independently implemented in v0.2. v0.3 source reuse is limited to the components listed above; AF's audio routing, per-application volume and spectrum capture are not included.

## Runtime dependencies

The build uses .NET/WPF and Microsoft.Windows.SDK.NET.Ref/WinRT projections under their upstream licenses. The portable build is framework-dependent and requires the .NET 10 Desktop Runtime. `licenses/runtime/` carries the CsWinRT MIT notice, Windows SDK package metadata and its upstream license link. See `FloatDock.deps.json` for exact runtime library versions. Corresponding FloatDock source, build steps and third-party module source are available in this public repository; archives created by `scripts/package.ps1` include a complete matching source ZIP.
