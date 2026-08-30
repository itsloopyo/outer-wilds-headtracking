extern alias UnityCoreModule;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;

namespace OuterWildsHeadTracking.Camera.Utilities
{
    /// <summary>
    /// Local copy of the unset-quaternion guard in cameraunlock-core's
    /// CameraUnlock.Core.Unity/Tracking/TemporaryRotationScope.cs. This assembly
    /// reaches UnityEngine through extern aliases, so its Quaternion is a
    /// different type identity from core's and the shared one cannot be
    /// referenced. Fix both together.
    /// </summary>
    public static class QuaternionGuards
    {
        // Unity overloads Quaternion.operator== as Dot(a, b) > 0.999999f, and the dot of
        // any quaternion with the all-zero default is 0 - so "q == default" is false even
        // for default itself. Written that way the guard never fires, letting an unset
        // base rotation through to the transform as the zero quaternion, which Unity
        // rejects with "Quaternion To Matrix conversion failed".
        public static bool IsUnset(this Quaternion rotation)
        {
            return rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f;
        }
    }
}
