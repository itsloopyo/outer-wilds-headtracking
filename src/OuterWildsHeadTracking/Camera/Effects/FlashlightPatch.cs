extern alias UnityCoreModule;
using System;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Effects;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Core;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;

namespace OuterWildsHeadTracking.Camera.Effects
{
    /// <summary>
    /// Points the flashlight where the head is looking rather than where the body is
    /// aiming.
    ///
    /// Flashlight takes its beam direction off camera.transform.forward inside
    /// FixedUpdate, and the tracked rotation is written to the view matrix rather than to
    /// that transform - so there is no head pose on it to work from. The prefix composes
    /// the clean aim with the head pose for the length of that one method and the postfix
    /// takes it straight back off, which is what keeps everything else in the game reading
    /// the transform the game itself set.
    ///
    /// The beam LEADS the view by <see cref="HeadFollowLightSettings.DefaultMultiplier"/>
    /// rather than matching it, and the scaling is the shared one: you keep your eyes on
    /// what you turned towards, so your gaze sits past the centre of the screen and a beam
    /// matched to the view alone lands short of what you are actually looking at.
    /// </summary>
    public static class FlashlightPatch
    {
        private static Quaternion _savedRotation = Quaternion.identity;
        private static bool _rotationModified;

        /// <summary>How far the beam turns relative to the head. Read from the mod config.</summary>
        public static float Multiplier { get; set; } = HeadFollowLightSettings.DefaultMultiplier;

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

            _savedRotation = cameraTransform.rotation;
            cameraTransform.rotation = baseRotation * Lead(headTracking);
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

        private static Quaternion Lead(Quaternion headTracking)
        {
            Quat4 scaled = HeadFollowLightSettings.ScaleRotation(
                new Quat4(headTracking.x, headTracking.y, headTracking.z, headTracking.w),
                Multiplier);
            return new Quaternion(scaled.X, scaled.Y, scaled.Z, scaled.W);
        }
    }
}
