using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealPOV.Core
{
    /// <summary>
    /// Base class for RealPOV plugin, providing core functionality for first-person perspective.
    /// Handles configuration, POV toggle, camera adjustments, and mouse input processing.
    /// </summary>
    public abstract class RealPOVCore : BaseUnityPlugin
    {
        // Plugin identification constants.
        public const string GUID = "hamakaze1s.realpov.vrfix"; // Forked from "keelhauled.realpov"
        public const string PluginName = "RealPOV VR Fix";

        // Configuration section names.
        protected const string SECTION_GENERAL = "General";
        protected const string SECTION_HOTKEYS = "Keyboard shortcuts";

        // Configuration entries.
        internal static ConfigEntry<float> ViewOffset { get; set; } // Camera offset for non-VR mode.
        internal static ConfigEntry<float> VRViewOffset { get; set; } // Camera offset for VR mode.
        internal static ConfigEntry<float> DefaultFOV { get; set; } // Default Field of View.
        internal static ConfigEntry<float> MouseSens { get; set; } // Mouse sensitivity for camera rotation.
        internal static ConfigEntry<KeyboardShortcut> POVHotkey { get; set; } // Hotkey to toggle POV.

        // Static state variables for POV.
        protected static bool POVEnabled; // True if POV mode is active.
        protected static float? CurrentFOV; // Current FOV value, nullable for dynamic control.
        protected static readonly Dictionary<GameObject, Vector3> LookRotation = new Dictionary<GameObject, Vector3>(); // Stores character's head rotation.
        protected static GameObject currentCharaGo; // GameObject of the current character in POV.
        protected static Camera GameCamera; // Reference to the main game camera.

        // Default values for settings.
        protected static float defaultViewOffset = 0.05f;
        protected static float defaultVRViewOffset = 0.01f;
        protected static float defaultFov = 70f;

        // Camera state backups.
        private static float backupFOV; // Backup of original camera FOV.
        private static float backupNearClip; // Backup of original camera near clip plane.

        // Mouse input handling state for non-VR.
        private static bool allowCamera; // Controls if mouse input is processed for camera control.
        private bool mouseButtonDown0; // Left mouse button state.
        private bool mouseButtonDown1; // Right mouse button state.

        /// <summary>
        /// Determines if VR is currently enabled. This method is intended to be overridden
        /// by derived classes to provide game-specific VR detection.
        /// </summary>
        /// <returns>True if VR is active; otherwise, false.</returns>
        protected virtual bool IsVREnabled()
        {
            return false;
        }

        /// <summary>
        /// Initializes plugin configurations.
        /// </summary>
        protected virtual void Awake()
        {
            Log.SetLogSource(Logger); // Initialize BepInEx logger.

            // Bind configuration entries.
            POVHotkey = Config.Bind(SECTION_HOTKEYS, "Toggle POV", new KeyboardShortcut(KeyCode.Backspace));
            DefaultFOV = Config.Bind(SECTION_GENERAL, "Default FOV", defaultFov, new ConfigDescription("", new AcceptableValueRange<float>(20f, 120f)));
            MouseSens = Config.Bind(SECTION_GENERAL, "Mouse sensitivity", 1f, new ConfigDescription("", new AcceptableValueRange<float>(0.1f, 2f)));
            ViewOffset = Config.Bind(SECTION_GENERAL, "View offset", defaultViewOffset, new ConfigDescription("Move the camera backward or forward", new AcceptableValueRange<float>(-0.5f, 0.5f)));
            VRViewOffset = Config.Bind(SECTION_GENERAL, "VR View offset", defaultVRViewOffset, new ConfigDescription("Move the VR camera backward or forward", new AcceptableValueRange<float>(-0.5f, 0.5f)));
        }

        /// <summary>
        /// Handles hotkey input for toggling POV mode.
        /// </summary>
        private void Update()
        {
            if (POVHotkey.Value.IsDown())
            {
                if (POVEnabled)
                    DisablePov();
                else
                    EnablePov();
            }
        }

        /// <summary>
        /// Processes mouse input for camera control and FOV adjustment in non-VR mode.
        /// Manages cursor locking and prevents conflicts with UI.
        /// </summary>
        private void LateUpdate()
        {
            if (POVEnabled)
            {
                // In VR mode, mouse input is generally not used for camera rotation/FOV.
                // It is instead used for UI interaction or other plugin features.
                if (!IsVREnabled())
                {
                    // If not in VR, manage camera input focus.
                    if (!allowCamera)
                    {
                        // Check if mouse is over UI or if GUI has hot control.
                        if (GUIUtility.hotControl == 0 && !EventSystem.current.IsPointerOverGameObject())
                        {
                            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                            {
                                // Activate camera control when mouse buttons are pressed.
                                mouseButtonDown0 = Input.GetMouseButtonDown(0);
                                mouseButtonDown1 = Input.GetMouseButtonDown(1);
                                allowCamera = true;
                                if (GameCursor.IsInstance())
                                    GameCursor.Instance.SetCursorLock(true); // Lock cursor for camera control.
                            }
                        }
                    }
                    else // If camera input is allowed, check for mouse button releases to disable.
                    {
                        bool mouseUp0 = Input.GetMouseButtonUp(0);
                        bool mouseUp1 = Input.GetMouseButtonUp(1);

                        if ((mouseButtonDown0 || mouseButtonDown1) && (mouseUp0 || mouseUp1))
                        {
                            if (mouseUp0) mouseButtonDown0 = false;
                            if (mouseUp1) mouseButtonDown1 = false;

                            if (!mouseButtonDown0 && !mouseButtonDown1)
                            {
                                allowCamera = false;
                                if (GameCursor.IsInstance())
                                    GameCursor.Instance.SetCursorLock(false); // Unlock cursor.
                            }
                        }
                    }
                }
                else // In VR mode.
                {
                    // Ensure allowCamera is true to permit other plugins/game to interact with UI.
                    // RealPOV itself won't process mouse inputs for camera adjustments in VR.
                    allowCamera = true;
                    if (GameCursor.IsInstance())
                        GameCursor.Instance.SetCursorLock(false); // Ensure cursor is unlocked.
                }

                // Process mouse input for rotation and FOV ONLY if allowCamera is true (i.e., not in VR).
                if (allowCamera && !IsVREnabled())
                {
                    if (mouseButtonDown0) // Left Mouse Button: Character head rotation (non-VR only).
                    {
                        if (LookRotation.ContainsKey(currentCharaGo))
                        {
                            var x = Input.GetAxis("Mouse X") * MouseSens.Value;
                            var y = -Input.GetAxis("Mouse Y") * MouseSens.Value;
                            LookRotation[currentCharaGo] += new Vector3(y, x, 0f);
                        }
                    }
                    else if (mouseButtonDown1) // Right Mouse Button: FOV adjustment (non-VR only).
                    {
                        CurrentFOV += Input.GetAxis("Mouse X");
                    }
                }
            }
        }

        /// <summary>
        /// Activates POV mode. Backs up camera settings and sets initial FOV.
        /// </summary>
        protected virtual void EnablePov()
        {
            POVEnabled = true;

            if (GameCamera != null && !IsVREnabled())
            {
                // Backup non-VR camera settings.
                backupFOV = GameCamera.fieldOfView;
                backupNearClip = GameCamera.nearClipPlane;

                // Set initial FOV if not already set.
                if (CurrentFOV == null)
                    CurrentFOV = DefaultFOV.Value;
            }
            else if (IsVREnabled())
            {
                Logger.LogMessage("RealPOV: VR enabled. Not touching FOV/NearClipPlane; VR plugin handles this.");
                CurrentFOV = null; // Let VRGIN handle FOV.
            }
        }

        /// <summary>
        /// Deactivates POV mode. Restores original camera settings and releases character reference.
        /// </summary>
        protected virtual void DisablePov()
        {
            currentCharaGo = null; // Clear character reference.
            POVEnabled = false;

            if (GameCamera != null)
            {
                if (!IsVREnabled())
                {
                    // Restore non-VR camera settings.
                    GameCamera.fieldOfView = backupFOV;
                    GameCamera.nearClipPlane = backupNearClip;
                }
                else
                {
                    Logger.LogMessage("RealPOV: VR enabled. Not restoring FOV/NearClipPlane; VR plugin handles this.");
                }

                // Ensure cursor is unlocked when exiting POV.
                if (GameCursor.IsInstance())
                    GameCursor.Instance.SetCursorLock(false);
            }
        }
    }
}