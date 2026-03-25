extern alias UnityCoreModule;
using System;
using HarmonyLib;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using OuterWildsHeadTracking.Configuration;
using OuterWildsHeadTracking.Tracking;
using OuterWildsHeadTracking.Camera.Utilities;
using OuterWildsHeadTracking.Camera.Effects;
using OuterWildsHeadTracking.Camera.UI;
using OuterWildsHeadTracking.Utilities;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;
using Vector3 = UnityCoreModule::UnityEngine.Vector3;

namespace OuterWildsHeadTracking.Camera.Core
{
    /// <summary>
    /// Core camera patch that applies head tracking to the player camera transform.
    /// Rotation and position are applied in Update_Postfix. The game resets localRotation
    /// each frame via its own UpdateRotation, so no save/restore is needed for rotation.
    /// Position is cleaned up in FixedUpdate_Prefix/Update_Prefix before game logic.
    /// </summary>
    [HarmonyPatch(typeof(PlayerCameraController))]
    public class SimpleCameraPatch
    {
        private static bool _centerSet = false;
        private static int _framesWithoutData = 0;
        private static float _secondsWithoutData = 0f;
        private static int _stabilizationFramesRemaining = 0;
        private const float TRACKING_LOSS_FADE_DELAY_SECONDS = 0.5f;
        private const float TRACKING_LOSS_FADE_SPEED = 2.0f;
        private const int STABILIZATION_FRAME_COUNT = 10;

        public static Quaternion _lastHeadTrackingRotation = Quaternion.identity;
        public static Quaternion _baseRotationBeforeHeadTracking = Quaternion.identity;
        public static UnityCoreModule::UnityEngine.Transform? _cameraTransform = null;

        private static float _lastGameDegreesX = 0f;
        private static float _lastGameDegreesY = 0f;
        private static float _gameCameraChangeSpeed = 0f;

        private static AccessTools.FieldRef<PlayerCameraController, float>? _degreesXRef;
        private static AccessTools.FieldRef<PlayerCameraController, float>? _degreesYRef;

        public static float _smoothedYaw = 0f;
        public static float _smoothedPitch = 0f;
        public static float _smoothedRoll = 0f;

        // Position tracking state
        public static Vec3 _lastPositionOffset = Vec3.Zero;
        private static bool _positionOffsetApplied = false;

        // HUD camera save/restore state
        private static Quaternion _savedHudCameraRotation;
        private static bool _hudCameraModified = false;

        // Frame coordination for tracking data drain
        public static int _lastDrainedFrame = -1;

        public static void RecenterTracking()
        {
            _centerSet = false;
            _smoothedYaw = 0f;
            _smoothedPitch = 0f;
            _smoothedRoll = 0f;
            _lastPositionOffset = Vec3.Zero;
            _positionOffsetApplied = false;
            HeadTrackingMod.Instance?.GetTrackingClient()?.ResetProcessor();
        }

        [HarmonyPatch("FixedUpdate")]
        [HarmonyPrefix]
        public static void FixedUpdate_Prefix(PlayerCameraController __instance)
        {
            if (!_positionOffsetApplied) return;
            var t = __instance.transform;
            t.localPosition -= new Vector3(
                _lastPositionOffset.X, _lastPositionOffset.Y, _lastPositionOffset.Z);
            _positionOffsetApplied = false;
        }

