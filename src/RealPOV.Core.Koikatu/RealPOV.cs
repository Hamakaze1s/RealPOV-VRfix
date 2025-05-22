using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using KKAPI.Studio.SaveLoad;
using RealPOV.Core;
using Studio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

using KKCharaStudioVR; // VR integration for Koikatsu Studio
using VRGIN.Core; // Core VR library

[assembly: System.Reflection.AssemblyVersion(RealPOV.Koikatu.RealPOV.Version)]

namespace RealPOV.Koikatu
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KKAPI.KoikatuAPI.GUID)]
    public class RealPOV : RealPOVCore
    {
        // Plugin version, combines base version with build number.
        public const string Version = "1.4.1." + BuildNumber.Version;

        // Configuration entries for user settings.
        private ConfigEntry<bool> HideHead { get; set; }
        private ConfigEntry<PovSex> SelectedPOV { get; set; }

        // Static fields for camera/character state.
        private static int backupLayer; // Original camera layer before POV activation.
        private static ChaControl currentChara; // The character currently in POV.
        private static Queue<ChaControl> charaQueue; // Queue of characters for non-Studio mode.
        private readonly bool isStudio = Paths.ProcessName == "CharaStudio"; // Checks if running in Studio mode.
        private bool prevVisibleHeadAlways; // Original head visibility setting of the character.
        private HFlag hFlag; // Reference to HScene HFlag, for non-Studio mode.
        private static int currentCharaId = -1; // ID of the current character in POV.
        private static RealPOV plugin; // Reference to the plugin instance.

        // Depth of field (DOF) settings backup.
        private float dofOrigSize;
        private float dofOrigAperature;

        // Reflection fields for VRGIN.Core.VR.Mode.MoveToPosition.
        private static Type vrginModeType;
        private static MethodInfo vrginModeMoveToPositionMethod;
        private static bool vrginModeReflectionInitialized = false; // Flag to ensure reflection initialization runs once.

        protected override void Awake()
        {
            plugin = this;
            defaultFov = 45f; // Default Field of View.
            defaultViewOffset = 0.03f; // Default camera offset for non-VR.
            defaultVRViewOffset = 0.00f; // Default camera offset for VR.
            base.Awake();

            // Bind configuration settings.
            HideHead = Config.Bind(SECTION_GENERAL, "Hide character head", false, "When entering POV, hide the character's head. Prevents accessories and hair from obstructing the view.");
            SelectedPOV = Config.Bind(SECTION_GENERAL, "Selected POV", PovSex.Male, "Choose which sex to use as your point of view.");

            // Apply Harmony patches.
            Harmony.CreateAndPatchAll(typeof(Hooks));
            // Register for Studio save/load events to persist POV data.
            StudioSaveLoadApi.RegisterExtraBehaviour<SceneDataController>(GUID);

            // Scene change event handlers for HScene and character queue management.
            SceneManager.sceneLoaded += (arg0, scene) =>
            {
                hFlag = FindObjectOfType<HFlag>();
                charaQueue = null; // Clear character queue on new scene load.
            };
            SceneManager.sceneUnloaded += arg0 => charaQueue = null; // Clear character queue on scene unload.

            // Initialize VRGIN reflection.
            InitializeVRGINModeMoveToPositionReflection();
        }

        // Checks if VR is currently active.
        protected override bool IsVREnabled()
        {
            return VR.Active;
        }

        /// <summary>
        /// Initializes reflection for VRGIN.Core.VR.Mode.MoveToPosition method.
        /// This allows dynamic access to VR camera positioning without direct assembly reference.
        /// </summary>
        private static void InitializeVRGINModeMoveToPositionReflection()
        {
            if (vrginModeReflectionInitialized) return;

            try
            {
                // Get VRGIN_KKCS assembly, which contains VRGIN.Modes.ControlMode.
                Assembly vrginAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "VRGIN_KKCS");

                if (vrginAssembly == null)
                {
                    plugin.Logger.LogWarning("VRGIN_KKCS.dll assembly not found. VRGIN.Core.VR.Mode.MoveToPosition cannot be used.");
                    return;
                }

                // Get the base ControlMode type.
                vrginModeType = vrginAssembly.GetType("VRGIN.Modes.ControlMode");
                if (vrginModeType == null)
                {
                    plugin.Logger.LogWarning("VRGIN.Modes.ControlMode type not found. VRGIN.Core.VR.Mode.MoveToPosition cannot be used.");
                    return;
                }

                // Get the MoveToPosition method with specific parameters (Vector3 targetPosition, bool ignoreHeight).
                // We choose the overload that allows ignoring height, to get full control over Y position.
                vrginModeMoveToPositionMethod = vrginModeType.GetMethod("MoveToPosition", BindingFlags.Public | BindingFlags.Instance, null,
                    new Type[] { typeof(Vector3), typeof(bool) }, null);

                // Fallback: try method with 3 parameters (Vector3, Quaternion, bool), and only pass position and ignoreHeight.
                // This is less ideal but might work if the 2-param overload isn't found.
                if (vrginModeMoveToPositionMethod == null)
                {
                    plugin.Logger.LogWarning("VRGIN.Modes.ControlMode.MoveToPosition(Vector3, bool) method not found. Attempting (Vector3, Quaternion, bool).");
                    vrginModeMoveToPositionMethod = vrginModeType.GetMethod("MoveToPosition", BindingFlags.Public | BindingFlags.Instance, null,
                        new Type[] { typeof(Vector3), typeof(Quaternion), typeof(bool) }, null);
                    if (vrginModeMoveToPositionMethod == null)
                    {
                        plugin.Logger.LogWarning("VRGIN.Modes.ControlMode.MoveToPosition method with 2 or 3 args not found. Giving up.");
                        return;
                    }
                }

                vrginModeReflectionInitialized = true;
                plugin.Logger.LogMessage("Successfully initialized VRGIN.Core.VR.Mode.MoveToPosition reflection.");
            }
            catch (Exception ex)
            {
                plugin.Logger.LogError($"Error initializing VRGIN.Core.VR.Mode.MoveToPosition reflection: {ex}");
            }
        }

        /// <summary>
        /// Enables POV mode based on saved scene data.
        /// </summary>
        /// <param name="povData">Scene POV data to apply.</param>
        public static void EnablePov(ScenePovData povData)
        {
            if (Studio.Studio.Instance.dicObjectCtrl.TryGetValue(povData.CharaId, out var chara))
            {
                currentChara = ((OCIChar)chara).charInfo;
                currentCharaId = chara.objectInfo.dicKey;
                currentCharaGo = currentChara.gameObject;
                LookRotation[currentCharaGo] = povData.Rotation;
                CurrentFOV = povData.Fov;
                plugin.EnablePov(); // Call the instance method to apply POV.
                plugin.prevVisibleHeadAlways = povData.CharaPrevVisibleHeadAlways;
            }
        }

        /// <summary>
        /// Retrieves current POV data for saving.
        /// </summary>
        /// <returns>A ScenePovData object containing current POV settings.</returns>
        public static ScenePovData GetPovData()
        {
            return new ScenePovData
            {
                CharaId = currentCharaId,
                CharaPrevVisibleHeadAlways = plugin.prevVisibleHeadAlways,
                Fov = CurrentFOV ?? defaultFov, // Use CurrentFOV if set, otherwise default.
                Rotation = currentCharaGo != null ? LookRotation[currentCharaGo] : Vector3.zero // Use saved rotation or zero.
            };
        }

        /// <summary>
        /// Core logic to enable Point of View mode.
        /// Handles character selection, head visibility, camera control, and VR integration.
        /// </summary>
        protected override void EnablePov()
        {
            if (!currentChara) // If no character is selected for POV.
            {
                if (isStudio) // Studio mode: select first active character.
                {
                    var selectedCharas = GuideObjectManager.Instance.selectObjectKey
                        .Select(x => Studio.Studio.GetCtrlInfo(x) as OCIChar)
                        .Where(x => x != null)
                        .ToList();
                    if (selectedCharas.Count > 0)
                    {
                        var ociChar = selectedCharas.First();
                        currentChara = ociChar.charInfo;
                        currentCharaId = ociChar.objectInfo.dicKey;
                        currentCharaGo = currentChara.gameObject;
                    }
                    else
                    {
                        Logger.LogMessage("Select a character in workspace to enter its POV.");
                    }
                }
                else // Non-Studio (Main game) mode: queue and find character.
                {
                    if (charaQueue == null)
                        charaQueue = new Queue<ChaControl>(FindObjectsOfType<ChaControl>());

                    currentChara = GetCurrentChara(); // Try to get character from queue.
                    if (!currentChara)
                    {
                        // If no character found, refresh queue and try again.
                        charaQueue = new Queue<ChaControl>(FindObjectsOfType<ChaControl>());
                        currentChara = GetCurrentChara();
                    }

                    currentCharaGo = null;
                    if (currentChara)
                        currentCharaGo = currentChara.gameObject;
                    else
                        Log.Message("Can't enter POV: Could not find any valid characters (Non-Studio).");
                }
            }

            if (currentChara) // If a character is successfully selected.
            {
                prevVisibleHeadAlways = currentChara.fileStatus.visibleHeadAlways; // Backup original head visibility.

                if (IsVREnabled() && isStudio) // VR enabled in Studio mode.
                {
                    // Attempt to hide character head using TransientHead component for VR integration.
                    TransientHead transientHead = currentChara.gameObject.GetComponent<TransientHead>();
                    if (transientHead != null)
                    {
                        plugin.Logger.LogMessage($"RealPOV: Studio VR enabled. Attempting to hide character head via TransientHead.Visible = false.");
                        transientHead.Visible = false;
                    }
                    else
                    {
                        plugin.Logger.LogWarning("RealPOV: TransientHead component not found on character. Cannot hide head via Studio VR plugin. Falling back to default.");
                        if (HideHead.Value) currentChara.fileStatus.visibleHeadAlways = false;
                    }
                }
                else if (!IsVREnabled()) // Non-VR mode.
                {
                    if (HideHead.Value) currentChara.fileStatus.visibleHeadAlways = false;
                }
                else // VR enabled in Non-Studio mode.
                {
                    Logger.LogMessage("RealPOV: VR enabled (Non-Studio). Not hiding character head; VR plugin usually handles this.");
                }

                GameCamera = Camera.main; // Get the main game camera.

                if (!IsVREnabled()) // Non-VR specific camera adjustments.
                {
                    // Disable default camera controls.
                    var cc = (MonoBehaviour)GameCamera.GetComponent<CameraControl_Ver2>();
                    if (!cc) cc = GameCamera.GetComponent<Studio.CameraControl>();
                    if (cc) cc.enabled = false;

                    // Adjust Depth of Field (DOF) for POV effect.
                    var depthOfField = GameCamera.GetComponent<UnityStandardAssets.ImageEffects.DepthOfField>();
                    if (depthOfField != null && depthOfField.enabled)
                    {
                        dofOrigSize = depthOfField.focalSize;
                        dofOrigAperature = depthOfField.aperture;
                        depthOfField.focalTransform.localPosition = new Vector3(0, 0, 0.25f);
                        depthOfField.focalSize = 0.9f;
                        depthOfField.aperture = 0.6f;
                    }
                }

                // Initialize or retrieve POV look rotation.
                if (!LookRotation.TryGetValue(currentCharaGo, out _))
                    LookRotation[currentCharaGo] = currentChara.objHeadBone.transform.rotation.eulerAngles;

                base.EnablePov(); // Call base class's EnablePov.

                if (!IsVREnabled()) // Non-VR specific camera layer adjustment.
                {
                    backupLayer = GameCamera.gameObject.layer;
                    GameCamera.gameObject.layer = 0; // Set camera layer to avoid rendering issues.
                }
                else
                {
                    Logger.LogMessage("RealPOV: VR enabled. Not changing camera layer; VR plugin handles this.");
                }
            }
        }

        /// <summary>
        /// Retrieves a suitable character for POV in non-Studio mode from the queue.
        /// Filters by sex preference and activity.
        /// </summary>
        /// <returns>A suitable ChaControl object, or null if none found.</returns>
        private ChaControl GetCurrentChara()
        {
            for (int i = 0; i < charaQueue.Count; i++)
            {
                var chaControl = charaQueue.Dequeue();

                if (!chaControl) continue;

                charaQueue.Enqueue(chaControl); // Re-add to end of queue for next cycle.

                // Skip male characters in certain H-scene modes, unless specifically allowed by POV sex setting.
                if (chaControl.sex == 0 && hFlag && (hFlag.mode == HFlag.EMode.aibu || hFlag.mode == HFlag.EMode.lesbian || hFlag.mode == HFlag.EMode.masturbation))
                    continue;
                // Filter by selected POV sex.
                if (SelectedPOV.Value != PovSex.Either && chaControl.sex != (int)SelectedPOV.Value)
                    continue;
                // Ensure character is active in hierarchy.
                if (chaControl.objTop.activeInHierarchy)
                    return chaControl;
            }
            return null;
        }

        /// <summary>
        /// Disables POV mode and restores original camera and character settings.
        /// </summary>
        protected override void DisablePov()
        {
            if (currentChara != null)
            {
                if (IsVREnabled() && isStudio) // VR enabled in Studio mode.
                {
                    // Restore character head visibility via TransientHead.
                    TransientHead transientHead = currentChara.gameObject.GetComponent<TransientHead>();
                    if (transientHead != null)
                    {
                        plugin.Logger.LogMessage($"RealPOV: Studio VR enabled. Attempting to restore character head via TransientHead.Visible = true.");
                        transientHead.Visible = true;
                    }
                    else
                    {
                        plugin.Logger.LogWarning("RealPOV: TransientHead component not found on character. Cannot restore head via Studio VR plugin. Falling back to default.");
                        currentChara.fileStatus.visibleHeadAlways = prevVisibleHeadAlways;
                    }
                }
                else // Non-VR or Non-Studio VR mode.
                {
                    currentChara.fileStatus.visibleHeadAlways = prevVisibleHeadAlways; // Restore original head visibility.
                }
            }

            currentChara = null; // Clear current character reference.
            currentCharaId = -1; // Reset character ID.

            if (GameCamera != null)
            {
                if (!IsVREnabled()) // Non-VR specific camera restoration.
                {
                    // Re-enable default camera controls.
                    var cc = (MonoBehaviour)GameCamera.GetComponent<CameraControl_Ver2>();
                    if (!cc) cc = GameCamera.GetComponent<Studio.CameraControl>();
                    if (cc) cc.enabled = true;

                    // Restore Depth of Field settings.
                    var depthOfField = GameCamera.GetComponent<UnityStandardAssets.ImageEffects.DepthOfField>();
                    if (depthOfField != null && depthOfField.enabled)
                    {
                        depthOfField.focalSize = dofOrigSize;
                        depthOfField.aperture = dofOrigAperature;
                    }
                }
                else
                {
                    Logger.LogMessage("RealPOV: VR enabled. Not restoring camera control or DepthOfField.");
                }
            }

            base.DisablePov(); // Call base class's DisablePov.

            if (GameCamera != null && !IsVREnabled()) // Non-VR specific camera layer restoration.
            {
                GameCamera.gameObject.layer = backupLayer;
            }
            else if (GameCamera != null)
            {
                Logger.LogMessage("RealPOV: VR enabled. Not restoring camera layer.");
            }
        }

        /// <summary>
        /// Harmony patches for camera and animation control.
        /// </summary>
        private class Hooks
        {
            /// <summary>
            /// Prefix patch for NeckLookControllerVer2.LateUpdate to control camera position and rotation.
            /// Prevents original neck look logic from running when POV is active.
            /// Integrates with VRGIN for VR camera movement.
            /// </summary>
            [HarmonyPrefix, HarmonyPatch(typeof(NeckLookControllerVer2), nameof(NeckLookControllerVer2.LateUpdate)), HarmonyWrapSafe]
            private static bool ApplyRotation(NeckLookControllerVer2 __instance)
            {
                if (plugin == null) return true;

                // In main game mode with VR enabled, let original game logic run.
                // This plugin's VR integration is primarily for Studio mode.
                if (!plugin.isStudio && plugin.IsVREnabled())
                {
                    plugin.Logger.LogDebug("RealPOV: Not in Studio mode and VR enabled. Bypassing POV camera logic.");
                    return true;
                }

                if (POVEnabled)
                {
                    if (!currentChara)
                    {
                        plugin.DisablePov(); // Disable POV if character is no longer valid.
                        return true;
                    }

                    // Retrieve or initialize camera look rotation.
                    Vector3 rot;
                    if (LookRotation.TryGetValue(currentCharaGo, out var val))
                        rot = val;
                    else
                        LookRotation[currentCharaGo] = rot = currentChara.objHeadBone.transform.rotation.eulerAngles;

                    if (__instance.neckLookScript && currentChara.neckLookCtrl == __instance)
                    {
                        // Reset neck bone rotations to avoid conflicts.
                        __instance.neckLookScript.aBones[0].neckBone.rotation = Quaternion.identity;
                        __instance.neckLookScript.aBones[1].neckBone.rotation = Quaternion.identity;
                        __instance.neckLookScript.aBones[1].neckBone.Rotate(rot); // Apply custom rotation to neck bone.

                        var eyeObjs = currentChara.eyeLookCtrl.eyeLookScript.eyeObjs;
                        var headBoneTransform = currentChara.objHeadBone.transform;

                        // Calculate average eye position.
                        var eyePosition = Vector3.Lerp(eyeObjs[0].eyeTransform.position, eyeObjs[1].eyeTransform.position, 0.5f);

                        if (plugin.IsVREnabled()) // VR camera positioning.
                        {
                            // Calculate target position for VR camera.
                            Vector3 targetPosition = eyePosition + headBoneTransform.forward * VRViewOffset.Value;

                            if (VR.Mode != null && vrginModeMoveToPositionMethod != null)
                            {
                                // Call VRGIN.Core.VR.Mode.MoveToPosition via reflection.
                                // This moves the VR play space so that the HMD is at targetPosition.
                                // ignoreHeight = false ensures full control over Y position.
                                if (vrginModeMoveToPositionMethod.GetParameters().Length == 3)
                                {
                                    // If method is MoveToPosition(Vector3, Quaternion, bool)
                                    vrginModeMoveToPositionMethod.Invoke(VR.Mode, new object[] { targetPosition, Quaternion.identity, false });
                                }
                                else if (vrginModeMoveToPositionMethod.GetParameters().Length == 2)
                                {
                                    // If method is MoveToPosition(Vector3, bool)
                                    vrginModeMoveToPositionMethod.Invoke(VR.Mode, new object[] { targetPosition, false });
                                }

                                plugin.Logger.LogDebug($"RealPOV: Moved VR Camera to {targetPosition}");
                            }
                            else
                            {
                                plugin.Logger.LogWarning("VRGIN.Core.VR.Mode or MoveToPosition method is null. Cannot move VR camera.");
                            }

                            CurrentFOV = null; // Let VRGIN handle FOV.
                        }
                        else // Non-VR camera positioning.
                        {
                            // Set camera position and rotation based on head bone.
                            GameCamera.transform.SetPositionAndRotation(eyePosition, headBoneTransform.rotation);
                            GameCamera.transform.Translate(Vector3.forward * ViewOffset.Value); // Apply view offset.

                            // Set FOV.
                            if (CurrentFOV == null)
                            {
                                CurrentFOV = DefaultFOV.Value;
                            }
                            GameCamera.fieldOfView = CurrentFOV.Value;
                        }

                        return false; // Prevent original LateUpdate from running.
                    }
                }

                return true; // Allow original LateUpdate to run if POV is not enabled or conditions not met.
            }

            /// <summary>
            /// Postfix patches to reset all POV rotations when H-scene animations change.
            /// Ensures fresh rotation data for new animations.
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
            [HarmonyPatch(typeof(HFlag), nameof(HFlag.selectAnimationListInfo), MethodType.Setter)]
            private static void ResetAllRotations()
            {
                LookRotation.Clear();
            }
        }

        /// <summary>
        /// Enum for selecting point of view character sex.
        /// </summary>
        private enum PovSex
        {
            Male,
            Female,
            Either
        }
    }
}