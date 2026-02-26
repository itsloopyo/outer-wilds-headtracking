using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Utilities;

namespace OuterWildsHeadTracking.Camera.Effects
{
    /// <summary>
    /// Patches FogLight.UpdateFogLight to ensure head tracking is applied during WorldToScreenPoint calculations.
    /// This fixes anglerfish lights and other fog lights from flickering with head movement.
    /// </summary>
    public static class FogLightPatch
    {
        private static readonly RotationPatchHelper _helper = new RotationPatchHelper(RotationPatchMode.ApplyHeadTracking);

        public static void ApplyPatches(Harmony harmony)
        {
            var fogLightType = AccessTools.TypeByName("FogLight");
            if (fogLightType == null)
            {
                throw new InvalidOperationException("Could not find FogLight type!");
            }

            var updateFogLightMethod = AccessTools.Method(fogLightType, "UpdateFogLight");
            if (updateFogLightMethod == null)
            {
                throw new InvalidOperationException("Could not find FogLight.UpdateFogLight method!");
            }

            var prefixMethod = AccessTools.Method(typeof(FogLightPatch), nameof(UpdateFogLight_Prefix));
            var postfixMethod = AccessTools.Method(typeof(FogLightPatch), nameof(UpdateFogLight_Postfix));

            if (prefixMethod == null || postfixMethod == null)
            {
                throw new InvalidOperationException("Could not find FogLightPatch prefix/postfix methods!");
            }

            harmony.Patch(updateFogLightMethod,
                prefix: new HarmonyMethod(prefixMethod),
                postfix: new HarmonyMethod(postfixMethod));
        }

        public static void UpdateFogLight_Prefix() => _helper.BeginPatch();

        public static void UpdateFogLight_Postfix() => _helper.EndPatch();
    }
}
