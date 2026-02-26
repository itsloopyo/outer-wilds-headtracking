using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Utilities;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// Patches ReferenceFrameTracker to use BASE rotation (reticle direction) for targeting raycasts.
    /// Without this patch, LOOK markers flicker because the raycast uses HEAD rotation,
    /// causing targets to be found/lost as the player moves their head.
    /// </summary>
    public static class ReferenceFrameTrackerPatch
    {
        private static readonly RotationPatchHelper _helper = new RotationPatchHelper(RotationPatchMode.RemoveHeadTracking);

        public static void ApplyPatches(Harmony harmony)
        {
            var trackerType = AccessTools.TypeByName("ReferenceFrameTracker");
            if (trackerType == null)
            {
                throw new InvalidOperationException("Could not find ReferenceFrameTracker type!");
            }

            // Patch FindReferenceFrameInLineOfSight
            var findInLineOfSightMethod = AccessTools.Method(trackerType, "FindReferenceFrameInLineOfSight");
            if (findInLineOfSightMethod != null)
            {
                var prefix = AccessTools.Method(typeof(ReferenceFrameTrackerPatch), nameof(FindReferenceFrame_Prefix));
                var postfix = AccessTools.Method(typeof(ReferenceFrameTrackerPatch), nameof(FindReferenceFrame_Postfix));
                harmony.Patch(findInLineOfSightMethod,
                    prefix: new HarmonyMethod(prefix),
                    postfix: new HarmonyMethod(postfix));
            }

            // Patch FindReferenceFrameInMapView
            var findInMapViewMethod = AccessTools.Method(trackerType, "FindReferenceFrameInMapView");
            if (findInMapViewMethod != null)
            {
                var prefix = AccessTools.Method(typeof(ReferenceFrameTrackerPatch), nameof(FindReferenceFrame_Prefix));
                var postfix = AccessTools.Method(typeof(ReferenceFrameTrackerPatch), nameof(FindReferenceFrame_Postfix));
                harmony.Patch(findInMapViewMethod,
                    prefix: new HarmonyMethod(prefix),
                    postfix: new HarmonyMethod(postfix));
            }
        }

        public static void FindReferenceFrame_Prefix() => _helper.BeginPatch();

        public static void FindReferenceFrame_Postfix() => _helper.EndPatch();
    }
}
