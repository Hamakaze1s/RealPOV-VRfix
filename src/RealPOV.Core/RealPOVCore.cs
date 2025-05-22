using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealPOV.Core
{
    public abstract class RealPOVCore : BaseUnityPlugin
    {
        public const string GUID = "hamakaze1s.realpov.vrfix"; // Fork from "keelhauled.realpov"
        public const string PluginName = "RealPOV VR Fix";

        protected const string SECTION_GENERAL = "General";
        protected const string SECTION_HOTKEYS = "Keyboard shortcuts";

        internal static ConfigEntry<float> ViewOffset { get; set; }
        internal static ConfigEntry<float> VRViewOffset { get; set; }
        internal static ConfigEntry<float> DefaultFOV { get; set; }
        internal static ConfigEntry<float> MouseSens { get; set; }
        internal static ConfigEntry<KeyboardShortcut> POVHotkey { get; set; }

        protected static bool POVEnabled;
        protected static float? CurrentFOV;
        protected static readonly Dictionary<GameObject, Vector3> LookRotation = new Dictionary<GameObject, Vector3>();
        protected static GameObject currentCharaGo;
        protected static Camera GameCamera;
        protected static float defaultViewOffset = 0.05f;
        protected static float defaultVRViewOffset = 0.01f;
        protected static float defaultFov = 70f;

        private static float backupFOV;
        private static float backupNearClip;
        private static bool allowCamera; // Controls if mouse input is processed for camera control
        private bool mouseButtonDown0; // Left mouse button state
        private bool mouseButtonDown1; // Right mouse button state

        // IsVREnabled() will be overridden in RealPOV.Koikatu
        protected virtual bool IsVREnabled()
        {
            return false;
        }

        protected virtual void Awake()
        {
            Log.SetLogSource(Logger);

            POVHotkey = Config.Bind(SECTION_HOTKEYS, "Toggle POV", new KeyboardShortcut(KeyCode.Backspace));
            DefaultFOV = Config.Bind(SECTION_GENERAL, "Default FOV", defaultFov, new ConfigDescription("", new AcceptableValueRange<float>(20f, 120f)));
            MouseSens = Config.Bind(SECTION_GENERAL, "Mouse sensitivity", 1f, new ConfigDescription("", new AcceptableValueRange<float>(0.1f, 2f)));
            ViewOffset = Config.Bind(SECTION_GENERAL, "View offset", defaultViewOffset, new ConfigDescription("Move the camera backward or forward", new AcceptableValueRange<float>(-0.5f, 0.5f)));
            VRViewOffset = Config.Bind(SECTION_GENERAL, "VR View offset", defaultVRViewOffset, new ConfigDescription("Move the VR camera backward or forward", new AcceptableValueRange<float>(-0.5f, 0.5f)));
        }

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

        private void LateUpdate()
        {
            if (POVEnabled)
            {
                // In VR, mouse input should not control rotation (it's handled by HMD) or FOV.
                // It can still be used for UI interaction if needed, but not for camera movements.
                // We will only allow mouse input for camera/FOV adjustment if NOT in VR mode.
                if (!IsVREnabled()) // Only allow mouse control if not in VR mode
                {
                    if (!allowCamera)
                    {
                        if (GUIUtility.hotControl == 0 && !EventSystem.current.IsPointerOverGameObject())
                        {
                            if (Input.GetMouseButtonDown(0))
                            {
                                mouseButtonDown0 = true;
                                allowCamera = true;
                                if (GameCursor.IsInstance())
                                    GameCursor.Instance.SetCursorLock(true);
                            }

                            if (Input.GetMouseButtonDown(1))
                            {
                                mouseButtonDown1 = true;
                                allowCamera = true;
                                if (GameCursor.IsInstance())
                                    GameCursor.Instance.SetCursorLock(true);
                            }
                        }
                    }

                    if (allowCamera)
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
                                    GameCursor.Instance.SetCursorLock(false);
                            }
                        }
                    }
                }
                else
                { // In VR mode, ensure allowCamera is true to permit other plugins/game to interact with UI,
                  // but RealPOV itself won't process mouse inputs for camera adjustments.
                  // Also ensure cursor is unlocked by RealPOV.
                    allowCamera = true;
                    if (GameCursor.IsInstance())
                        GameCursor.Instance.SetCursorLock(false);
                }


                // --- MODIFIED: Mouse input for rotation and FOV is ONLY processed if allowCamera is true (i.e., not in VR) ---
                if (allowCamera && !IsVREnabled()) // Only process mouse for camera adjustments if NOT in VR
                {
                    if (mouseButtonDown0) // Mouse Left Button: Character head rotation (non-VR only)
                    {
                        if (LookRotation.ContainsKey(currentCharaGo))
                        {
                            var x = Input.GetAxis("Mouse X") * MouseSens.Value;
                            var y = -Input.GetAxis("Mouse Y") * MouseSens.Value;
                            LookRotation[currentCharaGo] += new Vector3(y, x, 0f);
                        }
                    }
                    else if (mouseButtonDown1) // Mouse Right Button: FOV adjustment (non-VR only)
                    {
                        CurrentFOV += Input.GetAxis("Mouse X");
                    }
                }
                // --- END MODIFIED ---
            }
        }

        protected virtual void EnablePov()
        {
            POVEnabled = true;

            if (GameCamera != null && !IsVREnabled())
            {
                backupFOV = GameCamera.fieldOfView;
                backupNearClip = GameCamera.nearClipPlane;

                if (CurrentFOV == null)
                    CurrentFOV = DefaultFOV.Value;
            }
            else if (IsVREnabled())
            {
                Log.Message("RealPOV: VR enabled. Not touching FOV/NearClipPlane.");
                CurrentFOV = null;
            }
        }

        protected virtual void DisablePov()
        {
            currentCharaGo = null;
            POVEnabled = false;

            if (GameCamera != null)
            {
                if (!IsVREnabled())
                {
                    GameCamera.fieldOfView = backupFOV;
                    GameCamera.nearClipPlane = backupNearClip;
                }
                else
                {
                    Log.Message("RealPOV: VR enabled. Not restoring FOV/NearClipPlane.");
                }

                if (GameCursor.IsInstance())
                    GameCursor.Instance.SetCursorLock(false);
            }
        }
    }
}