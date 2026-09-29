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
        private static readonly RotationPatchHelper _lineOfSightHelper = new RotationPatchHelper();
        private static readonly RotationPatchHelper _mapViewHelper = new RotationPatchHelper();

        public static void ApplyPatches(Harmony harmony)
        {
            var trackerType = AccessTools.TypeByName("ReferenceFrameTracker");
            if (trackerType == null)
                throw new InvalidOperationException("Could not find ReferenceFrameTracker type!");

            var findInLineOfSightMethod = AccessTools.Method(trackerType, "FindReferenceFrameInLineOfSight");
            if (findInLineOfSightMethod == null)
                throw new InvalidOperationException("Could not find ReferenceFrameTracker.FindReferenceFrameInLineOfSight method!");

            var findInMapViewMethod = AccessTools.Method(trackerType, "FindReferenceFrameInMapView");
            if (findInMapViewMethod == null)
                throw new InvalidOperationException("Could not find ReferenceFrameTracker.FindReferenceFrameInMapView method!");

            harmony.Patch(findInLineOfSightMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ReferenceFrameTrackerPatch), nameof(LineOfSight_Prefix))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ReferenceFrameTrackerPatch), nameof(LineOfSight_Postfix))));

            harmony.Patch(findInMapViewMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ReferenceFrameTrackerPatch), nameof(MapView_Prefix))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ReferenceFrameTrackerPatch), nameof(MapView_Postfix))));
        }

        public static void LineOfSight_Prefix() => _lineOfSightHelper.BeginPatch();
        public static void LineOfSight_Postfix() => _lineOfSightHelper.EndPatch();
        public static void MapView_Prefix() => _mapViewHelper.BeginPatch();
        public static void MapView_Postfix() => _mapViewHelper.EndPatch();
    }
}
