# FloatDock

**让 Windows 的应用图标轻轻浮起来。**

A minimal, open-source floating icon dock for Windows.

[![Windows build](https://github.com/guofudamo2007-lab/FloatDock/actions/workflows/build.yml/badge.svg)](https://github.com/guofudamo2007-lab/FloatDock/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

FloatDock 是一个独立浮动图标栏。默认只显示图标，提供悬停放大、邻近联动、选中上浮和点击回弹，可以配合 Windows 任务栏自动隐藏使用。

**状态：v0.1 开发原型。尚未发布稳定版；动画手感、真实桌面交互和不同缩放配置仍需体验验证。**

## 首版功能

- 透明无边框浮动栏，主屏幕底部居中。
- 鼠标靠近时图标平滑放大和上浮，相邻图标轻微联动。
- 活动应用上浮与浅蓝指示条，运行中的应用显示状态点。
- 真实应用图标、固定应用、识别运行中的窗口。
- 点击启动或切换应用；同一程序多窗口循环切换，右键可选具体窗口。
- 可选圆角半透明底板、四档图标尺寸、减少动画。
- 图标过多时滚轮横向浏览；全屏应用前台时隐藏 Dock。
- 本地 JSON 配置；无需管理员权限，无遥测。

## 运行

目标平台为 Windows 10/11，当前交付构建为 Windows x64，需要 **.NET 10 Desktop Runtime**。开发需要 **.NET 10 SDK**。

```powershell
git clone https://github.com/guofudamo2007-lab/FloatDock.git
cd FloatDock
dotnet run --project src/FloatDock.Windows
```

右键任意图标可添加应用、固定/取消固定、调整外观或退出。初始固定文件资源管理器。应用固定目前支持本地 `.exe`，不接受任意命令字符串。

想让桌面只剩浮动图标：右键图标 → **Windows 任务栏设置** → 自行启用系统任务栏自动隐藏。FloatDock 不会修改或隐藏系统任务栏。

配置位置：`%LOCALAPPDATA%\FloatDock\settings.json`。无开机启动项，关闭即可停止运行。

## 构建与检查

```powershell
dotnet build FloatDock.slnx -c Release
dotnet run --project tests/FloatDock.Core.Tests -c Release
dotnet run --project tests/FloatDock.Windows.Tests -c Release
dotnet publish src/FloatDock.Windows -c Release -r win-x64 --self-contained false -o artifacts/win-x64
powershell -ExecutionPolicy Bypass -File scripts/smoke.ps1
```

发布目录里的 `FloatDock.exe` 可直接运行；分发时保留整个目录。GitHub Actions 也提供构建产物，需要对应 Desktop Runtime。

核心测试检查应用合并、窗口轮换、动画目标及屏幕宽度约束。启动检查只证明进程和主窗口建立，**不代表动画观感、点击切换或全屏体验已经通过人工验收**。

## 当前边界

- 只在主屏幕创建一个 Dock；多屏和混合 DPI 尚未完成验收。
- 按 EXE 路径分组，UWP、宿主进程应用与浏览器 PWA 的身份识别仍有限。
- 尚无系统托盘、缩略图预览、Jump List、拖拽排序、主题包和自动启动。
- 普通最大化窗口可能被 Dock 遮挡；后续增加自动躲避与独立自动隐藏。
- 底板为半透明颜色，不宣称实现系统模糊或 Mica。

## 参考与贡献

视觉及交互参考 Nexus、MyDockFinder，功能组织参考 Seelen UI。具体来源和取舍见 [效果参考](docs/references.md)。本项目代码独立实现。

阅读 [设计说明](docs/design.md)、[实现计划](docs/implementation-plan.md)、[验证记录](docs/validation.md) 和 [贡献指南](CONTRIBUTING.md)。

## License

[MIT](LICENSE)
