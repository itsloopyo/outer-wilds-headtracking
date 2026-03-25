extern alias UnityCoreModule;
using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Core;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// Patches NomaiTranslator.Update to apply head tracking to the camera transform
    /// during raycast calculations. Without this, if NomaiTranslator.Update runs before
    /// PlayerCameraController.Update, the camera has game-only rotation and the translator
    /// raycast won't follow head direction.
    /// Matches the signalscope pattern: modify camera transform directly.
    /// </summary>
    public static class NomaiTranslatorPatches
    {
        private static Quaternion _savedRotation = Quaternion.identity;
        private static bool _rotationModified = false;

        public static void ApplyPatches(Harmony harmony)
        {
            var nomaiTranslatorType = AccessTools.TypeByName("NomaiTranslator");
            if (nomaiTranslatorType == null)
                throw new InvalidOperationException("Could not find NomaiTranslator type!");

            var translatorUpdateMethod = AccessTools.Method(nomaiTranslatorType, "Update");
            if (translatorUpdateMethod == null)
                throw new InvalidOperationException("Could not find NomaiTranslator.Update method!");

            harmony.Patch(translatorUpdateMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(NomaiTranslatorPatches), nameof(Prefix))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(NomaiTranslatorPatches), nameof(Postfix))));
        }

        public static void Prefix()
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

        public static void Postfix()
        {
            if (!_rotationModified) return;

            var cameraTransform = SimpleCameraPatch._cameraTransform;
            if (cameraTransform == null) return;

            cameraTransform.rotation = _savedRotation;
            _rotationModified = false;
        }
    }
}