        [HarmonyPatch("FixedUpdate")]
        [HarmonyPostfix]
        public static void FixedUpdate_Postfix(PlayerCameraController __instance)
        {
            if (_lastHeadTrackingRotation == Quaternion.identity) return;

            var cameraTransform = __instance.transform;
            if (cameraTransform == null) return;

            // Lazy-init field refs (FixedUpdate may run before first Update)
            if (_degreesXRef == null || _degreesYRef == null)
            {
                _degreesXRef = FastFieldRef.Create<PlayerCameraController, float>("_degreesX");
                _degreesYRef = FastFieldRef.Create<PlayerCameraController, float>("_degreesY");
            }

            // Re-apply head tracking rotation (game's UpdateRotation just reset it)
            float degreesX = _degreesXRef(__instance);
            float degreesY = _degreesYRef(__instance);
            var gameWantedRotation = Quaternion.Euler(-degreesY, degreesX, 0f);

            _baseRotationBeforeHeadTracking = cameraTransform.parent != null
                ? cameraTransform.parent.rotation * gameWantedRotation
                : gameWantedRotation;

            cameraTransform.localRotation = gameWantedRotation * _lastHeadTrackingRotation;

            // Re-apply position offset
            if (_lastPositionOffset.X != 0f || _lastPositionOffset.Y != 0f || _lastPositionOffset.Z != 0f)
            {
                cameraTransform.localPosition += new Vector3(
                    _lastPositionOffset.X, _lastPositionOffset.Y, _lastPositionOffset.Z);
                _positionOffsetApplied = true;
            }
        }

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        public static void Update_Prefix(PlayerCameraController __instance)
        {
            if (!_positionOffsetApplied) return;
            var t = __instance.transform;
            t.localPosition -= new Vector3(
                _lastPositionOffset.X, _lastPositionOffset.Y, _lastPositionOffset.Z);
            _positionOffsetApplied = false;
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void Update_Postfix(PlayerCameraController __instance)
        {
            var cameraTransform = __instance.transform;
            if (cameraTransform == null)
            {
                return;
            }

            _cameraTransform = cameraTransform;

            if (_degreesXRef == null || _degreesYRef == null)
            {
                _degreesXRef = FastFieldRef.Create<PlayerCameraController, float>("_degreesX");
                _degreesYRef = FastFieldRef.Create<PlayerCameraController, float>("_degreesY");
            }

            float gameDegreesX = _degreesXRef(__instance);
            float gameDegreesY = _degreesYRef(__instance);

            float deltaX = gameDegreesX - _lastGameDegreesX;
            float deltaY = gameDegreesY - _lastGameDegreesY;
            _gameCameraChangeSpeed = UnityCoreModule::UnityEngine.Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY);
            _lastGameDegreesX = gameDegreesX;
            _lastGameDegreesY = gameDegreesY;

            var gameWantedRotation = Quaternion.Euler(-gameDegreesY, gameDegreesX, 0f);

            _baseRotationBeforeHeadTracking = cameraTransform.parent != null
                ? cameraTransform.parent.rotation * gameWantedRotation
                : gameWantedRotation;

            var mod = HeadTrackingMod.Instance;
            if (mod == null || !mod.IsTrackingEnabled())
            {
                _centerSet = false;
                _lastHeadTrackingRotation = Quaternion.identity;
                return;
            }

            if (OWTime.IsPaused(OWTime.PauseType.Menu))
            {
                _lastHeadTrackingRotation = Quaternion.identity;
                return;
            }

            var trackingClient = mod.GetTrackingClient();
            if (trackingClient == null)
            {
                _lastHeadTrackingRotation = Quaternion.identity;
                return;
            }

            // Use unscaledDeltaTime: head tracking must respond in real time even when
            // the game is paused (e.g. PauseType.Reading while using the Nomai translator).
            float deltaTime = UnityCoreModule::UnityEngine.Time.unscaledDeltaTime;

            int currentFrame = UnityCoreModule::UnityEngine.Time.frameCount;
            if (_lastDrainedFrame != currentFrame)
            {
                trackingClient.PeekRawEulerAngles();
                _lastDrainedFrame = currentFrame;
            }

            var rawAngles = trackingClient.PeekRawEulerAngles();

            HandleTrackingLoss(rawAngles, deltaTime);

            if (!_centerSet)
            {
                if (!rawAngles.IsValid)
                {
                    _lastHeadTrackingRotation = CameraRotationComposer.GetTrackingOnlyRotation(
                        _smoothedYaw, _smoothedPitch, _smoothedRoll);
                    cameraTransform.localRotation = gameWantedRotation * _lastHeadTrackingRotation;
                    return;
                }

                if (_stabilizationFramesRemaining > 0)
                {
                    _stabilizationFramesRemaining--;
                    float t = 1f - UnityCoreModule::UnityEngine.Mathf.Exp(-TRACKING_LOSS_FADE_SPEED * deltaTime);
                    _smoothedYaw *= (1f - t);
                    _smoothedPitch *= (1f - t);
                    _smoothedRoll *= (1f - t);
                    _lastHeadTrackingRotation = CameraRotationComposer.GetTrackingOnlyRotation(
                        _smoothedYaw, _smoothedPitch, _smoothedRoll);
                    cameraTransform.localRotation = gameWantedRotation * _lastHeadTrackingRotation;
                    return;
                }

                SetCenter(rawAngles, mod);
            }

            ComputeHeadTracking(rawAngles, mod, deltaTime);
            cameraTransform.localRotation = gameWantedRotation * _lastHeadTrackingRotation;
        }

