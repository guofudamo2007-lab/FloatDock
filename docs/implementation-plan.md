# FloatDock initial implementation

Goal: a public MIT repository and runnable Windows floating icon dock.

Status: initial prototype implemented; build, core tests, WPF/Win32 regressions and packaged startup checks passed. Manual desktop acceptance remains open; see [validation.md](validation.md).

Architecture: isolate deterministic dock behavior in a .NET library, then connect WPF rendering to Win32 window information. Use only the installed .NET SDK and framework libraries.

Spec: [design.md](design.md).

1. Establish the public repository and MIT license. Add solution, reproducible SDK selection, documentation and ignore rules.
2. Write behavior tests for case-insensitive merging, pinned ordering, inaccessible processes, window cycling, motion and viewport clamping. Run them against unimplemented behavior and record the failure. Implement the core until tests pass.
3. Implement transparent WPF dock, icons, window switching, motion, context menu and preference persistence. Build after integration. Keep shell operations reversible and scope them to the selected application.
4. Build Release, run regressions, publish Windows x64 and check startup. Document manual acceptance boundaries. Add Windows CI that repeats the same checks, commit and push the working prototype.

Verification commands:

```powershell
dotnet build FloatDock.slnx -c Release
dotnet run --project tests/FloatDock.Core.Tests -c Release
dotnet run --project tests/FloatDock.Windows.Tests -c Release
dotnet publish src/FloatDock.Windows -c Release -r win-x64 --self-contained false -o artifacts/win-x64
```
