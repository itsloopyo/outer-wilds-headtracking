extern alias UnityCoreModule;
using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Core;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;
using Vector3 = UnityCoreModule::UnityEngine.Vector3;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// Patches for Signalscope tool - ensures signal detection uses head direction.
    /// With view matrix, the transform is clean so we temporarily apply head tracking
    /// for methods that read camera.transform directly.
    /// </summary>
    public static class SignalscopePatches
    {
        private static Quaternion _savedRotation = Quaternion.identity;
        private static bool _rotationModified = false;

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

            var headTracking = SimpleCameraPatch._lastHeadTrackingRotation;
            if (headTracking == Quaternion.identity) return;

            var baseRotation = SimpleCameraPatch._baseRotationBeforeHeadTracking;
            if (baseRotation == default) return;

            _savedRotation = cameraTransform.rotation;
            cameraTransform.rotation = baseRotation * headTracking;
            _rotationModified = true;
        }

        public static void Signalscope_Update_Postfix()
        {
            if (!_rotationModified) return;

            var cameraTransform = SimpleCameraPatch._cameraTransform;
            if (cameraTransform == null) return;

            cameraTransform.rotation = _savedRotation;
            _rotationModified = false;
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
    }
}
