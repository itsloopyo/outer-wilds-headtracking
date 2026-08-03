using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Utilities;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// Removes head tracking from the camera transform during NomaiTranslator.Update
    /// so the translator raycast targets the text under the reticle. Must match
    /// InteractionPatches: FirstPersonManipulator focuses Nomai text along the clean
    /// aim, so the translator has to scan the same direction or the "translate"
    /// prompt and the actual translation target disagree.
    /// </summary>
    public static class NomaiTranslatorPatches
    {
        private static readonly RotationPatchHelper _helper =
            new RotationPatchHelper(RotationPatchMode.RemoveHeadTracking);

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

        public static void Prefix() => _helper.BeginPatch();

        public static void Postfix() => _helper.EndPatch();
    }
}
