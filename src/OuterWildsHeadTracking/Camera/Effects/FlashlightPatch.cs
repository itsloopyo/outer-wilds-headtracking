extern alias UnityCoreModule;
using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Core;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;

namespace OuterWildsHeadTracking.Camera.Effects
{
    /// <summary>
    /// Patches Flashlight to follow head-tracked direction.
    /// Flashlight uses camera.transform.forward for beam direction, so we temporarily
    /// apply head tracking to the transform during FixedUpdate.
    /// </summary>
    public static class FlashlightPatch
    {
        private static Quaternion _savedRotation = Quaternion.identity;
        private static bool _rotationModified = false;

        public static void ApplyPatches(Harmony harmony)
        {
            var flashlightType = AccessTools.TypeByName("Flashlight");
            if (flashlightType == null)
            {
                throw new InvalidOperationException("Could not find Flashlight type!");
            }

            var fixedUpdateMethod = AccessTools.Method(flashlightType, "FixedUpdate");
            if (fixedUpdateMethod == null)
            {
                throw new InvalidOperationException("Could not find Flashlight.FixedUpdate method!");
            }

            var prefixMethod = AccessTools.Method(typeof(FlashlightPatch), nameof(FixedUpdate_Prefix));
            var postfixMethod = AccessTools.Method(typeof(FlashlightPatch), nameof(FixedUpdate_Postfix));

            if (prefixMethod == null || postfixMethod == null)
            {
                throw new InvalidOperationException("Could not find FlashlightPatch prefix/postfix methods!");
            }

            harmony.Patch(fixedUpdateMethod,
                prefix: new HarmonyMethod(prefixMethod),
                postfix: new HarmonyMethod(postfixMethod));
        }

        public static void FixedUpdate_Prefix()
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

        public static void FixedUpdate_Postfix()
        {
            if (!_rotationModified) return;

            var cameraTransform = SimpleCameraPatch._cameraTransform;
            if (cameraTransform == null) return;

            cameraTransform.rotation = _savedRotation;
            _rotationModified = false;
        }
    }
}
