extern alias UnityCoreModule;
using OuterWildsHeadTracking.Camera.Utilities;
using OuterWildsHeadTracking.Camera.Core;
using Vector3 = UnityCoreModule::UnityEngine.Vector3;
using GameObject = UnityCoreModule::UnityEngine.GameObject;
using RectTransform = UnityCoreModule::UnityEngine.RectTransform;
using MonoBehaviour = UnityCoreModule::UnityEngine.MonoBehaviour;
using Physics = UnityEngine.Physics;
using QueryTriggerInteraction = UnityEngine.QueryTriggerInteraction;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// MonoBehaviour that updates reticle position in LateUpdate
    /// This runs AFTER all game Update logic, ensuring we override the game's reticle positioning
    /// </summary>
    public class ReticleUpdater : MonoBehaviour
    {
        private const float MaxRaycastDistance = 1000f;
        private const float MinRaycastDistance = 0.5f;
        private const float DistanceSmoothingRate = 15f;
        private static float _lastHitDistance = 100f;

        private static ReticleUpdater _instance = null!;
        private RectTransform _reticleTransform = null!;
        private UnityCoreModule::UnityEngine.Camera _mainCamera = null!;
        private int _lastCacheAttemptFrame = -1;
        private const int CACHE_RETRY_INTERVAL = 60;

        public static ReticleUpdater GetInstance()
        {
            return _instance;
        }

        public static void Create()
        {
            var mod = HeadTrackingMod.Instance;

            if (_instance != null)
            {
                return;
            }

            // Create a new GameObject for the updater
            var go = new GameObject("ReticleUpdater");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ReticleUpdater>();

        }

        private void Start()
        {
            // Find the reticle GameObject
            var reticleObject = GameObject.Find("Reticule/Image");
            if (reticleObject != null)
            {
                _reticleTransform = reticleObject.GetComponent<RectTransform>();
            }

            _mainCamera = UnityCoreModule::UnityEngine.Camera.main;
        }

        public void UpdateReticlePosition()
        {
            int currentFrame = UnityCoreModule::UnityEngine.Time.frameCount;

            // Re-acquire references if they've become stale (e.g., after death/respawn)
            // Only retry every CACHE_RETRY_INTERVAL frames to avoid repeated expensive GameObject.Find calls
            if (_reticleTransform == null && (currentFrame - _lastCacheAttemptFrame) > CACHE_RETRY_INTERVAL)
            {
                _lastCacheAttemptFrame = currentFrame;
                var reticleObject = GameObject.Find("Reticule/Image");
                if (reticleObject != null)
                {
                    _reticleTransform = reticleObject.GetComponent<RectTransform>();
                }
            }

            if (_mainCamera == null && (currentFrame - _lastCacheAttemptFrame) > CACHE_RETRY_INTERVAL)
            {
                _lastCacheAttemptFrame = currentFrame;
                _mainCamera = UnityCoreModule::UnityEngine.Camera.main;
            }

            if (_reticleTransform == null || _mainCamera == null) return;

            // Get the base camera rotation (without head tracking)
            var baseRotation = SimpleCameraPatch._baseRotationBeforeHeadTracking;
            if (baseRotation == default) return;

            // Raycast along the base aim direction to find the actual target distance.
            var baseForward = baseRotation * Vector3.forward;
            Vector3 aimOrigin = _mainCamera.transform.position;

            UnityEngine.RaycastHit hit;
            if (Physics.Raycast(aimOrigin, baseForward, out hit, MaxRaycastDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.distance >= MinRaycastDistance)
            {
                float t = 1f - UnityCoreModule::UnityEngine.Mathf.Exp(-DistanceSmoothingRate * UnityCoreModule::UnityEngine.Time.deltaTime);
                _lastHitDistance = UnityCoreModule::UnityEngine.Mathf.Lerp(_lastHitDistance, hit.distance, t);
            }

            var screenPoint = _mainCamera.WorldToScreenPoint(aimOrigin + baseForward * _lastHitDistance);

            // Update reticle position to match base aim direction
            _reticleTransform.position = new Vector3(screenPoint.x, screenPoint.y, 0);
        }
    }
}
