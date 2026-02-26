extern alias UnityCoreModule;
using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Utilities;
using OuterWildsHeadTracking.Camera.Core;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;
using Transform = UnityCoreModule::UnityEngine.Transform;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// Patches for Nomai Translator tool - ensures translator raycasting uses head direction.
    /// Uses TemporaryWorldRotationScope to apply head tracking to the translator's raycast transform.
    /// </summary>
    public static class NomaiTranslatorPatches
    {
        private static Transform? _translatorRaycastTransform = null;
        private static TemporaryWorldRotationScope? _scope;

        public static void ApplyPatches(Harmony harmony)
        {
            var nomaiTranslatorType = AccessTools.TypeByName("NomaiTranslator");
            if (nomaiTranslatorType == null)
            {
                throw new InvalidOperationException("Could not find NomaiTranslator type!");
            }

            var translatorUpdateMethod = AccessTools.Method(nomaiTranslatorType, "Update");
            if (translatorUpdateMethod == null)
            {
                throw new InvalidOperationException("Could not find NomaiTranslator.Update method!");
            }

            var translatorPrefix = AccessTools.Method(typeof(NomaiTranslatorPatches), nameof(NomaiTranslator_Update_Prefix));
            var translatorPostfix = AccessTools.Method(typeof(NomaiTranslatorPatches), nameof(NomaiTranslator_Update_Postfix));

            if (translatorPrefix == null || translatorPostfix == null)
            {
                throw new InvalidOperationException("Could not find NomaiTranslatorPatches prefix/postfix methods!");
            }

            harmony.Patch(translatorUpdateMethod,
                prefix: new HarmonyMethod(translatorPrefix),
                postfix: new HarmonyMethod(translatorPostfix));
        }

        public static void NomaiTranslator_Update_Prefix(object __instance)
        {
            var mod = HeadTrackingMod.Instance;
            if (mod == null || !mod.IsTrackingEnabled()) return;

            if (_translatorRaycastTransform == null)
            {
                var raycastField = AccessTools.Field(__instance.GetType(), "_raycastTransform");
                if (raycastField == null)
                {
                    throw new InvalidOperationException("Could not find _raycastTransform field on NomaiTranslator!");
                }
                _translatorRaycastTransform = raycastField.GetValue(__instance) as Transform;
            }

            if (_translatorRaycastTransform == null) return;

            var headTracking = SimpleCameraPatch._lastHeadTrackingRotation;
            if (headTracking == Quaternion.identity) return;

            var baseRotation = SimpleCameraPatch._baseRotationBeforeHeadTracking;
            _scope = TemporaryWorldRotationScope.ApplyHeadTracking(_translatorRaycastTransform, baseRotation, headTracking);
        }

        public static void NomaiTranslator_Update_Postfix()
        {
            _scope?.Dispose();
            _scope = null;
        }
    }
}
