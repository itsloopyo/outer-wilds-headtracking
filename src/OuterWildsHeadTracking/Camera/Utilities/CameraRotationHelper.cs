extern alias UnityCoreModule;
using System;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;
using Transform = UnityCoreModule::UnityEngine.Transform;

namespace OuterWildsHeadTracking.Camera.Utilities
{
    /// <summary>
    /// Temporarily modifies camera localRotation and restores it on Dispose.
    /// </summary>
    public sealed class TemporaryRotationScope : IDisposable
    {
        private Quaternion _savedRotation;
        private Transform? _transform;
        private bool _isActive;

        private TemporaryRotationScope()
        {
            _savedRotation = Quaternion.identity;
            _transform = null;
            _isActive = false;
        }

        public static TemporaryRotationScope? Apply(Transform? transform, Quaternion newRotation)
        {
            if (transform == null) return null;

            var scope = new TemporaryRotationScope
            {
                _savedRotation = transform.localRotation,
                _transform = transform,
                _isActive = true
            };
            transform.localRotation = newRotation;
            return scope;
        }

        public static TemporaryRotationScope? RemoveHeadTracking(
            Transform? cameraTransform, Quaternion baseRotation)
        {
            if (cameraTransform == null) return null;

            Quaternion targetLocalRotation = cameraTransform.parent != null
                ? Quaternion.Inverse(cameraTransform.parent.rotation) * baseRotation
                : baseRotation;
            return Apply(cameraTransform, targetLocalRotation);
        }

        public static TemporaryRotationScope? ApplyBaseRotation(
            Transform? cameraTransform, Quaternion baseRotation, Quaternion headTrackingRotation)
        {
            if (cameraTransform == null) return null;

            Quaternion headTrackedWorld = baseRotation * headTrackingRotation;
            Quaternion targetLocalRotation = cameraTransform.parent != null
                ? Quaternion.Inverse(cameraTransform.parent.rotation) * headTrackedWorld
                : headTrackedWorld;
            return Apply(cameraTransform, targetLocalRotation);
        }

        public void Dispose()
        {
            if (!_isActive || _transform == null) return;
            _transform.localRotation = _savedRotation;
            _isActive = false;
            _transform = null;
        }
    }
}
