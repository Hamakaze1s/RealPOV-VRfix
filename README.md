# RealPOV (Koikatu Studio VR Fix Fork)
[English](./README.md) | [中文](./README_zh.md)

---

<span id="english"></span>

## RealPOV (Koikatu Studio VR Fix Fork)

This is a specialized fork of the `RealPOV` plugin from the [KeelPlugins](https://github.com/IllusionMods/KeelPlugins) collection. Its primary purpose is to enhance the first-person experience in a VR environment for **Koikatu Studio**. Currently, only basic functionality is implemented. Its goal is to become the best VR POV plugin for Koikatu Chara Studio. 

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


