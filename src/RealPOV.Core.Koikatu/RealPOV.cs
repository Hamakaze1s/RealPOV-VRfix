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

using KKCharaStudioVR;
using VRGIN.Core;

[assembly: System.Reflection.AssemblyVersion(RealPOV.Koikatu.RealPOV.Version)]

namespace RealPOV.Koikatu
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KKAPI.KoikatuAPI.GUID)]
    public class RealPOV : RealPOVCore
    {
        public const string Version = "1.4.1." + BuildNumber.Version;

        private ConfigEntry<bool> HideHead { get; set; }
        private ConfigEntry<PovSex> SelectedPOV { get; set; }

        private static int backupLayer;
        private static ChaControl currentChara;
        private static Queue<ChaControl> charaQueue;
        private readonly bool isStudio = Paths.ProcessName == "CharaStudio";
        private bool prevVisibleHeadAlways;
        private HFlag hFlag;
        private static int currentCharaId = -1;
        private static RealPOV plugin;

        private float dofOrigSize;
        private float dofOrigAperature;

        // --- Keep VRGIN.Core.VR.Active related fields for direct access ---
        // (No change needed here from previous versions if you keep VRGIN.Core using)

        // --- NEW: Reflection fields for VRGIN.Core.VR.Mode.MoveToPosition ---
        private static Type vrginModeType; // This will be VRGIN.Modes.ControlMode (or derived)
        private static MethodInfo vrginModeMoveToPositionMethod;
        private static bool vrginModeReflectionInitialized = false;

        protected override void Awake()
        {
            plugin = this;
            defaultFov = 45f;
            defaultViewOffset = 0.03f;
            defaultVRViewOffset = 0.00f;
            base.Awake();

            HideHead = Config.Bind(SECTION_GENERAL, "Hide character head", false, "When entering POV, hide the character's head. Prevents accessories and hair from obstructing the view.");
            SelectedPOV = Config.Bind(SECTION_GENERAL, "Selected POV", PovSex.Male, "Choose which sex to use as your point of view.");

            Harmony.CreateAndPatchAll(typeof(Hooks));
            StudioSaveLoadApi.RegisterExtraBehaviour<SceneDataController>(GUID);

            SceneManager.sceneLoaded += (arg0, scene) =>
            {
                hFlag = FindObjectOfType<HFlag>();
                charaQueue = null;
            };
            SceneManager.sceneUnloaded += arg0 => charaQueue = null;

            // Initialize VRGIN.Core.VR.Mode.MoveToPosition reflection in Awake
            InitializeVRGINModeMoveToPositionReflection();
        }

        protected override bool IsVREnabled()
        {
            return VR.Active;
        }

        // --- NEW: Initialization for VRGIN.Core.VR.Mode.MoveToPosition ---
        private static void InitializeVRGINModeMoveToPositionReflection()
        {
            if (vrginModeReflectionInitialized) return;

            try
            {
                // We directly access VR.Mode, which is an instance of ControlMode (or its derived class).
                // We need to get the actual Type of VR.Mode at runtime.
                // However, the MoveToPosition method is defined on the base ControlMode class.
                // So we can directly get the method from VRGIN.Modes.ControlMode.
                Assembly vrginAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "VRGIN_KKCS"); // Use VRGIN_KKCS for VRGIN types

                if (vrginAssembly == null)
                {
                    plugin.Logger.LogWarning("VRGIN_KKCS.dll assembly not found. Cannot use VRGIN.Core.VR.Mode.MoveToPosition.");
                    return;
                }

                vrginModeType = vrginAssembly.GetType("VRGIN.Modes.ControlMode"); // Get the base ControlMode type
                if (vrginModeType == null)
                {
                    plugin.Logger.LogWarning("VRGIN.Modes.ControlMode type not found. Cannot use VRGIN.Core.VR.Mode.MoveToPosition.");
                    return;
                }

                // Get the MoveToPosition method: public virtual void MoveToPosition(Vector3 targetPosition, Quaternion rotation = default(Quaternion), bool ignoreHeight = true)
                // We want to pass ignoreHeight = false, to allow full control over Y position.
                vrginModeMoveToPositionMethod = vrginModeType.GetMethod("MoveToPosition", BindingFlags.Public | BindingFlags.Instance, null,
                    new Type[] { typeof(Vector3), typeof(Quaternion), typeof(bool) }, null);

                if (vrginModeMoveToPositionMethod == null)
                {
                    plugin.Logger.LogWarning("VRGIN.Modes.ControlMode.MoveToPosition method not found or signature mismatch. Cannot use VRGIN.Core.VR.Mode.MoveToPosition.");
                    // Try with 2 args (if default(Quaternion) and ignoreHeight were params)
                    vrginModeMoveToPositionMethod = vrginModeType.GetMethod("MoveToPosition", BindingFlags.Public | BindingFlags.Instance, null,
                        new Type[] { typeof(Vector3), typeof(Quaternion) }, null);
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


        public static void EnablePov(ScenePovData povData)
        {
            if (Studio.Studio.Instance.dicObjectCtrl.TryGetValue(povData.CharaId, out var chara))
            {
                currentChara = ((OCIChar)chara).charInfo;
                currentCharaId = chara.objectInfo.dicKey;
                currentCharaGo = currentChara.gameObject;
                LookRotation[currentCharaGo] = povData.Rotation;
                CurrentFOV = povData.Fov;
                plugin.EnablePov();
                plugin.prevVisibleHeadAlways = povData.CharaPrevVisibleHeadAlways;
            }
        }

        public static ScenePovData GetPovData()
        {
            return new ScenePovData
            {
                CharaId = currentCharaId,
                CharaPrevVisibleHeadAlways = plugin.prevVisibleHeadAlways,
                Fov = CurrentFOV != null ? CurrentFOV.Value : defaultFov,
                Rotation = currentCharaGo != null ? LookRotation[currentCharaGo] : new Vector3(0, 0, 0)
            };
        }

        protected override void EnablePov()
        {
            if (!currentChara)
            {
                if (isStudio)
                {
                    var selectedCharas = GuideObjectManager.Instance.selectObjectKey.Select(x => Studio.Studio.GetCtrlInfo(x) as OCIChar).Where(x => x != null).ToList();
                    if (selectedCharas.Count > 0)
                    {
                        var ociChar = selectedCharas.First();
                        currentChara = ociChar.charInfo;
                        currentCharaId = ociChar.objectInfo.dicKey;
                        currentCharaGo = currentChara.gameObject;
                    }
                    else
                    {
                        Logger.LogMessage("Select a character in workspace to enter its POV");
                    }
                }
                else // NON-STUDIO (Main game)
                {
                    if (charaQueue == null)
                        charaQueue = new Queue<ChaControl>(FindObjectsOfType<ChaControl>());

                    currentChara = GetCurrentChara();
                    if (!currentChara)
                    {
                        charaQueue = new Queue<ChaControl>(FindObjectsOfType<ChaControl>());
                        currentChara = GetCurrentChara();
                    }

                    currentCharaGo = null;
                    if (currentChara)
                        currentCharaGo = currentChara.gameObject;
                    else
                        Log.Message("Can't enter POV: Could not find any valid characters (Non-Studio)");
                }
            }

            if (currentChara)
            {
                prevVisibleHeadAlways = currentChara.fileStatus.visibleHeadAlways;

                if (IsVREnabled() && isStudio)
                {
                    TransientHead transientHead = currentChara.gameObject.GetComponent<TransientHead>();
                    if (transientHead != null)
                    {
                        plugin.Logger.LogMessage($"RealPOV: Studio VR enabled. Attempting to hide character head via TransientHead.Visible = false.");
                        transientHead.Visible = false;
                    }
                    else
                    {
                        plugin.Logger.LogWarning("RealPOV: TransientHead component not found on character. Cannot hide head via Studio VR plugin.");
                        if (HideHead.Value) currentChara.fileStatus.visibleHeadAlways = false;
                    }
                }
                else if (!IsVREnabled())
                {
                    if (HideHead.Value) currentChara.fileStatus.visibleHeadAlways = false;
                }
                else
                {
                    Logger.LogMessage("RealPOV: VR enabled (Non-Studio). Not hiding character head; VR plugin handles this (or not handled by RealPOV).");
                }

                GameCamera = Camera.main;

                if (!IsVREnabled())
                {
                    var cc = (MonoBehaviour)GameCamera.GetComponent<CameraControl_Ver2>();
                    if (!cc) cc = GameCamera.GetComponent<Studio.CameraControl>();
                    if (cc) cc.enabled = false;

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

                if (!LookRotation.TryGetValue(currentCharaGo, out _))
                    LookRotation[currentCharaGo] = currentChara.objHeadBone.transform.rotation.eulerAngles;

                base.EnablePov();

                if (!IsVREnabled())
                {
                    backupLayer = GameCamera.gameObject.layer;
                    GameCamera.gameObject.layer = 0;
                }
                else
                {
                    Logger.LogMessage("RealPOV: VR enabled. Not changing camera layer; VR plugin handles this.");
                }
            }
        }

        private ChaControl GetCurrentChara()
        {
            for (int i = 0; i < charaQueue.Count; i++)
            {
                var chaControl = charaQueue.Dequeue();

                if (!chaControl)
                    continue;

                charaQueue.Enqueue(chaControl);

                if (chaControl.sex == 0 && hFlag && (hFlag.mode == HFlag.EMode.aibu || hFlag.mode == HFlag.EMode.lesbian || hFlag.mode == HFlag.EMode.masturbation))
                    continue;
                if (SelectedPOV.Value != PovSex.Either && chaControl.sex != (int)SelectedPOV.Value)
                    continue;
                if (chaControl.objTop.activeInHierarchy)
                    return chaControl;
            }
            return null;
        }

        protected override void DisablePov()
        {
            if (currentChara != null)
            {
                if (IsVREnabled() && isStudio)
                {
                    TransientHead transientHead = currentChara.gameObject.GetComponent<TransientHead>();
                    if (transientHead != null)
                    {
                        plugin.Logger.LogMessage($"RealPOV: Studio VR enabled. Attempting to restore character head via TransientHead.Visible = true.");
                        transientHead.Visible = true;
                    }
                    else
                    {
                        plugin.Logger.LogWarning("RealPOV: TransientHead component not found on character. Cannot restore head via Studio VR plugin.");
                        currentChara.fileStatus.visibleHeadAlways = prevVisibleHeadAlways;
                    }
                }
                else
                {
                    currentChara.fileStatus.visibleHeadAlways = prevVisibleHeadAlways;
                }
            }

            currentChara = null;
            currentCharaId = -1;

            if (GameCamera != null)
            {
                if (!IsVREnabled())
                {
                    var cc = (MonoBehaviour)GameCamera.GetComponent<CameraControl_Ver2>();
                    if (!cc) cc = GameCamera.GetComponent<Studio.CameraControl>();
                    if (cc) cc.enabled = true;

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

            base.DisablePov();

            if (GameCamera != null && !IsVREnabled())
            {
                GameCamera.gameObject.layer = backupLayer;
            }
            else if (GameCamera != null)
            {
                Logger.LogMessage("RealPOV: VR enabled. Not restoring camera layer.");
            }
        }

        private class Hooks
        {
            [HarmonyPrefix, HarmonyPatch(typeof(NeckLookControllerVer2), nameof(NeckLookControllerVer2.LateUpdate)), HarmonyWrapSafe]
            private static bool ApplyRotation(NeckLookControllerVer2 __instance)
            {
                if (plugin == null) return true;

                // If in main game mode AND VR is enabled, let original game logic run.
                // This plugin is now designed specifically for Studio VR mode.
                if (!plugin.isStudio && plugin.IsVREnabled())
                {
                    plugin.Logger.LogDebug("RealPOV: Not in Studio mode and VR enabled. Bypassing POV camera logic.");
                    return true;
                }

                if (POVEnabled)
                {
                    if (!currentChara)
                    {
                        plugin.DisablePov();
                        return true;
                    }

                    Vector3 rot;
                    if (LookRotation.TryGetValue(currentCharaGo, out var val))
                        rot = val;
                    else
                        LookRotation[currentCharaGo] = rot = currentChara.objHeadBone.transform.rotation.eulerAngles;

                    if (__instance.neckLookScript && currentChara.neckLookCtrl == __instance)
                    {
                        __instance.neckLookScript.aBones[0].neckBone.rotation = Quaternion.identity;
                        __instance.neckLookScript.aBones[1].neckBone.rotation = Quaternion.identity;
                        __instance.neckLookScript.aBones[1].neckBone.Rotate(rot);

                        var eyeObjs = currentChara.eyeLookCtrl.eyeLookScript.eyeObjs;
                        var headBoneTransform = currentChara.objHeadBone.transform;

                        var eyePosition = Vector3.Lerp(eyeObjs[0].eyeTransform.position, eyeObjs[1].eyeTransform.position, 0.5f);

                        if (plugin.IsVREnabled())
                        {
                            Vector3 targetPosition = eyePosition + headBoneTransform.forward * VRViewOffset.Value;
                            // Quaternion targetRotation = headBoneTransform.rotation; // Still use character's head rotation as target
                            Quaternion targetRotation = VR.Camera.Head.rotation;
                            // NEW: Call VRGIN.Core.VR.Mode.MoveToPosition directly
                            // VR.Mode is an instance of ControlMode (or derived, like StandingMode).
                            // It has a MoveToPosition method defined in ControlMode.
                            // We need to pass ignoreHeight = false to allow full control of Y position.
                            // The VRGIN.Core.VR.Mode.MoveToPosition will handle the HMD rotation relative to origin.
                            if (VR.Mode != null)
                            {
                                // We are targeting a specific position and rotation (character's head).
                                // ignoreHeight: false means the origin will be moved so the head is EXACTLY at targetPosition.
                                // VR.Mode.MoveToPosition(targetPosition, targetRotation, false);
                                VR.Mode.MoveToPosition(targetPosition,false);
                                plugin.Logger.LogDebug($"RealPOV: Moved VR Camera to {targetPosition} with rotation {targetRotation.eulerAngles}");
                            }
                            else
                            {
                                plugin.Logger.LogWarning("VRGIN.Core.VR.Mode is null. Cannot move VR camera.");
                            }

                            CurrentFOV = null;
                        }
                        else
                        {
                            GameCamera.transform.SetPositionAndRotation(eyePosition, headBoneTransform.rotation);
                            GameCamera.transform.Translate(Vector3.forward * ViewOffset.Value);

                            if (CurrentFOV == null)
                            {
                                CurrentFOV = DefaultFOV.Value;
                            }
                            GameCamera.fieldOfView = CurrentFOV.Value;
                        }

                        return false;
                    }
                }

                return true;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
            [HarmonyPatch(typeof(HFlag), nameof(HFlag.selectAnimationListInfo), MethodType.Setter)]
            private static void ResetAllRotations()
            {
                LookRotation.Clear();
            }
        }

        private enum PovSex
        {
            Male,
            Female,
            Either
        }
    }
}