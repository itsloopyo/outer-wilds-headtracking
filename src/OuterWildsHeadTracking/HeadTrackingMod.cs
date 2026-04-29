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

        public static float YawSensitivity = 1.0f;
        public static float PitchSensitivity = 1.0f;
        public static float RollSensitivity = 1.0f;
        public static float Smoothing = 0.0f;
        public static bool AdaptiveSmoothing = true;

        // Position settings
        public static bool PositionEnabled = true;
        public static float PositionSensitivityX = 4.0f;
        public static float PositionSensitivityY = 4.0f;
        public static float PositionSensitivityZ = 4.0f;
        public static float PositionLimitX = 0.30f;
        public static float PositionLimitY = 0.20f;
        public static float PositionLimitZ = 0.40f;
        public static float PositionLimitZBack = 0.0f;
        public static float PositionSmoothing = 0.15f;

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
                    ModHelper.Console.WriteLine($"[HeadTracking] Initialized (Home=recenter, End=toggle, PgUp=position, smoothing={Smoothing})", MessageType.Info);
                }
                else
                {
                    ModHelper.Console.WriteLine("[HeadTracking] Failed to initialize", MessageType.Warning);
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
            // Unity's InputSystem can throw InvalidOperationException during scene transitions
            // when the keyboard device is being reconfigured. This is expected behavior and
            // hotkey checking should gracefully skip when the input system is in flux.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            try
            {
                bool chord = (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
                          && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

                // Recenter: Home or Ctrl+Shift+T
                if (keyboard.homeKey.wasPressedThisFrame
                    || (chord && keyboard.tKey.wasPressedThisFrame))
                {
                    global::OuterWildsHeadTracking.Camera.Core.SimpleCameraPatch.RecenterTracking();
                }

                // Toggle tracking: End or Ctrl+Shift+Y
                if (keyboard.endKey.wasPressedThisFrame
                    || (chord && keyboard.yKey.wasPressedThisFrame))
                {
                    _trackingEnabled = !_trackingEnabled;
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
            catch (InvalidOperationException)
            {
                // Input system in flux during scene transition - skip this frame
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
            Smoothing = (float)ModHelper.Config.GetSettingsValue<double>("smoothing");
            AdaptiveSmoothing = ModHelper.Config.GetSettingsValue<bool>("adaptiveSmoothing");

            if (YawSensitivity <= 0) YawSensitivity = 1.0f;
            if (PitchSensitivity <= 0) PitchSensitivity = 1.0f;
            if (RollSensitivity <= 0) RollSensitivity = 1.0f;
            Smoothing = UnityCoreModule::UnityEngine.Mathf.Clamp01(Smoothing);

            // Position settings
            PositionEnabled = ModHelper.Config.GetSettingsValue<bool>("positionEnabled");
            PositionSensitivityX = (float)ModHelper.Config.GetSettingsValue<double>("positionSensitivityX");
            PositionSensitivityY = (float)ModHelper.Config.GetSettingsValue<double>("positionSensitivityY");
            PositionSensitivityZ = (float)ModHelper.Config.GetSettingsValue<double>("positionSensitivityZ");
            PositionLimitX = (float)ModHelper.Config.GetSettingsValue<double>("positionLimitX");
            PositionLimitY = (float)ModHelper.Config.GetSettingsValue<double>("positionLimitY");
            PositionLimitZ = (float)ModHelper.Config.GetSettingsValue<double>("positionLimitZ");
            PositionLimitZBack = (float)ModHelper.Config.GetSettingsValue<double>("positionLimitZBack");
            PositionSmoothing = (float)ModHelper.Config.GetSettingsValue<double>("positionSmoothing");
            if (PositionSensitivityX <= 0) PositionSensitivityX = 2.0f;
            if (PositionSensitivityY <= 0) PositionSensitivityY = 2.0f;
            if (PositionSensitivityZ <= 0) PositionSensitivityZ = 2.0f;
            PositionSmoothing = UnityCoreModule::UnityEngine.Mathf.Clamp01(PositionSmoothing);

            // Update processor settings when config changes
            _trackingClient?.UpdateProcessorSettings();
        }
    }
}
