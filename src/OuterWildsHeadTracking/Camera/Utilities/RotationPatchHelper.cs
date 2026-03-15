extern alias UnityCoreModule;
using OuterWildsHeadTracking.Camera.Core;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;

namespace OuterWildsHeadTracking.Camera.Utilities
{
    public enum RotationPatchMode
    {
        RemoveHeadTracking,
        ApplyHeadTracking
    }

    /// <summary>
    /// Manages a TemporaryRotationScope for a single patch.
    /// Call BeginPatch in prefix and EndPatch in postfix.
    /// </summary>
    public class RotationPatchHelper
    {
        private TemporaryRotationScope? _scope;
        private readonly RotationPatchMode _mode;

        public RotationPatchHelper(RotationPatchMode mode)
        {
            _mode = mode;
        }

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

        public void EndPatch()
        {
            _scope?.Dispose();
            _scope = null;
        }
    }
}
