# FloatDock

**让 Windows 的应用图标轻轻浮起来。**

A minimal, open-source floating icon dock with media controls for Windows.

[![Windows build](https://github.com/guofudamo2007-lab/FloatDock/actions/workflows/build.yml/badge.svg)](https://github.com/guofudamo2007-lab/FloatDock/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

FloatDock 是一个独立浮动图标栏。默认只显示图标，提供悬停放大、邻近联动、选中上浮和点击回弹，可以配合 Windows 任务栏自动隐藏使用。

**状态：v0.2 开发原型。尚未发布稳定版；动画手感、第三方播放器兼容性和不同缩放配置仍需体验验证。**

## 已有功能

- 透明无边框浮动栏，主屏幕底部居中。
- 鼠标靠近时图标平滑放大和上浮，相邻图标轻微联动。
- 活动应用上浮与浅蓝指示条，运行中的应用显示状态点。
- 真实应用图标、固定应用、识别运行中的窗口。
- 点击启动或切换应用；同一程序多窗口循环切换，右键可选具体窗口。
- 可选圆角半透明底板、四档图标尺寸、减少动画。
- 图标过多时滚轮横向浏览；全屏应用前台时隐藏 Dock。
- 可关闭的媒体胶囊：封面、歌曲信息、上一首 / 播放暂停 / 下一首。
- 展开卡片切换媒体来源、拖动播放进度；支持本地 LRC 同步歌词及增强 LRC 的逐词高亮。
- 在线歌词匹配可选，默认关闭；未支持的播放器控制自动禁用。
- 本地 JSON 配置；无需管理员权限，无遥测。

## 媒体胶囊

参考 [AF Media Bar](https://github.com/Fervent-Tempo/AF-Media-Bar) 的紧凑媒体条与悬浮卡片，将控制器放在 Dock 左侧。启动支持 Windows 系统媒体控制的播放器后自动读取歌曲信息。鼠标悬停显示控制按钮，点击空白或封面展开卡片，滚轮切换来源。右键应用图标 → **媒体胶囊** 可关闭整个组件。

![媒体胶囊合成渲染示例](docs/assets/media-capsule.png)

![媒体卡片合成渲染示例](docs/assets/media-card.png)

上图是程序真实 WPF 控件的合成数据渲染，用于展示界面；不是第三方播放器实测截图。

卡片中可为当前歌曲导入 `.lrc`；切歌或切换来源会清除该本地歌词。普通 LRC 按行同步，带 `<分:秒>` 时间戳的增强 LRC 按提供的词时间高亮。展开卡片勾选 **在线匹配歌词（LRCLIB）** 后，会把曲名、歌手、专辑和时长发至 `lrclib.net`，设置会保留；关闭选项即停止后续在线请求。没有歌词或匹配失败时仍显示歌曲信息。

## 运行

目标平台为 Windows 10 1809 及以上 / Windows 11，当前交付构建为 Windows x64，需要 **.NET 10 Desktop Runtime**。开发需要 **.NET 10 SDK**，首次还原会下载 Windows SDK 的 .NET 引用包。

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

媒体测试还覆盖歌词时间戳、暂停/倍速/跳转、控制命令路由、过期异步响应和 WPF 控件。可在交互式 Windows 桌面额外运行以下原生集成检查：它只发布并控制自身的无声测试会话，退出时清理。

```powershell
dotnet run --project tests/FloatDock.Windows.Tests -c Release -- --media-integration
```

## 当前边界

- 只在主屏幕创建一个 Dock；多屏和混合 DPI 尚未完成验收。
- 按 EXE 路径分组，UWP、宿主进程应用与浏览器 PWA 的身份识别仍有限。
- 尚无系统托盘、缩略图预览、Jump List、拖拽排序、主题包和自动启动。
- 普通最大化窗口可能被 Dock 遮挡；后续增加自动躲避与独立自动隐藏。
- 底板为半透明颜色，不宣称实现系统模糊或 Mica。
- 媒体功能依赖播放器提供 GSMTC 会话、控制能力和进度；本机原生测试通过不代表所有音乐软件均兼容。
- 本版不含音频频谱、音频输出设备切换或每应用音量控制，尚未完整复刻 AF Media Bar 的全部功能。

## 参考与贡献

视觉及交互参考 Nexus、MyDockFinder、AF Media Bar，功能组织参考 Seelen UI。具体来源和取舍见 [效果参考](docs/references.md) 和 [媒体胶囊设计](docs/media-capsule.md)。本项目代码独立实现。

阅读 [设计说明](docs/design.md)、[实现计划](docs/implementation-plan.md)、[验证记录](docs/validation.md) 和 [贡献指南](CONTRIBUTING.md)。

## License

[MIT](LICENSE)
