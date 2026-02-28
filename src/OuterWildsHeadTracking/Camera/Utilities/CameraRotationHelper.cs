extern alias UnityCoreModule;
using System;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;
using Transform = UnityCoreModule::UnityEngine.Transform;

namespace OuterWildsHeadTracking.Camera.Utilities
{
    /// <summary>
    /// Utility for temporarily modifying camera rotation and safely restoring it.
    /// This follows the same pattern as CameraUnlock.Core.Unity.Tracking.TemporaryRotationScope.
    ///
    /// Note: Cannot use TemporaryRotationScope directly due to Unity extern alias requirements
    /// in Outer Wilds (UnityCoreModule::UnityEngine vs standard UnityEngine).
    ///
    /// Usage in prefix/postfix patches:
    /// <code>
    /// // In prefix:
    /// _scope = TemporaryRotationScope.ApplyBaseRotation(camera, baseRotation, headTracking);
    ///
    /// // In postfix:
    /// _scope?.Dispose();
    /// _scope = null;
    /// </code>
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

        /// <summary>
        /// Temporarily applies a rotation to the transform.
        /// Must be disposed to restore original rotation.
        /// </summary>
        /// <param name="transform">The transform to modify.</param>
        /// <param name="newRotation">The temporary rotation to apply.</param>
        /// <returns>A scope that will restore the original rotation when disposed, or null if transform is null.</returns>
        public static TemporaryRotationScope? Apply(Transform? transform, Quaternion newRotation)
        {
            if (transform == null)
            {
                return null;
            }

            var scope = new TemporaryRotationScope
            {
                _savedRotation = transform.localRotation,
                _transform = transform,
                _isActive = true
            };

            transform.localRotation = newRotation;
            return scope;
        }

        /// <summary>
        /// Temporarily applies the base rotation (without head tracking) to the camera.
        /// This is the most common use case - removing head tracking for operations
        /// that should use the game's intended aim direction.
        /// </summary>
        /// <param name="cameraTransform">The camera transform to modify.</param>
        /// <param name="baseRotation">The game's intended rotation (world space).</param>
        /// <param name="headTrackingRotation">The current head tracking rotation being applied.</param>
        /// <returns>A scope that will restore the head-tracked rotation when disposed, or null if not applicable.</returns>
        public static TemporaryRotationScope? ApplyBaseRotation(
            Transform? cameraTransform,
            Quaternion baseRotation,
            Quaternion headTrackingRotation)
        {
            if (cameraTransform == null)
            {
                return null;
            }

            // Skip if no valid base rotation or no head tracking applied
            if (baseRotation == default || headTrackingRotation == Quaternion.identity)
            {
                return null;
            }

            // Calculate what the local rotation should be with head tracking
            Quaternion headTrackedWorld = baseRotation * headTrackingRotation;
            Quaternion targetLocalRotation = cameraTransform.parent != null
                ? Quaternion.Inverse(cameraTransform.parent.rotation) * headTrackedWorld
                : headTrackedWorld;

            return Apply(cameraTransform, targetLocalRotation);
        }

        /// <summary>
        /// Temporarily removes head tracking from the camera rotation.
        /// The camera will point in the base aim direction during the scope.
        /// </summary>
        /// <param name="cameraTransform">The camera transform to modify.</param>
        /// <param name="baseRotation">The game's intended rotation (world space).</param>
        /// <returns>A scope that will restore the head-tracked rotation when disposed, or null if not applicable.</returns>
        public static TemporaryRotationScope? RemoveHeadTracking(
            Transform? cameraTransform,
            Quaternion baseRotation)
        {
            if (cameraTransform == null || baseRotation == default)
            {
                return null;
            }

            // Calculate local rotation without head tracking
            Quaternion targetLocalRotation = cameraTransform.parent != null
                ? Quaternion.Inverse(cameraTransform.parent.rotation) * baseRotation
                : baseRotation;

            return Apply(cameraTransform, targetLocalRotation);
        }

        /// <summary>
        /// Restores the saved rotation to the transform.
        /// Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (!_isActive || _transform == null)
            {
                return;
            }

            _transform.localRotation = _savedRotation;
            _isActive = false;
            _transform = null;
            _savedRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Variant of TemporaryRotationScope that operates on world-space rotation.
    /// Use this for transforms that are not the camera (e.g., tool raycast transforms).
    /// </summary>
    public sealed class TemporaryWorldRotationScope : IDisposable
    {
        private Quaternion _savedRotation;
        private Transform? _transform;
        private bool _isActive;

        private TemporaryWorldRotationScope()
        {
            _savedRotation = Quaternion.identity;
            _transform = null;
            _isActive = false;
        }

        /// <summary>
        /// Temporarily applies a world rotation to the transform.
        /// Must be disposed to restore original rotation.
        /// </summary>
        public static TemporaryWorldRotationScope? Apply(Transform? transform, Quaternion newWorldRotation)
        {
            if (transform == null)
            {
                return null;
            }

            var scope = new TemporaryWorldRotationScope
            {
                _savedRotation = transform.rotation,
                _transform = transform,
                _isActive = true
            };

            transform.rotation = newWorldRotation;
            return scope;
        }

        /// <summary>
        /// Temporarily applies head tracking to a raycast transform.
        /// The transform will point in the head-tracked direction.
        /// </summary>
        /// <param name="transform">The transform to modify (e.g., tool's raycast transform).</param>
        /// <param name="baseRotation">The base aim rotation (world space).</param>
        /// <param name="headTrackingRotation">The head tracking rotation to apply.</param>
        public static TemporaryWorldRotationScope? ApplyHeadTracking(
            Transform? transform,
            Quaternion baseRotation,
            Quaternion headTrackingRotation)
        {
            if (transform == null)
            {
                return null;
            }

            if (baseRotation == default || headTrackingRotation == Quaternion.identity)
            {
                return null;
            }

            return Apply(transform, baseRotation * headTrackingRotation);
        }

        /// <summary>
        /// Restores the saved rotation to the transform.
        /// Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (!_isActive || _transform == null)
            {
                return;
            }

            _transform.rotation = _savedRotation;
            _isActive = false;
            _transform = null;
            _savedRotation = Quaternion.identity;
        }
    }
}