        [HarmonyPatch("UpdateLockOnTargeting")]
        [HarmonyPrefix]
        public static bool UpdateLockOnTargeting_Prefix(PlayerCameraController __instance)
        {
            var mod = HeadTrackingMod.Instance;
            if (mod == null || !mod.IsTrackingEnabled())
            {
                return true;
            }
            return false;
        }

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void Start_Postfix(PlayerCameraController __instance)
        {
            RecenterTracking();

            var mod = HeadTrackingMod.Instance;
            if (mod == null) return;

            ReticleUpdater.Create();
            if (mod.ModHelper != null)
                PlayerHeadHider.LogNearbyRenderers(__instance.transform, mod.ModHelper);
            UnityCoreModule::UnityEngine.Camera.onPreRender -= OnCameraPreRender;
            UnityCoreModule::UnityEngine.Camera.onPreRender += OnCameraPreRender;
            UnityCoreModule::UnityEngine.Camera.onPreCull -= OnCameraPreCull;
            UnityCoreModule::UnityEngine.Camera.onPreCull += OnCameraPreCull;
            UnityCoreModule::UnityEngine.Camera.onPostRender -= OnCameraPostRender;
            UnityCoreModule::UnityEngine.Camera.onPostRender += OnCameraPostRender;
        }

        private static void OnCameraPreRender(UnityCoreModule::UnityEngine.Camera cam)
        {
            if (cam != UnityCoreModule::UnityEngine.Camera.main) return;
            if (_cameraTransform == null) return;
            if (_lastHeadTrackingRotation == Quaternion.identity) return;

            ReticleUpdater.GetInstance()?.UpdateReticlePosition();
        }

        /// <summary>
        /// Applies head tracking rotation to the HUD camera before it renders.
        /// The HUD camera renders to a "HelmetHUD" RenderTexture that is composited
        /// onto the helmet visor. Without this, the HUD camera sees canvas markers
        /// at un-rotated positions, producing ghost markers on the visor overlay.
        /// </summary>
        private static void OnCameraPreCull(UnityCoreModule::UnityEngine.Camera cam)
        {
            if (cam.targetTexture == null || cam.targetTexture.name != "HelmetHUD") return;
            if (_cameraTransform == null) return;
            if (_lastHeadTrackingRotation == Quaternion.identity) return;

            _savedHudCameraRotation = cam.transform.rotation;
            cam.transform.rotation = _cameraTransform.rotation;
            _hudCameraModified = true;
        }

        private static void OnCameraPostRender(UnityCoreModule::UnityEngine.Camera cam)
        {
            if (!_hudCameraModified) return;
            if (cam.targetTexture == null || cam.targetTexture.name != "HelmetHUD") return;

            cam.transform.rotation = _savedHudCameraRotation;
            _hudCameraModified = false;
        }

        private static void HandleTrackingLoss(OpenTrackClient.RawEulerAngles rawAngles, float deltaTime)
        {
            if (!rawAngles.IsValid)
            {
                _framesWithoutData++;
                _secondsWithoutData += deltaTime;
                _stabilizationFramesRemaining = STABILIZATION_FRAME_COUNT;

                if (_secondsWithoutData > TRACKING_LOSS_FADE_DELAY_SECONDS)
                {
                    float t = 1f - UnityCoreModule::UnityEngine.Mathf.Exp(-TRACKING_LOSS_FADE_SPEED * deltaTime);
                    _smoothedYaw *= (1f - t);
                    _smoothedPitch *= (1f - t);
                    _smoothedRoll *= (1f - t);
                    _lastHeadTrackingRotation = CameraRotationComposer.GetTrackingOnlyRotation(
                        _smoothedYaw, _smoothedPitch, _smoothedRoll);
                }

                if (_framesWithoutData > TrackingConstants.RECENTER_THRESHOLD_FRAMES && _centerSet)
                {
                    _centerSet = false;
                }
            }
            else
            {
                _framesWithoutData = 0;
                _secondsWithoutData = 0f;
            }
        }

