# 源码复用与效果参考

研究更新：2026-09-23。

v0.1/v0.2 的独立 Dock 与媒体界面曾以产品演示为参考。v0.3 已实际引入源码，不能再称“全部独立实现”。完整权利声明见 [第三方声明](../THIRD-PARTY-NOTICES.md)。

| 项目 | 当前使用 |
| --- | --- |
| Windhawk Windows 11 Taskbar Styler / DockLike | 完整固定版模块源码、内置主题与浮动样式配套配置 |
| Windhawk Taskbar Dock Animation Plus | 完整固定版模块源码与配套配置；移植余弦缩放、居中累计位移算法 |
| AF Media Bar | 移植空闲区域、稳定几何/可见性策略，适配其源自 FluentFlyout 的原生子窗口挂载 |
| Seelen UI | 调研整体 Shell 的功能组织，没有复制代码或素材 |
| TaskbarXI / RoundedTB / TaskbarX | 比较任务栏改造路线，没有复制代码或素材 |
| Nexus / MyDockFinder | 早期 Dock 产品效果参考，没有复制代码或素材 |

原生动画使用上游实现，本包初始配置为 125% 放大、100 影响半径、70% 邻居位移。独立 Dock 为 128% 放大、100 DIP 半径、16 DIP 上浮、180 ms WPF 过渡和原有 390 ms 启动回弹。这些值未经本轮真人观感验收。

AF 复用不能仅依据根目录 MIT 判断：其 TaskbarDockService 文件明确声明 GPL-3.0-or-later 的 FluentFlyout 来源。相关来源链、许可证、模块版本与本地哈希均随包提供。

媒体技术依据：[Microsoft GSMTC](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager)、[进度命令](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession.trychangeplaybackpositionasync)和 [LRCLIB API](https://lrclib.net/docs)。未加入音频设备切换、每应用音量或频谱；界面预览是合成数据离线渲染。