using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using OuterWildsHeadTracking.Tracking;
using Xunit;

namespace OuterWildsHeadTracking.Tests
{
    /// <summary>
    /// Drives OpenTrackClient over loopback UDP, which is the only way to reach the
    /// rotation pipeline from outside the game.
    /// </summary>
    public class SmoothingPipelineTests : IDisposable
    {
        private const float FrameDelta = 1f / 60f;
        private const float StepYawDegrees = 20f;

        private readonly UdpClient _sender = new UdpClient();
        private readonly int _port;

        public SmoothingPipelineTests()
        {
            var probe = new UdpClient(0);
            _port = ((IPEndPoint)probe.Client.LocalEndPoint).Port;
            probe.Close();
        }

        public void Dispose() => _sender.Close();

        private static byte[] Pose(double yaw)
        {
            var packet = new byte[48];
            Buffer.BlockCopy(BitConverter.GetBytes(yaw), 0, packet, 24, 8);
            return packet;
        }

        private void Send(double yaw)
        {
            var packet = Pose(yaw);
            _sender.Send(packet, packet.Length, new IPEndPoint(IPAddress.Loopback, _port));
        }

        /// <summary>
        /// Yaw after a fixed number of frames of a step from 0 to StepYawDegrees.
        /// </summary>
        private float StepResponse(float localSmoothing, int frames)
        {
            HeadTrackingMod.LocalSmoothing = localSmoothing;
            HeadTrackingMod.RemoteSmoothing = localSmoothing;

            using (var client = new OpenTrackClient(_port))
            {
                Assert.True(client.Initialize());

                // Settle the receiver and the interpolator on a zero pose first, so
                // what the step measures is the filter and not the startup transient.
                var deadline = Stopwatch.StartNew();
                while (!client.PeekRawEulerAngles().IsValid)
                {
                    Send(0d);
                    Assert.True(deadline.ElapsedMilliseconds < 5000,
                        "no packet reached the receiver within 5s");
                    System.Threading.Thread.Sleep(2);
                }
                for (int i = 0; i < 60; i++)
                {
                    Send(0d);
                    System.Threading.Thread.Sleep(2);
                    client.GetProcessedRotation(FrameDelta);
                }

                float yaw = 0f;
                for (int i = 0; i < frames; i++)
                {
                    Send(StepYawDegrees);
                    System.Threading.Thread.Sleep(2);
                    var processed = client.GetProcessedRotation(FrameDelta);
                    Assert.True(processed.HasValue);
                    yaw = processed!.Value.Yaw;
                }
                return yaw;
            }
        }

        /// <summary>
        /// The processor used to be built with LocalSmoothing and RemoteSmoothing
        /// hardcoded to 0 while the camera patch ran a second filter of its own, so
        /// the configured value never reached the rotation pipeline and every setting
        /// produced the same response.
        /// </summary>
        [Fact]
        public void ConfiguredSmoothingChangesTheRotationStepResponse()
        {
            const int frames = 10;
            float fast = StepResponse(0f, frames);
            float slow = StepResponse(0.9f, frames);

            Assert.True(fast > 0.95f * StepYawDegrees,
                $"smoothing 0 should have all but converged after {frames} frames, got {fast}");
            Assert.True(slow < 0.75f * StepYawDegrees,
                $"smoothing 0.9 should still be far from the target after {frames} frames, got {slow}");
        }
    }
}
