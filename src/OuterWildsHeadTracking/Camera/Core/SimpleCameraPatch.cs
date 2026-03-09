extern alias UnityCoreModule;
using System;
using HarmonyLib;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using OuterWildsHeadTracking.Configuration;
using OuterWildsHeadTracking.Tracking;
using OuterWildsHeadTracking.Camera.Utilities;
using OuterWildsHeadTracking.Camera.UI;
using OuterWildsHeadTracking.Utilities;
using Quaternion = UnityCoreModule::UnityEngine.Quaternion;
using Vector3 = UnityCoreModule::UnityEngine.Vector3;

namespace OuterWildsHeadTracking.Camera.Core
{
    /// <summary>
    /// Core camera patch that applies head tracking rotation to the player camera.
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

        private static float _cachedYawOffset = 0f;
        private static float _cachedPitchOffset = 0f;
        private static float _cachedRollOffset = 0f;
        private static float _cachedHeadTrackingInfluence = 1f;
        private static int _lastRotationCalcFrame = -1;
        private static Quaternion _smoothedHeadTrackingRotation = Quaternion.identity;

        // Position tracking state
        private static Vec3 _lastPositionOffset = Vec3.Zero;
        private static bool _positionOffsetApplied = false;

        public static void RecenterTracking()
        {
            _centerSet = false;
            _smoothedHeadTrackingRotation = Quaternion.identity;
            _lastPositionOffset = Vec3.Zero;
            _positionOffsetApplied = false;
            HeadTrackingMod.Instance?.GetTrackingClient()?.ResetProcessor();
        }

        /// <summary>
        /// Remove our position offset before the game's UpdateCamera lerps localPosition,
        /// so the lerp operates on the game's clean position, not our offset position.
        /// </summary>
        [HarmonyPatch("FixedUpdate")]
        [HarmonyPrefix]
        public static void FixedUpdate_Prefix(PlayerCameraController __instance)
        {
            if (!_positionOffsetApplied) return;

            var t = __instance.transform;
            var pos = t.localPosition;
            t.localPosition = pos - new Vector3(
                _lastPositionOffset.X, _lastPositionOffset.Y, _lastPositionOffset.Z);
            _positionOffsetApplied = false;
        }

