using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Utilities;

namespace OuterWildsHeadTracking.Camera.Effects
{
    /// <summary>
    /// Patches Flashlight to prevent flickering when head tracking is active.
    /// Ensures head-tracked rotation is applied during FixedUpdate.
    /// </summary>
    public static class FlashlightPatch
    {
        private static readonly RotationPatchHelper _helper = new RotationPatchHelper(RotationPatchMode.ApplyHeadTracking);

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

        public static void FixedUpdate_Prefix() => _helper.BeginPatch();

        public static void FixedUpdate_Postfix() => _helper.EndPatch();
    }
}
