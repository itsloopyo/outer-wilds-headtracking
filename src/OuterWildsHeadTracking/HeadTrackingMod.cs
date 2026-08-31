extern alias OWMLCommon;
extern alias OWMLCore;
extern alias UnityCoreModule;
using HarmonyLib;
using CameraUnlock.Core.Protocol;
using OuterWildsHeadTracking.Configuration;
using OuterWildsHeadTracking.Tracking;
using OuterWildsHeadTracking.Utilities;
using System;
using System.Reflection;
using UnityEngine;
using IModHelper = OWMLCommon::OWML.Common.IModHelper;
using IModConfig = OWMLCommon::OWML.Common.IModConfig;
using MessageType = OWMLCommon::OWML.Common.MessageType;
using ModBehaviour = OWMLCore::OWML.ModHelper.ModBehaviour;
using KeyCode = UnityCoreModule::UnityEngine.KeyCode;

namespace OuterWildsHeadTracking
{
    public enum TrackingMode
    {
        Both = 0,
        RotationOnly = 1,
        PositionOnly = 2
    }

    public class HeadTrackingMod : ModBehaviour
    {
        public static HeadTrackingMod? Instance { get; private set; }
        private Harmony? _harmony;
        private OpenTrackClient? _trackingClient;
        private bool _trackingEnabled = true;
        private bool _trackingStateBeforeModelShip = true;
        private bool _trackingStateBeforeSignalscopeZoom = true;
        private TrackingMode _trackingMode = TrackingMode.Both;
        private bool _inputExceptionLogged;

        public static float YawSensitivity = 1.0f;
        public static float PitchSensitivity = 1.0f;
        public static float RollSensitivity = 1.0f;
        // Smoothing is picked per connection from the packet source address:
        // loopback senders get LocalSmoothing, remote network devices get RemoteSmoothing.
        // Both cover rotation and position.
        public static float LocalSmoothing = CameraUnlock.Core.Math.SmoothingUtils.DefaultLocalSmoothing;
        public static float RemoteSmoothing = CameraUnlock.Core.Math.SmoothingUtils.DefaultRemoteSmoothing;

        // Position settings
        public static bool PositionEnabled = true;
        public static float PositionSensitivityX = 4.0f;
        public static float PositionSensitivityY = 4.0f;
        public static float PositionSensitivityZ = 4.0f;
        public static float PositionLimitX = CameraUnlock.Core.Data.PositionSettings.Default.LimitX;
        public static float PositionLimitY = CameraUnlock.Core.Data.PositionSettings.Default.LimitY;
        public static float PositionLimitZ = CameraUnlock.Core.Data.PositionSettings.Default.LimitZ;
        public static float PositionLimitZBack = 0.0f;

        public new IModHelper? ModHelper { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            // ModHelper is set by OWML before Start() is called
            ModHelper = base.ModHelper;

            if (ModHelper == null)
            {
                return;
            }

            try
            {
                _harmony = new Harmony("itsloopyo.OuterWildsHeadTracking");
                _harmony.PatchAll(Assembly.GetExecutingAssembly());

                // Apply manual patches for types that aren't directly accessible
                global::OuterWildsHeadTracking.Camera.UI.MapMarkerPatch.ApplyPatches(_harmony);
            }
            catch (Exception ex)
            {
                ModHelper.Console.WriteLine($"[HeadTracking] Failed to apply patches: {ex.Message}", MessageType.Error);
            }

            try
            {
                int port = (int)ModHelper.Config.GetSettingsValue<long>("opentrackPort");
                if (port <= 0) port = OpenTrackReceiver.DefaultPort;

                ReadConfigValues();

                _trackingClient = new OpenTrackClient(port);

                var helper = ModHelper;
                if (_trackingClient.Initialize(msg => helper.Console.WriteLine($"[HeadTracking] {msg}", MessageType.Info)))
                {
                    ModHelper.Console.WriteLine($"[HeadTracking] Initialized, listening on UDP port {port} (End=toggle, PgUp=position, smoothing local={LocalSmoothing} remote={RemoteSmoothing})", MessageType.Info);
                }
                else
                {
                    ModHelper.Console.WriteLine($"[HeadTracking] Failed to bind UDP port {port}", MessageType.Warning);
                }

                // Listen for model ship events to disable head tracking during model ship control
                GlobalMessenger<OWRigidbody>.AddListener("EnterRemoteFlightConsole", OnEnterModelShip);
                GlobalMessenger.AddListener("ExitRemoteFlightConsole", OnExitModelShip);

                // Listen for signalscope zoom events to disable head tracking when zoomed
                GenericMessengerHelper.AddListener("EnterSignalscopeZoom", "Signalscope", "OnEnterSignalscopeZoom", this);
                GlobalMessenger.AddListener("ExitSignalscopeZoom", OnExitSignalscopeZoom);
            }
            catch (Exception ex)
            {
                ModHelper.Console.WriteLine($"[HeadTracking] Startup error: {ex.Message}", MessageType.Error);
            }
        }