        /// <summary>
        /// Also remove offset before Update, since Update calls UpdateCamera when reading-paused.
        /// </summary>
        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        public static void Update_Prefix(PlayerCameraController __instance)
        {
            if (!_positionOffsetApplied) return;

            var t = __instance.transform;
            var pos = t.localPosition;
            t.localPosition = pos - new Vector3(
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

            int currentFrame = UnityCoreModule::UnityEngine.Time.frameCount;
            if (MapMarkerPatch._lastDrainedFrame != currentFrame)
            {
                trackingClient.PeekRawEulerAngles();
                MapMarkerPatch._lastDrainedFrame = currentFrame;
            }

            var rawAngles = trackingClient.PeekRawEulerAngles();

            HandleTrackingLoss(rawAngles, mod);

            if (!_centerSet)
            {
                if (!rawAngles.IsValid)
                {
                    _lastHeadTrackingRotation = Quaternion.identity;
                    cameraTransform.localRotation = gameWantedRotation * _smoothedHeadTrackingRotation;
                    return;
                }

                if (_stabilizationFramesRemaining > 0)
                {
                    _stabilizationFramesRemaining--;
                    float t = 1f - UnityCoreModule::UnityEngine.Mathf.Exp(-TRACKING_LOSS_FADE_SPEED * UnityCoreModule::UnityEngine.Time.deltaTime);
                    _smoothedHeadTrackingRotation = Quaternion.Slerp(
                        _smoothedHeadTrackingRotation,
                        Quaternion.identity,
                        t
                    );
                    cameraTransform.localRotation = gameWantedRotation * _smoothedHeadTrackingRotation;
                    return;
                }

                SetCenter(rawAngles, mod);
            }

            ApplyHeadTracking(cameraTransform, gameWantedRotation, rawAngles, mod);

            MapMarkerPatch._headTrackingAppliedFrame = currentFrame;
            MapMarkerPatch._cameraHasHeadTracking = true;
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
            _centerSet = false;

            var mod = HeadTrackingMod.Instance;
            if (mod == null) return;

            ReticleUpdater.Create();
            UnityCoreModule::UnityEngine.Camera.onPreRender += OnCameraPreRender;
        }

        private static void OnCameraPreRender(UnityCoreModule::UnityEngine.Camera cam)
        {
            if (cam != UnityCoreModule::UnityEngine.Camera.main) return;
            if (_cameraTransform == null) return;
            if (_lastHeadTrackingRotation == Quaternion.identity) return;

            ReticleUpdater.GetInstance()?.UpdateReticlePosition();
        }

        private static void HandleTrackingLoss(OpenTrackClient.RawEulerAngles rawAngles, HeadTrackingMod mod)
        {
            if (!rawAngles.IsValid)
            {
                _framesWithoutData++;
                _secondsWithoutData += UnityCoreModule::UnityEngine.Time.deltaTime;
                _stabilizationFramesRemaining = STABILIZATION_FRAME_COUNT;

                if (_secondsWithoutData > TRACKING_LOSS_FADE_DELAY_SECONDS)
                {
                    float t = 1f - UnityCoreModule::UnityEngine.Mathf.Exp(-TRACKING_LOSS_FADE_SPEED * UnityCoreModule::UnityEngine.Time.deltaTime);
                    _smoothedHeadTrackingRotation = Quaternion.Slerp(
                        _smoothedHeadTrackingRotation,
                        Quaternion.identity,
                        t
                    );
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

        private static void ApplyHeadTracking(UnityCoreModule::UnityEngine.Transform cameraTransform,
            Quaternion gameWantedRotation, OpenTrackClient.RawEulerAngles rawAngles, HeadTrackingMod mod)
        {
            Quaternion headTrackingRotation;

            var trackingClient = mod.GetTrackingClient();
            float deltaTime = UnityCoreModule::UnityEngine.Time.deltaTime;

            var processed = trackingClient?.GetProcessedRotation(deltaTime);

            if (processed.HasValue)
            {
                int currentFrame = UnityCoreModule::UnityEngine.Time.frameCount;

                float yaw = processed.Value.Yaw;
                float pitch = processed.Value.Pitch;
                float roll = processed.Value.Roll;

                float headTrackingInfluence = CalculateHeadTrackingInfluence();

                bool needsRecalc = _lastRotationCalcFrame != currentFrame ||
                    UnityCoreModule::UnityEngine.Mathf.Abs(yaw - _cachedYawOffset) > 0.01f ||
                    UnityCoreModule::UnityEngine.Mathf.Abs(pitch - _cachedPitchOffset) > 0.01f ||
                    UnityCoreModule::UnityEngine.Mathf.Abs(roll - _cachedRollOffset) > 0.01f ||
                    UnityCoreModule::UnityEngine.Mathf.Abs(headTrackingInfluence - _cachedHeadTrackingInfluence) > 0.01f;

                if (needsRecalc)
                {
                    _cachedYawOffset = yaw;
                    _cachedPitchOffset = pitch;
                    _cachedRollOffset = roll;
                    _cachedHeadTrackingInfluence = headTrackingInfluence;
                    _lastRotationCalcFrame = currentFrame;

                    headTrackingRotation = Utilities.CameraRotationComposer.GetTrackingOnlyRotation(
                        yaw, pitch, roll, headTrackingInfluence);
                }
                else
                {
                    headTrackingRotation = _lastHeadTrackingRotation;
                }

                float smoothing = HeadTrackingMod.Smoothing;
                bool isRemote = trackingClient?.IsRemoteSource ?? false;

                if (HeadTrackingMod.AdaptiveSmoothing)
                {
                    if (isRemote)
                    {
                        smoothing += SmoothingUtils.RemoteConnectionBaseline;
                        smoothing = UnityCoreModule::UnityEngine.Mathf.Clamp01(smoothing);
                    }
                    else
                    {
                        smoothing = 0f;
                    }
                }

                float t = SmoothingUtils.CalculateSmoothingFactor(smoothing, deltaTime);
                _smoothedHeadTrackingRotation = Quaternion.Slerp(_smoothedHeadTrackingRotation, headTrackingRotation, t);
                headTrackingRotation = _smoothedHeadTrackingRotation;

                _lastHeadTrackingRotation = headTrackingRotation;
                cameraTransform.localRotation = gameWantedRotation * headTrackingRotation;

                // Position tracking
                if (HeadTrackingMod.PositionEnabled && trackingClient != null)
                {
                    // Build rotation quaternion matching the tracking rotation for neck model
                    var headRotQ = QuaternionUtils.FromYawPitchRoll(
                        yaw * headTrackingInfluence,
                        pitch * headTrackingInfluence,
                        roll * headTrackingInfluence);

                    Vec3 posOffset = trackingClient.GetProcessedPosition(headRotQ, deltaTime);
                    Vec3 scaledPos = posOffset * headTrackingInfluence;
                    _lastPositionOffset = scaledPos;

                    var gamePos = cameraTransform.localPosition;
                    cameraTransform.localPosition = gamePos + new Vector3(
                        scaledPos.X, scaledPos.Y, scaledPos.Z);
                    _positionOffsetApplied = true;
                }
            }
            else
            {
                cameraTransform.localRotation = gameWantedRotation * _smoothedHeadTrackingRotation;
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
