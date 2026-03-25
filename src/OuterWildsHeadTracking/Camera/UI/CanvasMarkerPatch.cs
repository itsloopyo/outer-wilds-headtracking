using HarmonyLib;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// Previously patched WorldToCanvasPosition and OffScreenIndicator to apply head tracking
    /// rotation during marker calculations. No longer needed: SimpleCameraPatch.Update_Postfix
    /// bakes head tracking into the camera transform, so projection methods naturally include it.
    /// </summary>
    public static class CanvasMarkerPatch
    {
        public static void ApplyPatches(Harmony harmony)
        {
            // No patches needed — camera transform already has head tracking applied.
        }
    }
}
