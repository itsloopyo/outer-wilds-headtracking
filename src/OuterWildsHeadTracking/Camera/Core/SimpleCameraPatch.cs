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
    /// Core camera patch that applies head tracking to the player camera transform.
    /// Rotation and position are applied in Update_Postfix. The game resets localRotation
    /// each frame via its own UpdateRotation, so no save/restore is needed for rotation.
    /// Position is cleaned up in FixedUpdate_Prefix/Update_Prefix before game logic.
    ///
    /// Never touch PlayerHUD/HelmetOnUI/HUDCamera. It is a self-contained rig outside
    /// the player camera hierarchy that renders the helmet gauges into the "HelmetHUD"
    /// RenderTexture painted on the visor, so it must keep its fixed pose relative to
    /// its own canvas. Canvas markers project through the player camera
    /// (CanvasMarkerManager sets worldCamera to the active camera), not through it.
    /// </summary>
    [HarmonyPatch(typeof(PlayerCameraController))]
    public class SimpleCameraPatch
    {
        private static float _secondsWithoutData = 0f;
        private const float TRACKING_LOSS_FADE_DELAY_SECONDS = 0.5f;
        private const float TRACKING_LOSS_FADE_SPEED = 2.0f;

        public static Quaternion _lastHeadTrackingRotation = Quaternion.identity;

        // The camera's localRotation as the game set it, before head tracking. Kept in
        // local space because the player body the camera hangs off moves between the
        // capture and the patches that read it (physics steps, planet rotation), and a
        // world-space copy goes stale by exactly that motion.
        public static Quaternion _gameLocalRotation = Quaternion.identity;

        // The field above starts at identity, which is a real rotation and not a
        // sentinel, so patches that run before the first Update/FixedUpdate postfix
        // cannot tell "camera looking straight ahead" from "never captured".
        public static bool _baseRotationCaptured = false;
        public static UnityCoreModule::UnityEngine.Transform? _cameraTransform = null;
        private static UnityCoreModule::UnityEngine.Camera? _playerCamera = null;

        private static float _headTrackingInfluence = 1f;

        private static readonly AccessTools.FieldRef<PlayerCameraController, float> DegreesXRef =
            FastFieldRef.Create<PlayerCameraController, float>("_degreesX");
        private static readonly AccessTools.FieldRef<PlayerCameraController, float> DegreesYRef =
            FastFieldRef.Create<PlayerCameraController, float>("_degreesY");

        public static float _smoothedYaw = 0f;
        public static float _smoothedPitch = 0f;
        public static float _smoothedRoll = 0f;

        // Position tracking state
        public static Vec3 _lastPositionOffset = Vec3.Zero;
        private static bool _positionOffsetApplied = false;

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
            var cameraTransform = __instance.transform;

            if (_lastHeadTrackingRotation != Quaternion.identity)
            {
                // Re-apply head tracking rotation (game's UpdateRotation just reset it)
                _gameLocalRotation = GameLocalRotation(__instance);
                _baseRotationCaptured = true;
                cameraTransform.localRotation = _gameLocalRotation * _lastHeadTrackingRotation;
            }

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
            _cameraTransform = cameraTransform;

            _gameLocalRotation = GameLocalRotation(__instance);
            _baseRotationCaptured = true;

            // Update_Prefix has already taken last frame's lean off. Whatever this frame
            // does not re-apply must not be re-added by the next FixedUpdate_Postfix.
            _lastPositionOffset = Vec3.Zero;

            var mod = HeadTrackingMod.Instance;
            if (mod == null || !mod.IsTrackingEnabled())
            {
                _lastHeadTrackingRotation = Quaternion.identity;
                return;
            }

            if (OWTime.IsPaused(OWTime.PauseType.Menu))
            {
                _lastHeadTrackingRotation = Quaternion.identity;
                return;
            }

            var trackingClient = mod.GetTrackingClient()!;

            // Use unscaledDeltaTime: head tracking must respond in real time even when
            // the game is paused (e.g. PauseType.Reading while using the Nomai translator).
            float deltaTime = UnityCoreModule::UnityEngine.Time.unscaledDeltaTime;

            HandleTrackingLoss(trackingClient.IsReceiving, deltaTime);

            ComputeHeadTracking(trackingClient, mod, cameraTransform, deltaTime);
            cameraTransform.localRotation = _gameLocalRotation * _lastHeadTrackingRotation;
        }

        // UpdateLockOnTargeting steers _degreesY by the angle between the camera's
        // forward and the lock-on target. Left head-tracked, that angle includes the
        // head pitch, so the game pitches the aim to cancel the head out and the view
        // pins to the target. Run it against the clean rotation instead.
        private static readonly RotationPatchHelper _lockOnHelper = new RotationPatchHelper();

        [HarmonyPatch("UpdateLockOnTargeting")]
        [HarmonyPrefix]
        public static void UpdateLockOnTargeting_Prefix() => _lockOnHelper.BeginPatch();

        [HarmonyPatch("UpdateLockOnTargeting")]
        [HarmonyPostfix]
        public static void UpdateLockOnTargeting_Postfix() => _lockOnHelper.EndPatch();

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void Start_Postfix(PlayerCameraController __instance)
        {
            var mod = HeadTrackingMod.Instance;
            if (mod == null) return;

            // A new loop. A suppression whose exit event never fired in the last one
            // (the loop ended mid model-ship flight) must not carry into this one.
            mod.ClearSuppression();

            _playerCamera = __instance.GetComponent<OWCamera>().mainCamera;

            ReticleUpdater.Create();
            UnityCoreModule::UnityEngine.Camera.onPreRender -= OnCameraPreRender;
            UnityCoreModule::UnityEngine.Camera.onPreRender += OnCameraPreRender;
        }

        /// <summary>World rotation for a camera-local rotation, against the live parent.</summary>
        public static Quaternion ToWorld(Quaternion localRotation)
        {
            var parent = _cameraTransform!.parent;
            return parent != null ? parent.rotation * localRotation : localRotation;
        }

        private static Quaternion GameLocalRotation(PlayerCameraController controller)
        {
            return Quaternion.Euler(-DegreesYRef(controller), DegreesXRef(controller), 0f);
        }

        private static void OnCameraPreRender(UnityCoreModule::UnityEngine.Camera cam)
        {
            // Every camera in the scene raises this (HUD, map, probe, reflections), so
            // compare against the cached player camera rather than Camera.main, which on
            // Unity 2019.4 is a tag search on every call.
            if (cam != _playerCamera) return;
            if (_cameraTransform == null) return;
            if (_lastHeadTrackingRotation == Quaternion.identity)
            {
                // The game never repositions the reticle itself, so a stale override
                // from before a toggle would linger until the head moved past
                // Unity's quaternion equality epsilon (~0.16 degrees).
                var updater = ReticleUpdater.GetInstance();
                updater?.RestoreReticlePosition();
                updater?.RestoreCenterPromptPosition();
                return;
            }

            ReticleUpdater.GetInstance()?.UpdateReticlePosition(cam);
        }

        private static void HandleTrackingLoss(bool isReceiving, float deltaTime)
        {
            if (!isReceiving)
            {
                _secondsWithoutData += deltaTime;

                if (_secondsWithoutData > TRACKING_LOSS_FADE_DELAY_SECONDS)
                {
                    float t = 1f - UnityCoreModule::UnityEngine.Mathf.Exp(-TRACKING_LOSS_FADE_SPEED * deltaTime);
                    _smoothedYaw *= (1f - t);
                    _smoothedPitch *= (1f - t);
                    _smoothedRoll *= (1f - t);
                    _lastHeadTrackingRotation = CameraRotationComposer.GetTrackingOnlyRotation(
                        _smoothedYaw, _smoothedPitch, _smoothedRoll);
                }
            }
            else
            {
                _secondsWithoutData = 0f;
            }
        }

        private static void ComputeHeadTracking(OpenTrackClient trackingClient, HeadTrackingMod mod,
            UnityCoreModule::UnityEngine.Transform cameraTransform, float deltaTime)
        {
            var processed = trackingClient.GetProcessedRotation(deltaTime);

            if (processed.HasValue)
            {
                float yaw = processed.Value.Yaw;
                float pitch = processed.Value.Pitch;
                float roll = processed.Value.Roll;

                float headTrackingInfluence = CalculateHeadTrackingInfluence(deltaTime);
                yaw *= headTrackingInfluence;
                pitch *= headTrackingInfluence;
                roll *= headTrackingInfluence;

                // Already smoothed by the TrackingProcessor inside OpenTrackClient.
                // Smoothing again here would put two exponential filters in series:
                // the speed clamp in CalculateSmoothingFactor means a smoothing of 0
                // is still a 20 ms time constant, not a bypass, so the second pass
                // roughly doubled the lag instead of costing nothing.
                _smoothedYaw = yaw;
                _smoothedPitch = pitch;
                _smoothedRoll = roll;

                if (!mod.IsRotationActive())
                {
                    _smoothedYaw = 0f;
                    _smoothedPitch = 0f;
                    _smoothedRoll = 0f;
                }

                _lastHeadTrackingRotation = CameraRotationComposer.GetTrackingOnlyRotation(
                    _smoothedYaw, _smoothedPitch, _smoothedRoll);

                // Position tracking: apply to localPosition so markers see the offset.
                // Cleaned up in FixedUpdate_Prefix/Update_Prefix before game logic.
                if (mod.IsPositionActive())
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

                    // Negative z is the forward lean throughout the pipeline, and the
                    // asymmetric clamp is built on that. Unity's transform +z is forward, so
                    // the flip belongs here, at the boundary. Everything downstream reads
                    // _lastPositionOffset, including the prefixes that subtract it back off,
                    // so this is the single place the two conventions meet.
                    _lastPositionOffset = new Vec3(scaledPos.X, scaledPos.Y, -scaledPos.Z);

                    cameraTransform.localPosition += new Vector3(
                        _lastPositionOffset.X, _lastPositionOffset.Y, _lastPositionOffset.Z);
                    _positionOffsetApplied = true;
                }
            }
            else
            {
                _lastHeadTrackingRotation = CameraRotationComposer.GetTrackingOnlyRotation(
                    _smoothedYaw, _smoothedPitch, _smoothedRoll);
            }
        }

        private static float CalculateHeadTrackingInfluence(float deltaTime)
        {
            float target = PlayerState.InConversation()
                ? TrackingConstants.DIALOGUE_MIN_HEAD_TRACKING
                : 1f;
            float t = 1f - UnityCoreModule::UnityEngine.Mathf.Exp(
                -TrackingConstants.DIALOGUE_FADE_SPEED * deltaTime);
            _headTrackingInfluence = UnityCoreModule::UnityEngine.Mathf.Lerp(
                _headTrackingInfluence, target, t);
            return _headTrackingInfluence;
        }
    }
}
