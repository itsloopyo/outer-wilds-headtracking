extern alias UnityCoreModule;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;
using Vector3 = UnityCoreModule::UnityEngine.Vector3;

namespace OuterWildsHeadTracking.Camera.Utilities
{
    /// <summary>
    /// Utility class for composing camera rotations with head tracking.
    /// This mirrors CameraUnlock.Core.Unity.Tracking.CameraRotationComposer but uses
    /// Outer Wilds' extern-aliased Unity types for compatibility.
    /// </summary>
    public static class CameraRotationComposer
    {
        /// <summary>
        /// Creates a head tracking rotation from yaw, pitch, and roll angles.
        /// Uses Y-X-Z (yaw-pitch-roll) ordering which is standard for head tracking.
        /// </summary>
        /// <param name="yaw">Yaw angle in degrees (left/right head rotation).</param>
        /// <param name="pitch">Pitch angle in degrees (up/down head tilt).</param>
        /// <param name="roll">Roll angle in degrees (head tilt to shoulder).</param>
        /// <returns>Quaternion representing the head tracking rotation.</returns>
        public static Quaternion GetTrackingOnlyRotation(float yaw, float pitch, float roll)
        {
            Quaternion yawQ = Quaternion.AngleAxis(yaw, Vector3.up);
            Quaternion pitchQ = Quaternion.AngleAxis(pitch, Vector3.right);
            Quaternion rollQ = Quaternion.AngleAxis(roll, Vector3.forward);
            return yawQ * pitchQ * rollQ;
        }

        /// <summary>
        /// Creates a head tracking rotation with an influence multiplier.
        /// The influence scales all angles (0 = no tracking, 1 = full tracking).
        /// </summary>
        /// <param name="yaw">Yaw angle in degrees.</param>
        /// <param name="pitch">Pitch angle in degrees.</param>
        /// <param name="roll">Roll angle in degrees.</param>
        /// <param name="influence">Influence multiplier (0-1).</param>
        /// <returns>Quaternion representing the scaled head tracking rotation.</returns>
        public static Quaternion GetTrackingOnlyRotation(float yaw, float pitch, float roll, float influence)
        {
            return GetTrackingOnlyRotation(yaw * influence, pitch * influence, roll * influence);
        }
    }
}
