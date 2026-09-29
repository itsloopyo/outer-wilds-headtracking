extern alias UnityCoreModule;
using System;
using HarmonyLib;
using OuterWildsHeadTracking.Camera.Core;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;
using Vector3 = UnityCoreModule::UnityEngine.Vector3;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// Points signal detection where the head is looking. On foot GetScopeDirection
    /// returns the signalscope tool's own forward rather than the camera's, so the
    /// head-tracked camera direction is substituted for it.
    /// </summary>
    public static class SignalscopePatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            var signalscopeType = AccessTools.TypeByName("Signalscope");
            if (signalscopeType == null)
            {
                throw new InvalidOperationException("Could not find Signalscope type!");
            }

            var getScopeDirectionMethod = AccessTools.Method(signalscopeType, "GetScopeDirection");
            if (getScopeDirectionMethod == null)
            {
                throw new InvalidOperationException("Could not find Signalscope.GetScopeDirection method!");
            }

            var scopeDirPostfix = AccessTools.Method(typeof(SignalscopePatches), nameof(Signalscope_GetScopeDirection_Postfix));
            if (scopeDirPostfix == null)
            {
                throw new InvalidOperationException("Could not find Signalscope_GetScopeDirection_Postfix method!");
            }

            harmony.Patch(getScopeDirectionMethod, postfix: new HarmonyMethod(scopeDirPostfix));
        }

        public static void Signalscope_GetScopeDirection_Postfix(ref Vector3 __result)
        {
            // At the flight console the game scans along the ship's forward, not the camera's.
            if (PlayerState.AtFlightConsole()) return;

            var mod = HeadTrackingMod.Instance;
            if (mod == null || !mod.IsTrackingEnabled()) return;

            if (SimpleCameraPatch._cameraTransform == null) return;
            if (!SimpleCameraPatch._baseRotationCaptured) return;

            var local = SimpleCameraPatch._gameLocalRotation;
            var headTracking = SimpleCameraPatch._lastHeadTrackingRotation;
            if (headTracking != Quaternion.identity)
            {
                local *= headTracking;
            }
            __result = SimpleCameraPatch.ToWorld(local) * Vector3.forward;
        }
    }
}
