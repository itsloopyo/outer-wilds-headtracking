extern alias UnityCoreModule;
using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Utilities;
using OuterWildsHeadTracking.Camera.Core;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;
using Vector3 = UnityCoreModule::UnityEngine.Vector3;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// Patches for Signalscope tool - ensures signal detection uses head direction
    /// </summary>
    public static class SignalscopePatches
    {
        private static Quaternion _signalscopeSavedRotation = Quaternion.identity;
        private static bool _signalscopeRotationModified = false;

        public static void ApplyPatches(Harmony harmony)
        {
            var signalscopeType = AccessTools.TypeByName("Signalscope");
            if (signalscopeType == null)
            {
                throw new InvalidOperationException("Could not find Signalscope type!");
            }

            PatchSignalscopeUpdate(harmony, signalscopeType);
            PatchGetScopeDirection(harmony, signalscopeType);
        }

        private static void PatchSignalscopeUpdate(Harmony harmony, Type signalscopeType)
        {
            var signalscopeUpdateMethod = AccessTools.Method(signalscopeType, "Update");
            if (signalscopeUpdateMethod == null)
            {
                throw new InvalidOperationException("Could not find Signalscope.Update method!");
            }

            var signalscopePrefix = AccessTools.Method(typeof(SignalscopePatches), nameof(Signalscope_Update_Prefix));
            var signalscopePostfix = AccessTools.Method(typeof(SignalscopePatches), nameof(Signalscope_Update_Postfix));

            if (signalscopePrefix == null || signalscopePostfix == null)
            {
                throw new InvalidOperationException("Could not find SignalscopePatches prefix/postfix methods!");
            }

            harmony.Patch(signalscopeUpdateMethod, prefix: new HarmonyMethod(signalscopePrefix), postfix: new HarmonyMethod(signalscopePostfix));
        }

        private static void PatchGetScopeDirection(Harmony harmony, Type signalscopeType)
        {
            var getScopeDirectionMethod = AccessTools.Method(signalscopeType, "GetScopeDirection");
            if (getScopeDirectionMethod == null)
            {
                throw new InvalidOperationException("Could not find Signalscope.GetScopeDirection method!");
            }

            var scopeDirPostfix = AccessTools.Method(typeof(SignalscopePatches), nameof(Signalscope_GetScopeDirection_Postfix));
            if (scopeDirPostfix == null)
            {
                throw new InvalidOperationException("Could not find Signalscope_GetScopeDirection_Postfix method!");
            }

            harmony.Patch(getScopeDirectionMethod, postfix: new HarmonyMethod(scopeDirPostfix));
        }

        public static void Signalscope_Update_Prefix()
        {
            var mod = HeadTrackingMod.Instance;
            if (mod == null || !mod.IsTrackingEnabled()) return;

            var cameraTransform = SimpleCameraPatch._cameraTransform;
            if (cameraTransform == null) return;

            EnsureFrameReset();

            if (MapMarkerPatch._cameraHasHeadTracking) return;

            var headTracking = SimpleCameraPatch._lastHeadTrackingRotation;
            if (headTracking == Quaternion.identity) return;

            _signalscopeSavedRotation = cameraTransform.localRotation;
            cameraTransform.localRotation = _signalscopeSavedRotation * headTracking;
            _signalscopeRotationModified = true;
            MapMarkerPatch._cameraHasHeadTracking = true;
        }

        public static void Signalscope_Update_Postfix()
        {
            if (!_signalscopeRotationModified) return;

            var cameraTransform = SimpleCameraPatch._cameraTransform;
            if (cameraTransform == null) return;

            cameraTransform.localRotation = _signalscopeSavedRotation;
            _signalscopeSavedRotation = Quaternion.identity;
            _signalscopeRotationModified = false;
            MapMarkerPatch._cameraHasHeadTracking = false;
        }

        public static void Signalscope_GetScopeDirection_Postfix(ref Vector3 __result)
        {
            var mod = HeadTrackingMod.Instance;
            if (mod == null || !mod.IsTrackingEnabled()) return;

            var cameraTransform = SimpleCameraPatch._cameraTransform;
            if (cameraTransform == null) return;

            var baseRotation = SimpleCameraPatch._baseRotationBeforeHeadTracking;
            if (baseRotation == default || baseRotation == Quaternion.identity)
            {
                return;
            }

            var headTracking = SimpleCameraPatch._lastHeadTrackingRotation;
            if (headTracking == Quaternion.identity)
            {
                __result = baseRotation * Vector3.forward;
            }
            else
            {
                __result = (baseRotation * headTracking) * Vector3.forward;
            }
        }

        private static void EnsureFrameReset()
        {
            int currentFrame = UnityCoreModule::UnityEngine.Time.frameCount;
            if (MapMarkerPatch._lastFrameReset != currentFrame)
            {
                if (MapMarkerPatch._headTrackingAppliedFrame != currentFrame)
                {
                    MapMarkerPatch._cameraHasHeadTracking = false;
                }
                MapMarkerPatch._lastFrameReset = currentFrame;
            }
        }

    }
}