        private void Update()
        {
            // Ahead of the input handling below, and outside the camera patch's
            // gameplay gates: packet arrival has to be visible in the log while
            // the player is in a menu, paused, or has tracking toggled off.
            _trackingClient?.LogConnectionOnce();

            // Unity's InputSystem can throw InvalidOperationException during scene transitions
            // when the keyboard device is being reconfigured. This is expected behavior and
            // hotkey checking should gracefully skip when the input system is in flux.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            try
            {
                bool chord = (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
                          && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

                // Toggle tracking: End or Ctrl+Shift+Y
                if (keyboard.endKey.wasPressedThisFrame
                    || (chord && keyboard.yKey.wasPressedThisFrame))
                {
                    _trackingEnabled = !_trackingEnabled;
                    ModHelper?.Console.WriteLine(
                        $"[HeadTracking] Tracking {(_trackingEnabled ? "enabled" : "disabled")}",
                        MessageType.Info);
                }

                // Cycle tracking mode: Page Up or Ctrl+Shift+G
                // Both → RotationOnly (position disabled) → PositionOnly (rotation disabled) → Both
                if (keyboard.pageUpKey.wasPressedThisFrame
                    || (chord && keyboard.gKey.wasPressedThisFrame))
                {
                    _trackingMode = (TrackingMode)(((int)_trackingMode + 1) % 3);
                    string desc = _trackingMode == TrackingMode.Both
                        ? "rotation + position"
                        : _trackingMode == TrackingMode.RotationOnly
                            ? "rotation only (position disabled)"
                            : "position only (rotation disabled)";
                    ModHelper?.Console.WriteLine(
                        $"[HeadTracking] Tracking mode: {desc}",
                        MessageType.Info);
                }
            }
            catch (InvalidOperationException ex)
            {
                // The keyboard device is reconfigured across a scene transition and
                // reading it mid-swap throws. Skipping the frame is correct, but the
                // exception is reported once so a real input fault is not mistaken
                // for a transition.
                if (!_inputExceptionLogged)
                {
                    _inputExceptionLogged = true;
                    ModHelper?.Console.WriteLine(
                        $"[HeadTracking] Hotkey polling skipped a frame: {ex.Message}",
                        MessageType.Warning);
                }
            }
        }

        private void OnEnterModelShip(OWRigidbody modelShipBody)
        {
            // Save current tracking state and disable tracking while piloting model ship
            // This prevents the camera from getting locked during model ship flight
            _trackingStateBeforeModelShip = _trackingEnabled;
            _trackingEnabled = false;
        }

        private void OnExitModelShip()
        {
            // Restore previous tracking state when exiting model ship
            _trackingEnabled = _trackingStateBeforeModelShip;
        }

        private void OnEnterSignalscopeZoom(object signalscope)
        {
            // Save current tracking state and disable tracking while zoomed in
            // Zoomed signalscope makes head tracking too sensitive for precise aiming
            _trackingStateBeforeSignalscopeZoom = _trackingEnabled;
            _trackingEnabled = false;
        }

        private void OnExitSignalscopeZoom()
        {
            // Restore previous tracking state when exiting zoom
            _trackingEnabled = _trackingStateBeforeSignalscopeZoom;
        }

        private void OnDestroy()
        {
            try
            {
                // Remove event listeners
                GlobalMessenger<OWRigidbody>.RemoveListener("EnterRemoteFlightConsole", OnEnterModelShip);
                GlobalMessenger.RemoveListener("ExitRemoteFlightConsole", OnExitModelShip);

                // Remove signalscope zoom listener
                GenericMessengerHelper.RemoveListener("EnterSignalscopeZoom", "Signalscope", "OnEnterSignalscopeZoom", this);
                GlobalMessenger.RemoveListener("ExitSignalscopeZoom", OnExitSignalscopeZoom);

                _trackingClient?.Shutdown();
                if (_harmony != null)
                {
                    _harmony.UnpatchAll(_harmony.Id);
                }
            }
            catch (Exception ex)
            {
                ModHelper?.Console.WriteLine($"[HeadTracking] Cleanup error: {ex.Message}", MessageType.Error);
            }
        }

        public bool IsTrackingEnabled()
        {
            // Don't check IsConnected() here - let the tracking client handle reconnection
            // by continuing to read from the socket even after a timeout
            return _trackingEnabled && _trackingClient != null;
        }

        public bool IsRotationActive()
        {
            return IsTrackingEnabled() && _trackingMode != TrackingMode.PositionOnly;
        }

        public bool IsPositionActive()
        {
            return IsTrackingEnabled() && _trackingMode != TrackingMode.RotationOnly && PositionEnabled;
        }

        public OpenTrackClient? GetTrackingClient()
        {
            return _trackingClient;
        }

        /// <summary>
        /// Called by OWML when config changes in the mod menu
        /// </summary>
        public override void Configure(IModConfig config)
        {
            ReadConfigValues();
        }

        private void ReadConfigValues()
        {
            if (ModHelper == null) return;

            YawSensitivity = (float)ModHelper.Config.GetSettingsValue<double>("yawSensitivity");
            PitchSensitivity = (float)ModHelper.Config.GetSettingsValue<double>("pitchSensitivity");
            RollSensitivity = (float)ModHelper.Config.GetSettingsValue<double>("rollSensitivity");
            LocalSmoothing = (float)ModHelper.Config.GetSettingsValue<double>("localSmoothing");
            RemoteSmoothing = (float)ModHelper.Config.GetSettingsValue<double>("remoteSmoothing");

            if (YawSensitivity <= 0) YawSensitivity = 1.0f;
            if (PitchSensitivity <= 0) PitchSensitivity = 1.0f;
            if (RollSensitivity <= 0) RollSensitivity = 1.0f;
            LocalSmoothing = UnityCoreModule::UnityEngine.Mathf.Clamp01(LocalSmoothing);
            RemoteSmoothing = UnityCoreModule::UnityEngine.Mathf.Clamp01(RemoteSmoothing);

            // Position settings
            PositionEnabled = ModHelper.Config.GetSettingsValue<bool>("positionEnabled");
            PositionSensitivityX = (float)ModHelper.Config.GetSettingsValue<double>("positionSensitivityX");
            PositionSensitivityY = (float)ModHelper.Config.GetSettingsValue<double>("positionSensitivityY");
            PositionSensitivityZ = (float)ModHelper.Config.GetSettingsValue<double>("positionSensitivityZ");
            PositionLimitX = (float)ModHelper.Config.GetSettingsValue<double>("positionLimitX");
            PositionLimitY = (float)ModHelper.Config.GetSettingsValue<double>("positionLimitY");
            PositionLimitZ = (float)ModHelper.Config.GetSettingsValue<double>("positionLimitZ");
            PositionLimitZBack = (float)ModHelper.Config.GetSettingsValue<double>("positionLimitZBack");
            if (PositionSensitivityX <= 0) PositionSensitivityX = 4.0f;
            if (PositionSensitivityY <= 0) PositionSensitivityY = 4.0f;
            if (PositionSensitivityZ <= 0) PositionSensitivityZ = 4.0f;

            // Update processor settings when config changes
            _trackingClient?.UpdateProcessorSettings();
        }
    }
}
