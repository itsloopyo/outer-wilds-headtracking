extern alias UnityCoreModule;
extern alias OWMLCommon;
using System;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using OuterWildsHeadTracking.Configuration;

namespace OuterWildsHeadTracking.Tracking
{
    /// <summary>
    /// Wrapper around CameraUnlock.Core.Protocol.OpenTrackReceiver and TrackingProcessor.
    /// Provides the same API that the Outer Wilds mod expects while delegating
    /// to the shared library for UDP reception, processing, and lock-free data access.
    /// </summary>
    public class OpenTrackClient : IDisposable
    {
        private CameraUnlock.Core.Protocol.OpenTrackReceiver? _receiver;
        private TrackingProcessor? _processor;
        private PoseInterpolator? _poseInterpolator;
        private PositionProcessor? _positionProcessor;
        private PositionInterpolator? _positionInterpolator;
        private readonly int _port;
        private bool _loggedConnection;

        public OpenTrackClient() : this(CameraUnlock.Core.Protocol.OpenTrackReceiver.DefaultPort)
        {
        }

        public OpenTrackClient(int port)
        {
            _port = port;
        }

        public bool Initialize(Action<string>? log = null)
        {
            _receiver = new CameraUnlock.Core.Protocol.OpenTrackReceiver();
            if (log != null) _receiver.Log = log;

            _processor = new TrackingProcessor
            {
                Deadzone = DeadzoneSettings.None
            };

            _poseInterpolator = new PoseInterpolator();
            _positionProcessor = new PositionProcessor();
            _positionInterpolator = new PositionInterpolator();
            UpdateProcessorSettings();

            return _receiver.Start(_port);
        }

        /// <summary>
        /// Updates the processor settings from HeadTrackingMod config values.
        /// Call this when config values change.
        /// </summary>
        public void UpdateProcessorSettings()
        {
            if (_processor == null) return;

            _processor.LocalSmoothing = HeadTrackingMod.LocalSmoothing;
            _processor.RemoteSmoothing = HeadTrackingMod.RemoteSmoothing;

            // Configure sensitivity with inversion
            // Pitch needs negation (OpenTrack up = Unity down)
            _processor.Sensitivity = new SensitivitySettings(
                HeadTrackingMod.YawSensitivity,
                HeadTrackingMod.PitchSensitivity,
                HeadTrackingMod.RollSensitivity,
                invertYaw: false,
                invertPitch: true,  // Pitch needs inversion for Unity
                invertRoll: false
            );

            if (_positionProcessor != null)
            {
                _positionProcessor.Settings = PositionSettings.Symmetric(
                    HeadTrackingMod.PositionSensitivityX,
                    HeadTrackingMod.PositionSensitivityY,
                    HeadTrackingMod.PositionSensitivityZ,
                    HeadTrackingMod.PositionLimitX,
                    HeadTrackingMod.PositionLimitY,
                    HeadTrackingMod.PositionLimitZ,
                    HeadTrackingMod.PositionLimitZBack,
                    localSmoothing: HeadTrackingMod.LocalSmoothing,
                    remoteSmoothing: HeadTrackingMod.RemoteSmoothing,
                    invertX: true, invertY: false, invertZ: false
                );
            }
        }

        /// <summary>
        /// Returns true if the tracking data is coming from a remote host (not localhost).
        /// Selects which smoothing parameter applies: LocalSmoothing for loopback senders,
        /// RemoteSmoothing for a remote network device.
        /// </summary>
        public bool IsRemoteSource => _receiver?.IsRemoteConnection ?? false;

        /// <summary>
        /// Latches one line the first time packets arrive. Called from the mod's
        /// Update, not from the camera patch: whether anything reached the port
        /// must be answerable from the log while the player sits in a menu, is
        /// paused, or has tracking toggled off.
        /// </summary>
        public void LogConnectionOnce()
        {
            if (_loggedConnection || !(_receiver?.IsReceiving ?? false))
            {
                return;
            }

            _loggedConnection = true;
            string sourceType = IsRemoteSource ? "REMOTE" : "LOCAL";
            HeadTrackingMod.Instance?.ModHelper?.Console.WriteLine(
                $"[HeadTracking] Connection from {sourceType} source",
                OWMLCommon::OWML.Common.MessageType.Info);
        }

        /// <summary>
        /// Processes position data through the position pipeline.
        /// </summary>
        /// <param name="headRotQ">Head rotation quaternion.</param>
        /// <param name="deltaTime">Frame delta time.</param>
        /// <returns>Processed position offset in meters, or Vec3.Zero.</returns>
        public Vec3 GetProcessedPosition(Quat4 headRotQ, float deltaTime)
        {
            if (_receiver == null || _positionProcessor == null || _positionInterpolator == null
                || !_receiver.IsReceiving)
            {
                return Vec3.Zero;
            }

            // Re-read locality every frame so switching between a local tracker and a
            // remote device picks up the other smoothing parameter without a restart.
            _positionProcessor.IsRemoteConnection = _receiver.IsRemoteConnection;

            var rawPos = _receiver.GetLatestPosition();
            var interpolatedPos = _positionInterpolator.Update(rawPos, deltaTime);
            return _positionProcessor.Process(interpolatedPos, headRotQ, deltaTime);
        }

        public void Shutdown()
        {
            _receiver?.Dispose();
            _receiver = null;
            _processor = null;
            _poseInterpolator = null;
            _positionProcessor = null;
            _positionInterpolator = null;
            _loggedConnection = false;
        }

        public void Dispose()
        {
            Shutdown();
        }

        /// <summary>
        /// Gets processed rotation values using TrackingProcessor.
        /// Applies sensitivity (with pitch inversion).
        /// </summary>
        /// <param name="deltaTime">Frame delta time.</param>
        /// <returns>Processed rotation (Yaw, Pitch, Roll) in degrees, or null if no valid data.</returns>
        public ProcessedRotation? GetProcessedRotation(float deltaTime)
        {
            if (_receiver == null || _processor == null || _poseInterpolator == null || !_receiver.IsReceiving)
            {
                return null;
            }

            // Re-read locality every frame so switching between a local tracker and a
            // remote device picks up the other smoothing parameter without a restart.
            _processor.IsRemoteConnection = _receiver.IsRemoteConnection;

            var rawPose = _receiver.GetLatestPose();
            var interpolatedPose = _poseInterpolator.Update(rawPose, deltaTime);
            var processed = _processor.Process(interpolatedPose, deltaTime);

            return new ProcessedRotation
            {
                Yaw = processed.Yaw,
                Pitch = processed.Pitch,
                Roll = processed.Roll
            };
        }

        public struct ProcessedRotation
        {
            public float Yaw { get; set; }
            public float Pitch { get; set; }
            public float Roll { get; set; }
        }

        public struct RawEulerAngles
        {
            public float Yaw { get; set; }
            public float Pitch { get; set; }
            public float Roll { get; set; }
            public bool IsValid { get; set; }
        }

        public RawEulerAngles PeekRawEulerAngles()
        {
            if (_receiver == null || !_receiver.IsReceiving)
            {
                return new RawEulerAngles { IsValid = false };
            }

            _receiver.GetRawRotation(out float yaw, out float pitch, out float roll);
            return new RawEulerAngles
            {
                Yaw = yaw,
                Pitch = pitch,
                Roll = roll,
                IsValid = true
            };
        }
    }
}
