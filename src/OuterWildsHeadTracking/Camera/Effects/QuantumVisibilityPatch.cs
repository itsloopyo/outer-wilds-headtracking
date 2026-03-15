extern alias UnityCoreModule;
using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Core;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;

namespace OuterWildsHeadTracking.Camera.Effects
{
    /// <summary>
    /// Patches VisibilityObject.Update to ensure quantum objects check visibility
    /// against head-tracked rotation. Temporarily applies head tracking to the
    /// camera transform for view cone checks.
    /// </summary>
    public static class QuantumVisibilityPatch
    {
        private static Quaternion _savedRotation = Quaternion.identity;
        private static bool _rotationModified = false;

        public static void ApplyPatches(Harmony harmony)
        {
            var visibilityObjectType = AccessTools.TypeByName("VisibilityObject");
            if (visibilityObjectType == null)
            {
                throw new InvalidOperationException("Could not find VisibilityObject type!");
            }

            var updateMethod = AccessTools.Method(visibilityObjectType, "Update");
            if (updateMethod == null)
            {
                throw new InvalidOperationException("Could not find VisibilityObject.Update method!");
            }

            var prefixMethod = AccessTools.Method(typeof(QuantumVisibilityPatch), nameof(VisibilityObject_Update_Prefix));
            var postfixMethod = AccessTools.Method(typeof(QuantumVisibilityPatch), nameof(VisibilityObject_Update_Postfix));

            if (prefixMethod == null || postfixMethod == null)
            {
                throw new InvalidOperationException("Could not find QuantumVisibilityPatch prefix/postfix methods!");
            }

            harmony.Patch(updateMethod,
                prefix: new HarmonyMethod(prefixMethod),
                postfix: new HarmonyMethod(postfixMethod));
        }

        public static void VisibilityObject_Update_Prefix()
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

        public static void VisibilityObject_Update_Postfix()
        {
            if (!_rotationModified) return;

            var cameraTransform = SimpleCameraPatch._cameraTransform;
            if (cameraTransform == null) return;

            cameraTransform.rotation = _savedRotation;
            _rotationModified = false;
        }
    }
}
