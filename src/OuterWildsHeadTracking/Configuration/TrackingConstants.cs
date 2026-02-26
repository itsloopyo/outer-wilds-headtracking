namespace OuterWildsHeadTracking.Configuration
{
    /// <summary>
    /// Constants for head tracking configuration.
    /// Note: Default UDP port is defined in HeadCannon.Core.Protocol.OpenTrackReceiver.DefaultPort
    /// </summary>
    public static class TrackingConstants
    {
        public const int RECENTER_THRESHOLD_FRAMES = 60;

        // Dialogue mode detection
        public const float DIALOGUE_CAMERA_SPEED_THRESHOLD = 2.0f;
        public const float DIALOGUE_CAMERA_SPEED_RANGE = 8.0f;
        public const float DIALOGUE_MIN_HEAD_TRACKING = 0.15f;
    }
}
