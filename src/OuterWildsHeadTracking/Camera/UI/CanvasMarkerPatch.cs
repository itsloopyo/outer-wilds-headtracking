extern alias UnityCoreModule;
using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Core;
using OuterWildsHeadTracking.Camera.Utilities;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;
using Vector3 = UnityCoreModule::UnityEngine.Vector3;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// Patches OWExtensions.WorldToCanvasPosition and OffScreenIndicator.SetCanvasPosition
    /// to ensure head tracking rotation is applied during marker position calculations.
    /// CanvasMarker.Update may run before our Update_Postfix, so the camera might not
    /// have head tracking on it yet. These patches normalize the state.
    /// </summary>
    public static class CanvasMarkerPatch
    {
        // State for WorldToCanvasPosition patch
        private static Quaternion _savedRotation = Quaternion.identity;
        private static bool _rotationWasModified = false;
        private static int _nestedCallCount = 0;
        private static int _lastModifiedFrame = -1;
        private static UnityCoreModule::UnityEngine.Transform? _modifiedTransform = null;

        // Helper for OffScreenIndicator patch
        private static readonly RotationPatchHelper _offscreenHelper = new RotationPatchHelper(RotationPatchMode.ApplyHeadTracking);

        public static void ApplyPatches(Harmony harmony)
        {
            // Patch WorldToCanvasPosition
            var owExtensionsType = AccessTools.TypeByName("OWExtensions");
            if (owExtensionsType == null)
                throw new InvalidOperationException("Could not find OWExtensions type!");

            var targetMethod = FindWorldToCanvasPositionMethod(owExtensionsType);
            if (targetMethod == null)
                throw new InvalidOperationException("Could not find WorldToCanvasPosition method!");

            harmony.Patch(targetMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(CanvasMarkerPatch), nameof(WorldToCanvasPosition_Prefix))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(CanvasMarkerPatch), nameof(WorldToCanvasPosition_Postfix))));

            // Patch OffScreenIndicator.SetCanvasPosition
            var offScreenIndicatorType = AccessTools.TypeByName("OffScreenIndicator");
            if (offScreenIndicatorType == null)
                throw new InvalidOperationException("Could not find OffScreenIndicator type!");

            var setCanvasPositionMethod = AccessTools.Method(offScreenIndicatorType, "SetCanvasPosition", new Type[] { typeof(Vector3) });
            if (setCanvasPositionMethod == null)
                throw new InvalidOperationException("Could not find SetCanvasPosition method!");

            harmony.Patch(setCanvasPositionMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(CanvasMarkerPatch), nameof(OffScreenIndicator_Prefix))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(CanvasMarkerPatch), nameof(OffScreenIndicator_Postfix))));
        }

        private static System.Reflection.MethodInfo? FindWorldToCanvasPositionMethod(Type owExtensionsType)
        {
            var methods = owExtensionsType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            foreach (var method in methods)
            {
                if (method.Name != "WorldToCanvasPosition") continue;
                var parameters = method.GetParameters();
                if (parameters.Length == 3 &&
                    parameters[0].ParameterType.Name == "Canvas" &&
                    parameters[1].ParameterType.Name == "Camera")
                {
                    return method;
                }
            }
            return null;
        }

        public static void WorldToCanvasPosition_Prefix(object canvas, UnityCoreModule::UnityEngine.Camera camera, Vector3 worldPosition)
        {
            int currentFrame = UnityCoreModule::UnityEngine.Time.frameCount;

            // Defensive frame reset: clear stuck state from previous frame
            if (_lastModifiedFrame != -1 && _lastModifiedFrame != currentFrame && _rotationWasModified)
            {
                if (_modifiedTransform != null)
                    _modifiedTransform.localRotation = _savedRotation;
                _rotationWasModified = false;
                _nestedCallCount = 0;
                _modifiedTransform = null;
            }

            var mod = HeadTrackingMod.Instance;
            if (mod == null || !mod.IsTrackingEnabled()) return;

            var cameraTransform = SimpleCameraPatch._cameraTransform;
            if (cameraTransform == null || camera.transform != cameraTransform) return;

            _nestedCallCount++;
            if (_nestedCallCount > 1) return;

            var headTracking = SimpleCameraPatch._lastHeadTrackingRotation;
            if (headTracking == Quaternion.identity) return;

            var baseRotation = SimpleCameraPatch._baseRotationBeforeHeadTracking;
            if (baseRotation == default || baseRotation == Quaternion.identity) return;

            _savedRotation = cameraTransform.localRotation;
            _rotationWasModified = true;
            _lastModifiedFrame = currentFrame;
            _modifiedTransform = cameraTransform;

            // Apply head-tracked rotation (base world rotation * head tracking, converted to local)
            Quaternion headTrackedWorld = baseRotation * headTracking;
            cameraTransform.localRotation = cameraTransform.parent != null
                ? Quaternion.Inverse(cameraTransform.parent.rotation) * headTrackedWorld
                : headTrackedWorld;
        }

        public static void WorldToCanvasPosition_Postfix()
        {
            if (_nestedCallCount > 0)
                _nestedCallCount--;

            if (_nestedCallCount > 0) return;
            if (!_rotationWasModified) return;

            if (_modifiedTransform != null)
                _modifiedTransform.localRotation = _savedRotation;

            _rotationWasModified = false;
            _modifiedTransform = null;
        }

        public static void OffScreenIndicator_Prefix() => _offscreenHelper.BeginPatch();
        public static void OffScreenIndicator_Postfix() => _offscreenHelper.EndPatch();
    }
}
