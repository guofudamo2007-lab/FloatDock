# Contributing

FloatDock is an early Windows dock prototype. Small focused contributions are welcome.

1. Install the .NET 10 SDK on Windows.
2. Run `dotnet build FloatDock.slnx -c Release`.
3. Run `dotnet run --project tests/FloatDock.Core.Tests -c Release`.
4. Run `dotnet run --project tests/FloatDock.Windows.Tests -c Release` for the WPF rendering regression.
5. Test the desktop app with `dotnet run --project src/FloatDock.Windows`.

Keep deterministic behavior in `FloatDock.Core`, Win32 integration in the Windows project, and explain the user-visible change in your pull request. Add behavioral regression coverage for logic changes. For visual changes include before/after captures and the display scale; do not commit private window titles or personal settings.

Report Windows build, display scale, monitor layout and reproduction steps for bugs. Do not change taskbar visibility, autostart, system settings or elevated privileges without an explicit user setting. Runtime configuration and generated packages belong outside source control.

Contributions are under the repository's MIT license. Reference product interactions in `docs/references.md`; do not copy third-party code or artwork without compatible licensing and attribution.
