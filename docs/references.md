# 效果参考与首版取舍

研究日期：2026-09-22。以下是产品效果参考，FloatDock 没有引入这些项目的代码或资源。

| 项目 | 已查阅内容 | FloatDock 的借鉴点 |
| --- | --- | --- |
| [Nexus](https://www.winstep.net/nexus.asp) | 官方首页图片、演示视频片段与效果说明 | 鼠标附近形成连续放大波峰；将悬停、点击和运行状态分开表达 |
| [MyDockFinder](https://www.mydockfinder.com/index_en) | 官方页面及视频画面中的底部 Dock、功能说明 | 紧凑图标排列、清晰底部状态提示；窗口预览列入后续范围 |
| [Seelen UI](https://github.com/eythaann/Seelen-UI) | README 的 Dock、主题与窗口管理功能说明；未进行本地体验 | 独立 Dock 与可定制主题的产品组织方式 |
| [TaskbarX](https://github.com/ChrisAnd1998/TaskbarX) | 官方 README 的居中动画和系统版本限制 | 作为原生任务栏改造路线的比较，不作为本项目首版实现基础 |
| [AF Media Bar](https://github.com/Fervent-Tempo/AF-Media-Bar) · AmorFate | 用户提供的[抖音视频](https://www.douyin.com/video/7687863850850356495)中紧凑媒体条与展开卡片画面；官方 README 与 MIT 许可证 | v0.2 媒体胶囊、封面信息、悬停播放控制、进度与同步歌词；未复制其代码或素材 |

这些参考的动画时长没有逐帧测量。本项目下列数值是自行选择的初始参数，需要真人体验后调整：

- 基础图标 44 DIP，间距 24 DIP；支持 32 / 44 / 56 / 64。
- 悬停最大放大 1.28 倍，上移 16 DIP；相邻图标按距离平滑衰减。
- 悬停过渡 180 ms；点击弹跳 390 ms，有一次轻回弹。
- 激活窗口所属图标上移 5 DIP，运行点变为浅蓝短条。
- 默认没有底板，右键可切换深色半透明圆角底板。
- 开启减少动画或 Windows 关闭客户端区域动画后，保留状态指示，停止缩放与跳动。

首版重点是纯图标、轻微浮动和可调运动。窗口预览、图标拖拽排序、自动躲避普通窗口、跨屏 Dock、主题包与独立应用标识分组保留到后续版本。

AF Media Bar 原项目为 MIT，署名 Copyright (c) 2026 AmorFate。FloatDock 的媒体实现独立编写，仅借鉴交互方向；本版未实现参考视频里的音频设备切换、每应用音量和频谱。卡片与胶囊示例图片由本项目自己的 WPF 测试控件使用合成歌曲和歌词渲染。

媒体技术依据：[Microsoft GSMTC](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager)、[播放进度命令](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession.trychangeplaybackpositionasync)与 [LRCLIB API](https://lrclib.net/docs)。原生集成测试通过 [SMTC GetForWindow](https://learn.microsoft.com/en-us/windows/win32/api/systemmediatransportcontrolsinterop/nf-systemmediatransportcontrolsinterop-isystemmediatransportcontrolsinterop-getforwindow) 发布自身的无声测试会话。
