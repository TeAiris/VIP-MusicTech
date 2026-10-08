using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VIP.MusicTech.Audio.Tests
{
    public sealed class MultitrackEngineTests
    {
        private GameObject host;
        private MultitrackEngine engine;
        private MultitrackSession session;
        private AudioClip clip;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            host = new GameObject("Engine Test");
            engine = host.AddComponent<MultitrackEngine>();
            clip = AudioClip.Create("Test Tone", 44100, 1, 44100, false);
            session = ScriptableObject.CreateInstance<MultitrackSession>();
            session.Tracks.Add(new AudioTrackDefinition { TrackName = "drums", Clip = clip });
            session.Tracks.Add(new AudioTrackDefinition { TrackName = "bass", Clip = clip, StartSeconds = 0.5f });
            engine.LoadSession(session);
            for (int i = 0; i < 120 && engine.State == TransportState.Loading; i++) yield return null;
            Assert.AreEqual(TransportState.Stopped, engine.State);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(host);
            Object.Destroy(session);
            Object.Destroy(clip);
            yield return null;
        }

        [Test]
        public void LoadsMetadataAndIncludesTrackOffsetsInDuration()
        {
            Assert.AreEqual(2, engine.TrackCount);
            Assert.AreEqual(1.5, engine.DurationSeconds, 0.0001);
            Assert.AreEqual("bass", engine.GetTrackAt(1).TrackName);
            Assert.AreSame(clip, engine.GetTrack("drums").Clip);
        }

        [Test]
        public void SoloMuteAndMasterGainComposeWithoutChangingAsset()
        {
            engine.SetTrackSolo("drums", true);
            Assert.IsTrue(engine.GetTrackSource("bass").mute);
            engine.SetTrackVolume("drums", 0.5f);
            engine.SetMasterVolume(0.5f);
            Assert.AreEqual(0.25f, engine.GetTrackSource("drums").volume, 0.0001);
            engine.SetTrackMuted("drums", true);
            Assert.IsTrue(engine.GetTrackSource("drums").mute);
            engine.SetTrackSolo("drums", false);
            Assert.IsFalse(engine.GetTrackSource("bass").mute);
            engine.SetMasterMuted(true);
            Assert.IsTrue(engine.GetTrackSource("bass").mute);
            Assert.AreEqual(1, session.Tracks[0].Volume);
            Assert.IsFalse(session.Tracks[0].Muted);
        }

        [Test]
        public void SeekPauseStopPreserveTransportContract()
        {
            engine.SeekSeconds(0.75f);
            engine.Play();
            engine.Pause();
            Assert.AreEqual(TransportState.Paused, engine.State);
            Assert.AreEqual(0.75, engine.PositionSeconds, 0.02);
            engine.SeekNormalized(1);
            Assert.AreEqual(1.5, engine.PositionSeconds, 0.0001);
            engine.Stop();
            Assert.AreEqual(0, engine.PositionSeconds);
            Assert.AreEqual(TransportState.Stopped, engine.State);
        }

        [Test]
        public void DuplicateNamesRejectLoadAndPreserveCurrentSession()
        {
            session.Tracks[1].TrackName = "drums";
            LogAssert.Expect(LogType.Warning, "Load: Tracks need unique nonblank names, clips, finite values, nonnegative start times, and a root AudioSource on any prefab. [drums]");
            engine.LoadSession(session);
            Assert.AreEqual(2, engine.TrackCount);
            Assert.AreEqual(TransportState.Stopped, engine.State);
        }

        [Test]
        public void StoppedOutputIsSilentAndPayloadIsASnapshot()
        {
            var output = new float[512];
            output[0] = 1;
            Assert.IsTrue(engine.CopyTrackOutput("drums", output));
            Assert.AreEqual(0, output[0]);
            Assert.IsFalse(engine.CopyTrackOutput("drums", new float[100]));
            var snapshot = engine.GetTrack("drums");
            engine.SetTrackName("drums", "Percussion");
            Assert.AreEqual("drums", snapshot.TrackName);
            Assert.AreEqual("Percussion", engine.GetTrack("Percussion").TrackName);
            Assert.AreEqual("drums", session.Tracks[0].TrackName);
        }

        [UnityTest, Category("AudioDevice")]
        public IEnumerator LiveOutputRemainsNonzeroWithMixedReaderSizesAndRightOnlyStereo()
        {
            // Run with an active audio device. Exercises the native AudioSource history path.
            if (!System.Array.Exists(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None), listener => listener.isActiveAndEnabled))
                host.AddComponent<AudioListener>();
            var stereo = AudioClip.Create("Right Channel Tone", 44100, 2, 44100, false);
            var data = new float[88200];
            for (int i = 0; i < 44100; i++) data[i * 2 + 1] = 0.5f * Mathf.Sin(2 * Mathf.PI * 440 * i / 44100);
            stereo.SetData(data, 0);
            session.Tracks[0].Clip = stereo;
            engine.LoadSession(session);
            while (engine.State == TransportState.Loading) yield return null;
            engine.Play();
            var meter = new float[512];
            var minimum = new float[1024];
            var maximum = new float[1024];
            var large = new float[8192];
            float observed = 0;
            float deadline = Time.realtimeSinceStartup + 0.8f;
            try
            {
                while (Time.realtimeSinceStartup < deadline)
                {
                    engine.CopyTrackOutput(1, meter, 1);
                    Assert.IsTrue(engine.CopyTrackOutputEnvelope(1, minimum, maximum));
                    engine.CopyTrackOutput(1, large, 1);
                    foreach (float value in maximum) observed = Mathf.Max(observed, Mathf.Abs(value));
                    foreach (float value in minimum) observed = Mathf.Max(observed, Mathf.Abs(value));
                    yield return null;
                }
                Assert.Greater(observed, 0.1f, "Live readers must see the right-channel tone while meters use a different window size.");
            }
            finally
            {
                engine.UnloadSession();
                Object.Destroy(stereo);
            }
        }

        [Test]
        public void RenameUpdatesBindingsAndRejectsDuplicateOrBlankNames()
        {
            var channel = new GameObject("Channel");
            channel.transform.SetParent(host.transform);
            channel.SetActive(false);
            var binding = channel.AddComponent<AudioTrackBinding>();
            binding.Engine = engine;
            binding.TrackId = 1;
            channel.SetActive(true);

            engine.SetTrackName("drums", "Percussion");
            Assert.AreEqual("Percussion", binding.TrackName);
            binding.SetVolume(0.3f);
            Assert.AreEqual(0.3f, engine.GetTrack("Percussion").Volume, 0.0001);

            LogAssert.Expect(LogType.Warning, "Rename: Track names must be unique and nonblank. [Percussion]");
            engine.SetTrackName("Percussion", "bass");
            LogAssert.Expect(LogType.Warning, "Rename: Track names must be unique and nonblank. [Percussion]");
            engine.SetTrackName("Percussion", " ");
            Assert.AreEqual("Percussion", binding.GetTrack().TrackName);
        }

        [Test]
        public void MovingTracksReassignsNumericIdsAndRefreshesChannelBindings()
        {
            var channel = new GameObject("Channel 1");
            channel.transform.SetParent(host.transform);
            channel.SetActive(false);
            var binding = channel.AddComponent<AudioTrackBinding>();
            binding.Engine = engine;
            binding.TrackId = 1;
            channel.SetActive(true);
            string displayedName = null;
            binding.TrackChanged.AddListener(track => displayedName = track.TrackName);
            var drumsSource = engine.GetTrackSource(1);
            engine.Play();
            double position = engine.PositionSeconds;

            engine.MoveTrackDown(1);
            Assert.AreEqual("bass", displayedName);
            Assert.AreEqual("bass", engine.GetTrack(1).TrackName);
            Assert.AreEqual("drums", engine.GetTrack(2).TrackName);
            Assert.AreEqual(2, engine.GetTrack("drums").TrackId);
            Assert.AreSame(drumsSource, engine.GetTrackSource(2));
            Assert.AreEqual(TransportState.Playing, engine.State);
            Assert.GreaterOrEqual(engine.PositionSeconds, position);
            binding.SetVolume(0.2f);
            Assert.AreEqual(0.2f, engine.GetTrack("bass").Volume, 0.0001);
            Assert.AreEqual(1, engine.GetTrack("drums").Volume);
            Assert.AreEqual("drums", session.Tracks[0].TrackName);

            binding.MoveTrackUp(); // First slot has no previous neighbor.
            Assert.AreEqual("bass", binding.TrackName);
            engine.MoveTrackUp(2);
            Assert.AreEqual("drums", displayedName);
            Assert.AreEqual(1, binding.TrackId);
        }

        [Test]
        public void EmptyChannelBindingsClearAndInvalidMovesAreNoOps()
        {
            engine.MoveTrackUp(1);
            engine.MoveTrackDown(2);
            engine.MoveTrack(0, 1);
            Assert.AreEqual("drums", engine.GetTrack(1).TrackName);
            Assert.IsNull(engine.GetTrack(3));
            var channel = new GameObject("Empty Channel");
            channel.transform.SetParent(host.transform);
            var binding = channel.AddComponent<AudioTrackBinding>();
            binding.Engine = engine;
            bool cleared = false;
            binding.TrackCleared.AddListener(() => cleared = true);
            binding.SetTrackId(3);
            Assert.IsTrue(cleared);
            Assert.IsNull(binding.GetTrack());
        }

        [UnityTest]
        public IEnumerator LoopWrapsSessionAndCanBeDisabled()
        {
            int loops = 0, completions = 0;
            engine.PlaybackLooped.AddListener(() => loops++);
            engine.PlaybackCompleted.AddListener(() => completions++);
            engine.SetLoop(true);
            Assert.IsTrue(engine.GetTransport().Loop);
            engine.SeekSeconds(1.25f);
            engine.Play();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(1, loops);
            Assert.AreEqual(0, completions);
            Assert.AreEqual(TransportState.Playing, engine.State);
            Assert.Less(engine.PositionSeconds, 1.0);
            Assert.AreEqual(2, engine.GetTrackSource("drums").GetComponents<AudioSource>().Length);
            engine.SetLoop(false);
            engine.SeekSeconds(1.4f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(TransportState.Stopped, engine.State);
            Assert.AreEqual(1, completions);
            Assert.IsFalse(engine.GetTransport().Loop);
        }

        [Test]
        public void GlobalPlayheadSettingsAreAvailableThroughApi()
        {
            engine.SetPlayheadWidth(0);
            Assert.AreEqual(1, engine.PlayheadWidth);
            engine.SetPlayheadWidth(4);
            engine.SetPlayheadColor(Color.magenta);
            engine.SetShowPlayhead(false);
            Assert.AreEqual(4, engine.PlayheadWidth);
            Assert.AreEqual(Color.magenta, engine.PlayheadColor);
            Assert.IsFalse(engine.ShowPlayhead);
        }

        [UnityTest]
        public IEnumerator UnloadCancelsAndDestroysSources()
        {
            engine.Play();
            engine.UnloadSession();
            yield return null;
            Assert.AreEqual(TransportState.Empty, engine.State);
            Assert.AreEqual(0, engine.TrackCount);
            Assert.AreEqual(0, host.transform.childCount);
        }
    }
}
