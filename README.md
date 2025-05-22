# RealPOV (Koikatu Studio VR Fix Fork)

[English] | [中文](#中文)

---

<span id="english"></span>

## RealPOV (Koikatu Studio VR Fix Fork)

This is a specialized fork of the `RealPOV` plugin from the [KeelPlugins](https://github.com/IllusionMods/KeelPlugins) collection. Its primary purpose is to enhance the first-person experience in a VR environment for **Koikatu Studio**. Currently, only basic functionality is implemented.

### Features

*   **Chara Studio VR First-Person:** Adds first-person functionality for Chara Studio VR mode.
*   **Familiar Usage:** The core usage remains consistent with the original RealPOV plugin.
*   **Default Toggle Key:** First-person mode can be toggled by pressing the `Backspace` key by default.
*   **Independent Camera Offset for Non-VR:** To address clipping issues, this allows you to set a camera position offset in the configuration file that is *independent* of VR mode settings. This provides greater flexibility for non-VR first-person views (if needed).
*   **Development Environment:** This fork was developed and tested within a Koikatu Studio environment leveraging `KoikatuStudioVRPlugin v0.0.3` and the accompanying `VRGIN_KKCS.dll`.

### How to Install

1.  **Install BepInEx:** Ensure you have the latest build of [BepInEx](https://github.com/BepInEx/BepInEx/releases) installed for your game.
2.  **Download this Plugin:** Get the latest release of *this specific RealPOV fork* from its [releases page](https://github.com/Hamakaze1s/RealPOV-VRfix/releases).
3.  **Place Plugin DLLs:** Extract the downloaded archive and place the `RealPOV` plugin DLLs into your game's `BepInEx/plugins` folder.

### Usage

1.  **Enable the Plugin:** You can enable and configure the plugin using a [Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager) or by editing the plugin's INI/config file directly.
2.  **Toggle First-Person:** In Koikatu Chara Studio, press `Backspace` to switch between third-person and first-person views.
3.  **Adjust for Clipping:** If you encounter clipping issues in first-person mode, you can try using a Configuration Manager to adjust the camera offset.

### Known Issues

*   **Initial Head Hiding:** In some environments, the character's head may not hide correctly upon initial activation of first-person mode. Toggling POV once (pressing `Backspace` again) usually resolves this by forcing a refresh. A future update is planned where this plugin will directly control head hiding to prevent this issue.

### Original Project & Credits

This plugin is a fork of the `RealPOV` plugin from the [KeelPlugins](https://github.com/IllusionMods/KeelPlugins) collection. All credit for the original work goes to its respective authors.

### License

Inherited from the original project. See [LICENSE](LICENSE) for details.

---

<br>
<br>

<span id="chinese"></span>

## RealPOV (恋活工作室 VR修复版)

这是从 [KeelPlugins](https://github.com/IllusionMods/KeelPlugins) 项目集合中`RealPOV`插件的一个专门分支。其主要目的是在VR环境下，为**恋活工作室**提供增强的第一人称体验，目前仅实现了基础功能。

### 功能特性

*   **Chara Studio VR第一人称:** 为Chara Studio VR模式添加了第一人称功能。
*   **使用方式保持一致:** 核心使用方法与原版RealPOV插件保持相同。
*   **默认切换键:** 默认情况下，可以通过按下`Backspace`键来切换第一人称模式。
*   **非VR模式独立相机偏移:** 为了解决穿模问题，允许你在配置文件中设置一个与VR模式设置**独立**的相机位置偏移量。这为非VR第一人称视角提供了更大的灵活性（如果需要）。
*   **开发环境:** 本分支是在`KoikatuStudioVRPlugin v0.0.3`及附带的`VRGIN_KKCS.dll`的恋活工作室环境下进行开发和测试的。

### 安装方法

1.  **安装BepInEx:** 确保你的游戏已安装最新版本的[BepInEx](https://github.com/BepInEx/BepInEx/releases)。
2.  **下载本插件:** 从本`RealPOV`分支的[发布页面](https://github.com/Hamakaze1s/RealPOV-VRfix/releases)下载最新版本。
3.  **放置插件DLL文件:** 解压下载的压缩包，并将`RealPOV`插件的DLL文件放置到游戏的`BepInEx/plugins`文件夹内。

### 使用方法

1.  **启用插件:** 你可以通过[Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager)或直接编辑插件的INI/配置文件来启用和配置插件。
2.  **切换第一人称:** 在恋活Chara Studio中，按下`Backspace`键即可在第三人称和第一人称视角之间切换。
3.  **调整穿模问题:** 如果你在第一人称模式下遇到穿模问题，你可以尝试使用Configuration Manager调整相机偏移量。

### 已知问题

*   **初始头部隐藏:** 在某些环境下，首次激活第一人称模式时，角色头部可能无法正确隐藏。通常通过切换一次POV（再次按下`Backspace`）强制刷新显示即可解决此问题。未来的更新计划中，本插件将直接控制头部隐藏以彻底解决此问题。

### 原项目与鸣谢

本插件是[KeelPlugins](https://github.com/IllusionMods/KeelPlugins)集合中`RealPOV`插件的一个分支。所有原版工作的归功与其各自作者。

### 许可证

继承自原项目。详情请参阅[LICENSE](LICENSE)。