extern alias UnityCoreModule;
using OuterWildsHeadTracking.Camera.Core;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;
using Transform = UnityCoreModule::UnityEngine.Transform;

namespace OuterWildsHeadTracking.Camera.Utilities
{
    /// <summary>
    /// Takes head tracking off the camera for the length of one game method.
    /// Call BeginPatch in the prefix and EndPatch in the postfix. One instance per
    /// patched method: a method that re-entered itself would overwrite the saved
    /// rotation.
    /// </summary>
    public class RotationPatchHelper
    {
        private Transform? _transform;
        private Quaternion _savedLocalRotation;

        public void BeginPatch()
        {
            var mod = HeadTrackingMod.Instance;
            if (mod == null || !mod.IsTrackingEnabled()) return;

            var cameraTransform = SimpleCameraPatch._cameraTransform;
            if (cameraTransform == null) return;

            if (SimpleCameraPatch._lastHeadTrackingRotation == Quaternion.identity) return;

            _transform = cameraTransform;
            _savedLocalRotation = cameraTransform.localRotation;
            cameraTransform.localRotation = SimpleCameraPatch._gameLocalRotation;
        }

        public void EndPatch()
        {
            if (_transform == null) return;
            _transform.localRotation = _savedLocalRotation;
            _transform = null;
        }
    }
}
