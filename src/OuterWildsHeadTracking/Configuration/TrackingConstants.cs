namespace OuterWildsHeadTracking.Configuration
{
    /// <summary>
    /// Constants for head tracking configuration.
    /// Note: Default UDP port is defined in CameraUnlock.Core.Protocol.OpenTrackReceiver.DefaultPort
    /// </summary>
    public static class TrackingConstants
    {
        public const int RECENTER_THRESHOLD_FRAMES = 60;

        // Head tracking fades toward this floor while PlayerState.InConversation()
        public const float DIALOGUE_MIN_HEAD_TRACKING = 0.15f;
        public const float DIALOGUE_FADE_SPEED = 3.0f;
    }
}
