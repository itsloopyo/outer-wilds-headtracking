extern alias UnityCoreModule;
using OuterWildsHeadTracking.Camera.Core;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;

namespace OuterWildsHeadTracking.Camera.Utilities
{
    /// <summary>
    /// Helper for patches that need to temporarily modify camera rotation.
    /// Provides common prefix/postfix logic to reduce code duplication.
    /// </summary>
    public enum RotationPatchMode
    {
        /// <summary>
        /// Remove head tracking - camera points at base/reticle direction.
        /// Use for raycasts, screen position calculations, fog lights, etc.
        /// </summary>
        RemoveHeadTracking,

        /// <summary>
        /// Apply head tracking to base rotation.
        /// Use when the original code doesn't account for head tracking.
        /// </summary>
        ApplyHeadTracking
    }

    /// <summary>
    /// Manages a TemporaryRotationScope for a single patch.
    /// Create one instance per patch class and call BeginPatch/EndPatch in prefix/postfix.
    /// </summary>
    public class RotationPatchHelper
    {
        private TemporaryRotationScope? _scope;
        private readonly RotationPatchMode _mode;

        public RotationPatchHelper(RotationPatchMode mode)
        {
            _mode = mode;
        }

        /// <summary>
        /// Call in patch prefix. Returns true if rotation was modified.
        /// </summary>
        public bool BeginPatch()
        {
            var mod = HeadTrackingMod.Instance;
            if (mod == null || !mod.IsTrackingEnabled()) return false;

            var cameraTransform = SimpleCameraPatch._cameraTransform;
            if (cameraTransform == null) return false;

            var headTracking = SimpleCameraPatch._lastHeadTrackingRotation;
            if (headTracking == Quaternion.identity) return false;

            var baseRotation = SimpleCameraPatch._baseRotationBeforeHeadTracking;
            if (baseRotation == default || baseRotation == Quaternion.identity) return false;

            _scope = _mode == RotationPatchMode.RemoveHeadTracking
                ? TemporaryRotationScope.RemoveHeadTracking(cameraTransform, baseRotation)
                : TemporaryRotationScope.ApplyBaseRotation(cameraTransform, baseRotation, headTracking);

            return _scope != null;
        }

        /// <summary>
        /// Call in patch postfix to restore original rotation.
        /// </summary>
        public void EndPatch()
        {
            _scope?.Dispose();
            _scope = null;
        }
    }
}