        private static void SetCenter(OpenTrackClient.RawEulerAngles rawAngles, HeadTrackingMod mod)
        {
            mod.GetTrackingClient()?.SetCenter(rawAngles);
            _centerSet = true;
            _lastHeadTrackingRotation = Quaternion.identity;
        }

        private static void ComputeHeadTracking(OpenTrackClient.RawEulerAngles rawAngles, HeadTrackingMod mod, float deltaTime)
        {
            var trackingClient = mod.GetTrackingClient();

            var processed = trackingClient?.GetProcessedRotation(deltaTime);

            if (processed.HasValue)
            {
                float yaw = processed.Value.Yaw;
                float pitch = processed.Value.Pitch;
                float roll = processed.Value.Roll;

                float headTrackingInfluence = CalculateHeadTrackingInfluence();
                yaw *= headTrackingInfluence;
                pitch *= headTrackingInfluence;
                roll *= headTrackingInfluence;

                float smoothing = SmoothingUtils.GetEffectiveSmoothing(HeadTrackingMod.Smoothing);

                _smoothedYaw = SmoothingUtils.Smooth(_smoothedYaw, yaw, smoothing, deltaTime);
                _smoothedPitch = SmoothingUtils.Smooth(_smoothedPitch, pitch, smoothing, deltaTime);
                _smoothedRoll = SmoothingUtils.Smooth(_smoothedRoll, roll, smoothing, deltaTime);

                _lastHeadTrackingRotation = CameraRotationComposer.GetTrackingOnlyRotation(
                    _smoothedYaw, _smoothedPitch, _smoothedRoll);

                // Position tracking: apply to localPosition so markers see the offset.
                // Cleaned up in FixedUpdate_Prefix/Update_Prefix before game logic.
                if (HeadTrackingMod.PositionEnabled && trackingClient != null && _cameraTransform != null)
                {
                    var headRotQ = QuaternionUtils.FromYawPitchRoll(
                        _smoothedYaw, _smoothedPitch, _smoothedRoll);

                    Vec3 posOffset = trackingClient.GetProcessedPosition(headRotQ, deltaTime);

                    // Attenuate Z position at high pitch angles. Face trackers
                    // conflate head rotation with translation at extreme tilt,
                    // causing a forward pop when the raw Z crosses the back-limit
                    // clamp boundary. Fade Z to zero beyond 30 degrees pitch.
                    float absPitch = UnityCoreModule::UnityEngine.Mathf.Abs(_smoothedPitch);
                    float zAtten = 1f - UnityCoreModule::UnityEngine.Mathf.Clamp01(
                        (absPitch - 30f) / 20f);
                    posOffset = new Vec3(posOffset.X, posOffset.Y, posOffset.Z * zAtten);

                    Vec3 scaledPos = posOffset * headTrackingInfluence;
                    _lastPositionOffset = scaledPos;

                    _cameraTransform.localPosition += new Vector3(
                        scaledPos.X, scaledPos.Y, scaledPos.Z);
                    _positionOffsetApplied = true;
                }
            }
            else
            {
                _lastHeadTrackingRotation = CameraRotationComposer.GetTrackingOnlyRotation(
                    _smoothedYaw, _smoothedPitch, _smoothedRoll);
            }
        }

        private static float CalculateHeadTrackingInfluence()
        {
            if (_gameCameraChangeSpeed > TrackingConstants.DIALOGUE_CAMERA_SPEED_THRESHOLD)
            {
                float reduction = UnityCoreModule::UnityEngine.Mathf.Clamp01(
                    (_gameCameraChangeSpeed - TrackingConstants.DIALOGUE_CAMERA_SPEED_THRESHOLD) /
                    TrackingConstants.DIALOGUE_CAMERA_SPEED_RANGE
                );
                return UnityCoreModule::UnityEngine.Mathf.Lerp(1f, TrackingConstants.DIALOGUE_MIN_HEAD_TRACKING, reduction);
            }
            return 1f;
        }
    }
}
