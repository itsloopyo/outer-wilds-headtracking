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

        public bool Initialize()
        {
            _receiver = new CameraUnlock.Core.Protocol.OpenTrackReceiver();
            if (!_receiver.Start(_port))
            {
                return false;
            }

            // Initialize processor with smoothing disabled (SimpleCameraPatch does quaternion Slerp)
            _processor = new TrackingProcessor
            {
                SmoothingFactor = 0f,
                Deadzone = DeadzoneSettings.None
            };

            _positionProcessor = new PositionProcessor();
            _positionInterpolator = new PositionInterpolator();
            UpdateProcessorSettings();

            return true;
        }

        /// <summary>
        /// Updates the processor settings from HeadTrackingMod config values.
        /// Call this when config values change.
        /// </summary>
        public void UpdateProcessorSettings()
        {
            if (_processor == null) return;

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
                _positionProcessor.Settings = new PositionSettings(
                    HeadTrackingMod.PositionSensitivityX,
                    HeadTrackingMod.PositionSensitivityY,
                    HeadTrackingMod.PositionSensitivityZ,
                    HeadTrackingMod.PositionLimitX,
                    HeadTrackingMod.PositionLimitY,
                    HeadTrackingMod.PositionLimitZ,
                    HeadTrackingMod.PositionSmoothing,
                    invertX: true, invertY: false, invertZ: true
                );
                _positionProcessor.NeckModelSettings = new NeckModelSettings(
                    HeadTrackingMod.NeckModelEnabled,
                    HeadTrackingMod.NeckModelHeight,
                    HeadTrackingMod.NeckModelForward
                );
            }
        }

        /// <summary>
        /// Returns true if the tracking data is coming from a remote host (not localhost).
        /// Used for adaptive smoothing - remote connections over WiFi may need smoothing.
        /// </summary>
        public bool IsRemoteSource
        {
            get
            {
                bool isRemote = _receiver?.IsRemoteConnection ?? false;

                // Log connection type once when we start receiving
                if (!_loggedConnection && (_receiver?.IsReceiving ?? false))
                {
                    _loggedConnection = true;
                    string sourceType = isRemote ? "REMOTE" : "LOCAL";
                    HeadTrackingMod.Instance?.ModHelper?.Console.WriteLine(
                        $"[HeadTracking] Connection from {sourceType} source",
                        OWMLCommon::OWML.Common.MessageType.Info);
                }

                return isRemote;
            }
        }

        /// <summary>
        /// Processes position data through the position pipeline.
        /// </summary>
        /// <param name="headRotQ">Head rotation quaternion (for neck model).</param>
        /// <param name="deltaTime">Frame delta time.</param>
        /// <returns>Processed position offset in meters, or Vec3.Zero.</returns>
        public Vec3 GetProcessedPosition(Quat4 headRotQ, float deltaTime)
        {
            if (_receiver == null || _positionProcessor == null || _positionInterpolator == null
                || !_receiver.IsReceiving)
            {
                return Vec3.Zero;
            }

            var rawPos = _receiver.GetLatestPosition();
            var interpolatedPos = _positionInterpolator.Update(rawPos, deltaTime);
            return _positionProcessor.Process(interpolatedPos, headRotQ, IsRemoteSource, deltaTime);
        }

        /// <summary>
        /// Sets the current position as the center offset.
        /// </summary>
        public void RecenterPosition()
        {
            if (_receiver == null || _positionProcessor == null) return;
            _positionProcessor.SetCenter(_receiver.GetLatestPosition());
            _positionInterpolator?.Reset();
        }

        /// <summary>
        /// Resets position processing state.
        /// </summary>
        public void ResetPositionProcessor()
        {
            _positionProcessor?.Reset();
            _positionInterpolator?.Reset();
        }

        public void Shutdown()
        {
            _receiver?.Dispose();
            _receiver = null;
            _processor = null;
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
        /// Applies center offset and sensitivity (with pitch inversion).
        /// </summary>
        /// <param name="deltaTime">Frame delta time.</param>
        /// <returns>Processed rotation (Yaw, Pitch, Roll) in degrees, or null if no valid data.</returns>
        public ProcessedRotation? GetProcessedRotation(float deltaTime)
        {
            if (_receiver == null || _processor == null || !_receiver.IsReceiving)
            {
                return null;
            }

            var rawPose = _receiver.GetLatestPose();
            var processed = _processor.Process(rawPose, IsRemoteSource, deltaTime);

            return new ProcessedRotation
            {
                Yaw = processed.Yaw,
                Pitch = processed.Pitch,
                Roll = processed.Roll
            };
        }

        /// <summary>
        /// Sets the specified raw pose as the new center point.
        /// Future processed rotations will be relative to this center.
        /// </summary>
        public void SetCenter(RawEulerAngles rawAngles)
        {
            if (_processor == null) return;
            var pose = new TrackingPose(rawAngles.Yaw, rawAngles.Pitch, rawAngles.Roll, 0);
            _processor.RecenterTo(pose);
            RecenterPosition();
        }

        /// <summary>
        /// Resets the processor state (clears center offset).
        /// </summary>
        public void ResetProcessor()
        {
            _processor?.Reset();
            ResetPositionProcessor();
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
