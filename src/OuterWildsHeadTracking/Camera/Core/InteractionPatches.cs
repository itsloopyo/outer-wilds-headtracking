using HarmonyLib;
using OuterWildsHeadTracking.Camera.Utilities;

namespace OuterWildsHeadTracking.Camera.Core
{
    /// <summary>
    /// Removes head tracking from the camera transform while the game's interaction
    /// detection runs, so interact prompts key off the reticle's aim direction
    /// instead of where the player's head is pointed.
    /// FirstPersonManipulator.LateUpdate owns the focus raycast (Nomai text, items,
    /// sockets, repair, orbs); its Update runs the InteractZone viewing-cone check.
    /// </summary>
    [HarmonyPatch(typeof(FirstPersonManipulator))]
    public static class FirstPersonManipulatorPatch
    {
        private static readonly RotationPatchHelper _updateHelper =
            new RotationPatchHelper(RotationPatchMode.RemoveHeadTracking);
        private static readonly RotationPatchHelper _lateUpdateHelper =
            new RotationPatchHelper(RotationPatchMode.RemoveHeadTracking);

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        public static void Update_Prefix() => _updateHelper.BeginPatch();

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Update_Postfix() => _updateHelper.EndPatch();

        [HarmonyPatch("LateUpdate")]
        [HarmonyPrefix]
        public static void LateUpdate_Prefix() => _lateUpdateHelper.BeginPatch();

        [HarmonyPatch("LateUpdate")]
        [HarmonyPostfix]
        public static void LateUpdate_Postfix() => _lateUpdateHelper.EndPatch();
    }

    /// <summary>
    /// ItemTool.UpdateInteract raycasts along the camera forward to place dropped
    /// items and gates droppability on the camera-vs-body look angle. Remove head
    /// tracking so items drop where the reticle points.
    /// </summary>
    [HarmonyPatch(typeof(ItemTool))]
    public static class ItemToolPatch
    {
        private static readonly RotationPatchHelper _helper =
            new RotationPatchHelper(RotationPatchMode.RemoveHeadTracking);

        [HarmonyPatch("UpdateInteract")]
        [HarmonyPrefix]
        public static void UpdateInteract_Prefix() => _helper.BeginPatch();

        [HarmonyPatch("UpdateInteract")]
        [HarmonyPostfix]
        public static void UpdateInteract_Postfix() => _helper.EndPatch();
    }
}
