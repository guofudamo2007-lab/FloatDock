# 原生任务栏融合配置

这套方案直接美化 Windows 11 原生任务栏：保留原有开始菜单、应用切换、托盘与位置，使用 DockLike 圆角浮动样式和鼠标附近的连续放大动画。FloatDock 另外提供嵌入原任务栏空白处的媒体控件。

## 使用

1. 从 [Windhawk 官方网站](https://windhawk.net/) 安装 Windhawk。
2. 安装 **Windows 11 Taskbar Styler** 和 **Taskbar Dock Animation Plus**。不要同时启用其他 Dock Animation 变体。
3. 在每个模块的 **Settings → Textual mode** 中分别粘贴同名 YAML 配置，再保存。先备份自己已有的配置。Styler 对应 `taskbar-styler.yaml`；Animation Plus 对应 `dock-animation.yaml`。
4. Windows 任务栏设置中使用居中对齐、合并按钮并隐藏标签；配置文件本身不更改这些系统设置。
5. 打开 FloatDock，点击 **启动媒体栏**。媒体栏只占用检测到的空闲范围；空间不足、任务栏移动或探测失败时会隐藏，并在控制窗口解释原因。

这里的模块源码已经随包附带，版本与哈希见 `PINNED-SOURCES.json`；正常使用优先通过 Windhawk 官方模块目录安装。只有 Windhawk 才能编译和加载 `.wh.cpp`。FloatDock 不会偷偷安装、注入或自动启用模块。

配套值为 125% 放大、100 像素影响半径、70% 邻居位移、180 ms 聚焦、650 ms 后悬停呼吸。原生动画保留上游完整实现。减少动画时，在 Windhawk 中禁用 Animation Plus；独立 Dock 的“减少动画”设置只控制独立模式。

## 停止与还原

- **停止媒体栏** 或媒体条右侧 **×** 只移除 FloatDock 媒体控件。
- 退出控制窗口会关闭 FloatDock 的媒体栏/独立 Dock。
- 原生样式与动画由 Windhawk 管理，退出 FloatDock **不会**禁用它们。在 Windhawk 中禁用上述两个模块即可移除对应效果；导回备份可恢复原来的自定义配置。
- `restore-taskbar.cmd` 仅用于独立 Dock 的任务栏接管恢复，不负责 Windhawk。

## 当前限制与验证

原生外观方案针对 Windows 11 x64；Animation Plus 上游注明不支持 StartAllBack。媒体宿主目前仅支持主屏横向任务栏，不宣称多屏或所有 Windows 更新兼容。使用 Styler 的 `clickThroughTaskbar: false`，避免任务栏裁剪区域排除嵌入的子窗口。

已检查设置键与固定版本的源代码、完成 FloatDock 编译和离线测试。本轮未在用户桌面安装/启用 Windhawk，也未挂载媒体栏；组合后的裁剪、DPI、自动隐藏和动画观感仍需在专门测试桌面验收。

浮动间距参考 [DockLike 官方指南](https://github.com/ramensoftware/windows-11-taskbar-styling-guide/blob/main/Themes/DockLike/README.md)。指南另外建议增加任务栏高度 4 像素以补偿间距；本包不自动安装第三个高度模块，若图标裁剪请先关闭动画并按指南调整。
