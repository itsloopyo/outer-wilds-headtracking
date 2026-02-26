using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Utilities;

namespace OuterWildsHeadTracking.Camera.Effects
{
    /// <summary>
    /// Patches VisibilityObject.Update to ensure quantum objects check visibility against head-tracked rotation.
    /// </summary>
    public static class QuantumVisibilityPatch
    {
        private static readonly RotationPatchHelper _helper = new RotationPatchHelper(RotationPatchMode.ApplyHeadTracking);

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

        public static void VisibilityObject_Update_Prefix() => _helper.BeginPatch();

        public static void VisibilityObject_Update_Postfix() => _helper.EndPatch();
    }
}
