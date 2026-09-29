extern alias UnityCoreModule;
extern alias UnityUIModule;
using OuterWildsHeadTracking.Camera.Core;
using Vector2 = UnityCoreModule::UnityEngine.Vector2;
using Vector3 = UnityCoreModule::UnityEngine.Vector3;
using GameObject = UnityCoreModule::UnityEngine.GameObject;
using RectTransform = UnityCoreModule::UnityEngine.RectTransform;
using MonoBehaviour = UnityCoreModule::UnityEngine.MonoBehaviour;
using Screen = UnityCoreModule::UnityEngine.Screen;
using Canvas = UnityUIModule::UnityEngine.Canvas;

namespace OuterWildsHeadTracking.Camera.UI
{
    /// <summary>
    /// Moves the game's reticle, and the centre prompt list with it, to where the clean
    /// aim sits in the head-tracked view. Driven from the player camera's onPreRender.
    ///
    /// The aim is projected as a direction, with no raycast for depth. The game's
    /// interaction rays (FirstPersonManipulator) start at the camera transform's position,
    /// and the lean is on that same transform while they run and while the frame renders,
    /// so the aim ray and the render share one eye and every point along the ray projects
    /// to the same pixel.
    /// </summary>
    public class ReticleUpdater : MonoBehaviour
    {
        private const int CACHE_RETRY_INTERVAL = 60;

        private static ReticleUpdater _instance = null!;
        private RectTransform _reticleTransform = null!;
        private Vector2 _reticleHomePosition;
        private bool _reticleMoved = false;
        private RectTransform _centerPromptTransform = null!;
        private Canvas _centerPromptCanvas = null!;
        private Vector2 _centerPromptHomePosition;
        private bool _centerPromptMoved = false;
        private int _lastCacheAttemptFrame = -CACHE_RETRY_INTERVAL - 1;

        public static ReticleUpdater GetInstance()
        {
            return _instance;
        }

        public static void Create()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("ReticleUpdater");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ReticleUpdater>();
        }

        public void UpdateReticlePosition(UnityCoreModule::UnityEngine.Camera playerCamera)
        {
            // The HUD is rebuilt on every loop, so the references go stale and are found
            // again. GameObject.Find is too slow to run every frame, hence the throttle.
            if (_reticleTransform == null || _centerPromptTransform == null)
            {
                int currentFrame = UnityCoreModule::UnityEngine.Time.frameCount;
                if (currentFrame - _lastCacheAttemptFrame > CACHE_RETRY_INTERVAL)
                {
                    _lastCacheAttemptFrame = currentFrame;
                    CacheReferences();
                }
            }

            if (_reticleTransform == null) return;

            var aimDirection = SimpleCameraPatch.ToWorld(SimpleCameraPatch._gameLocalRotation) * Vector3.forward;
            var cameraTransform = playerCamera.transform;
            var screenPoint = playerCamera.WorldToScreenPoint(cameraTransform.position + aimDirection);

            _reticleTransform.position = new Vector3(screenPoint.x, screenPoint.y, 0);
            _reticleMoved = true;

            // Move the center screen-prompt list (interact glyph/text) with the
            // reticle, preserving its designed offset from screen centre.
            if (_centerPromptTransform != null && _centerPromptCanvas != null)
            {
                var pixelDelta = new Vector2(
                    screenPoint.x - Screen.width * 0.5f,
                    screenPoint.y - Screen.height * 0.5f);
                _centerPromptTransform.anchoredPosition =
                    _centerPromptHomePosition + pixelDelta / _centerPromptCanvas.scaleFactor;
                _centerPromptMoved = true;
            }
        }

        private void CacheReferences()
        {
            if (_reticleTransform == null)
            {
                var reticleObject = GameObject.Find("Reticule/Image");
                if (reticleObject != null)
                {
                    _reticleTransform = reticleObject.GetComponent<RectTransform>();
                    _reticleHomePosition = _reticleTransform.anchoredPosition;
                    _reticleMoved = false;
                }
            }

            if (_centerPromptTransform == null)
            {
                var promptManager = Locator.GetPromptManager();
                var centerList = promptManager != null
                    ? promptManager.GetScreenPromptList(PromptPosition.Center)
                    : null;
                if (centerList != null)
                {
                    _centerPromptTransform = centerList.GetComponent<RectTransform>();
                    _centerPromptCanvas = centerList.GetComponentInParent<Canvas>();
                    _centerPromptHomePosition = _centerPromptTransform.anchoredPosition;
                    _centerPromptMoved = false;
                }
            }
        }

        public void RestoreReticlePosition()
        {
            if (!_reticleMoved) return;
            _reticleMoved = false;
            if (_reticleTransform != null)
            {
                _reticleTransform.anchoredPosition = _reticleHomePosition;
            }
        }

        public void RestoreCenterPromptPosition()
        {
            if (!_centerPromptMoved) return;
            _centerPromptMoved = false;
            if (_centerPromptTransform != null)
            {
                _centerPromptTransform.anchoredPosition = _centerPromptHomePosition;
            }
        }
    }
}
