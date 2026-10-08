using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

namespace VIP.MusicTech.Audio
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class MultitrackEngine : MonoBehaviour
    {
        [SerializeField] private MultitrackSession initialSession;
        [SerializeField] private bool playOnStart;
        [SerializeField, Tooltip("Repeat the entire session, preserving each track's start offset.")]
        private bool loop;
        [Header("Global Waveform Playhead")]
        [SerializeField] private bool showPlayhead = true;
        [SerializeField, Min(1), Tooltip("Playhead width in waveform texture pixels.")]
        private int playheadWidth = 2;
        [SerializeField] private Color playheadColor = Color.white;
        [Header("Mix and Scheduling")]
        [SerializeField, Range(0, 1)] private float masterVolume = 1;
        [SerializeField] private bool masterMuted;
        [SerializeField] private AudioMixerGroup defaultOutputGroup;
        [SerializeField, Min(0.02f)] private float scheduleLeadSeconds = 0.1f;
        [SerializeField, Range(1, 60)] private float updateRate = 20;
        [SerializeField, Min(1)] private float loadTimeoutSeconds = 30;

        public UnityEvent SessionLoaded = new UnityEvent();
        public UnityEvent PlaybackCompleted = new UnityEvent();
        public UnityEvent PlaybackLooped = new UnityEvent();
        public TransportEvent TransportChanged = new TransportEvent();
        public TransportEvent PositionChanged = new TransportEvent();
        public TrackEvent TrackChanged = new TrackEvent();
        public TrackLayoutEvent TrackLayoutChanged = new TrackLayoutEvent();
        public TrackEvent LevelsChanged = new TrackEvent();
        public AudioErrorEvent Error = new AudioErrorEvent();

        private sealed class Track
        {
            public string Name;
            public Color Color;
            public float Start, Volume, Pan, Peak, Rms;
            public bool Muted, Solo;
            public AudioSource Source;
            public AudioSource NextSource;
            public readonly float[] Samples = new float[512];
            public float[][] Output;
            public int OutputFrame = -1;
            public AudioSource OutputSource;
        }

        private readonly List<Track> tracks = new List<Track>();
        private Coroutine loadRoutine;
        private double position, anchorPosition, anchorDsp;
        private float nextUpdate;
        private bool scheduledLoop;
        public TransportState State { get; private set; } = TransportState.Empty;
        public MultitrackSession Session { get; private set; }
        public int TrackCount => tracks.Count;
        public double DurationSeconds { get; private set; }
        public double PositionSeconds => State == TransportState.Playing
            ? Math.Min(DurationSeconds, anchorPosition + Math.Max(0, AudioSettings.dspTime - anchorDsp)) : position;
        public float MasterVolume => masterVolume;
        public bool MasterMuted => masterMuted;
        public bool Loop => loop;
        public bool ShowPlayhead => showPlayhead;
        public int PlayheadWidth => Mathf.Max(1, playheadWidth);
        public Color PlayheadColor => playheadColor;

        private void Start()
        {
            if (initialSession != null && State == TransportState.Empty) LoadSession(initialSession);
        }

        public void LoadSession(MultitrackSession session)
        {
            if (!isActiveAndEnabled) { Report("Load", "", "Enable the engine before loading."); return; }
            if (session == null) { Report("Load", "", "A session asset is required."); return; }
            // Validate before replacing the currently loaded session.
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (session.Tracks == null || session.Tracks.Count == 0)
            { Report("Load", "", "The session contains no tracks."); return; }
            foreach (var definition in session.Tracks)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.TrackName) || !ids.Add(definition.TrackName) || definition.Clip == null ||
                    !Finite(definition.StartSeconds) || definition.StartSeconds < 0 || !Finite(definition.Volume) || !Finite(definition.Pan) ||
                    (definition.SourcePrefab != null && definition.SourcePrefab.GetComponent<AudioSource>() == null))
                { Report("Load", definition == null ? "" : definition.TrackName, "Tracks need unique nonblank names, clips, finite values, nonnegative start times, and a root AudioSource on any prefab."); return; }
            }
            UnloadSession();
            Session = session;
            State = TransportState.Loading;
            PublishTransport();
            loadRoutine = StartCoroutine(LoadTracks(session));
        }

        private IEnumerator LoadTracks(MultitrackSession session)
        {
            // Yield first so loadRoutine is assigned even for already-loaded clips.
            yield return null;
            foreach (var definition in session.Tracks)
            {
                var clip = definition.Clip;
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                float deadline = Time.realtimeSinceStartup + loadTimeoutSeconds;
                while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < deadline) yield return null;
                if (clip.loadState != AudioDataLoadState.Loaded)
                {
                    loadRoutine = null;
                    UnloadSession();
                    Report("Load", definition.TrackName, "Audio data failed to load or timed out.");
                    yield break;
                }
                var child = definition.SourcePrefab != null
                    ? Instantiate(definition.SourcePrefab, transform) : new GameObject();
                child.name = "Track - " + definition.TrackName;
                child.transform.SetParent(transform, false);
                var source = child.GetComponent<AudioSource>();
                if (source == null) source = child.AddComponent<AudioSource>();
                source.Stop();
                source.playOnAwake = false;
                source.clip = clip;
                source.loop = false;
                source.pitch = 1;
                source.spatialBlend = 0;
                source.dopplerLevel = 0;
                source.outputAudioMixerGroup = definition.OutputGroup != null ? definition.OutputGroup : defaultOutputGroup;
                // Two voices on the same track object share its attached filter components.
                // The idle voice prepares the next pass while the current pass finishes.
                var nextSource = child.AddComponent<AudioSource>();
                nextSource.playOnAwake = false;
                nextSource.clip = clip;
                nextSource.loop = false;
                nextSource.pitch = 1;
                nextSource.spatialBlend = 0;
                nextSource.dopplerLevel = 0;
                nextSource.priority = source.priority;
                nextSource.bypassEffects = source.bypassEffects;
                nextSource.bypassListenerEffects = source.bypassListenerEffects;
                nextSource.bypassReverbZones = source.bypassReverbZones;
                nextSource.reverbZoneMix = source.reverbZoneMix;
                nextSource.outputAudioMixerGroup = source.outputAudioMixerGroup;
                var output = new float[clip.channels][];
                for (int channel = 0; channel < output.Length; channel++) output[channel] = new float[8192];
                tracks.Add(new Track { Name = definition.TrackName, Color = definition.Color,
                    Start = definition.StartSeconds, Volume = Mathf.Clamp01(definition.Volume), Pan = Mathf.Clamp(definition.Pan, -1, 1),
                    Muted = definition.Muted, Solo = definition.Solo, Source = source, NextSource = nextSource, Output = output });
                DurationSeconds = Math.Max(DurationSeconds, definition.StartSeconds + (double)clip.samples / clip.frequency);
            }
            loadRoutine = null;
            State = TransportState.Stopped;
            ApplyMix();
            PublishTransport();
            PublishLayout();
            SessionLoaded.Invoke();
            if (playOnStart) Play();
        }

        public void UnloadSession()
        {
            if (loadRoutine != null) { StopCoroutine(loadRoutine); loadRoutine = null; }
            foreach (var track in tracks)
                if (track.Source != null) { track.Source.Stop(); track.NextSource.Stop(); Destroy(track.Source.gameObject); }
            tracks.Clear();
            Session = null;
            position = DurationSeconds = 0;
            State = TransportState.Empty;
            PublishTransport();
            PublishLayout();
        }

        public void Play()
        {
            if (State == TransportState.Playing) return;
            if (State == TransportState.Loading || State == TransportState.Empty || !isActiveAndEnabled)
            { Report("Play", "", "Wait for SessionLoaded and enable the engine before playing."); return; }
            if (position >= DurationSeconds) position = 0;
            Schedule();
            State = TransportState.Playing;
            PublishTransport();
        }

        private void Schedule()
        {
            anchorPosition = position;
            anchorDsp = AudioSettings.dspTime + Math.Max(0.02, scheduleLeadSeconds);
            foreach (var track in tracks)
            {
                var source = track.Source;
                source.Stop();
                track.NextSource.Stop();
                track.OutputFrame = -1;
                double local = position - track.Start;
                if (local >= (double)source.clip.samples / source.clip.frequency) continue;
                source.timeSamples = (int)Math.Min(source.clip.samples - 1, Math.Max(0, local) * source.clip.frequency);
                source.PlayScheduled(anchorDsp + Math.Max(0, -local));
            }
            PrepareLoop();
        }

        private void PrepareLoop()
        {
            scheduledLoop = loop;
            double boundary = anchorDsp + DurationSeconds - anchorPosition;
            foreach (var track in tracks)
            {
                track.NextSource.Stop();
                if (!loop || DurationSeconds <= 0) continue;
                // Mirror routing in case the caller changed it through GetTrackSource.
                track.NextSource.outputAudioMixerGroup = track.Source.outputAudioMixerGroup;
                track.NextSource.timeSamples = 0;
                track.NextSource.PlayScheduled(boundary + track.Start);
            }
        }

        private void AdvanceLoop()
        {
            double boundary = anchorDsp + DurationSeconds - anchorPosition;
            position = anchorPosition = 0;
            anchorDsp = boundary;
            foreach (var track in tracks)
            {
                var previous = track.Source;
                track.Source = track.NextSource;
                track.NextSource = previous;
            }
            // If a long main-thread stall missed an entire pass, recover in sync.
            if (AudioSettings.dspTime >= boundary + DurationSeconds)
            {
                position = (AudioSettings.dspTime - boundary) % DurationSeconds;
                Schedule();
            }
            else PrepareLoop();
        }

        public void Pause()
        {
            if (State != TransportState.Playing) return;
            position = PositionSeconds;
            StopSources();
            State = TransportState.Paused;
            PublishTransport();
        }

        public void Stop()
        {
            if (State == TransportState.Empty || State == TransportState.Loading) return;
            StopSources();
            position = 0;
            State = TransportState.Stopped;
            PublishTransport();
        }

        public void TogglePlayback() { if (State == TransportState.Playing) Pause(); else Play(); }
        public void SetLoop(bool enabled)
        {
            if (loop == enabled) return;
            // A queued pass may already have begun between Update and this call.
            if (State == TransportState.Playing && scheduledLoop && DurationSeconds > 0 && PositionSeconds >= DurationSeconds)
                AdvanceLoop();
            loop = enabled;
            if (State == TransportState.Playing) PrepareLoop();
            PublishTransport();
        }
        public void ToggleLoop() { SetLoop(!loop); }
        public void SetShowPlayhead(bool visible) { showPlayhead = visible; }
        public void SetPlayheadWidth(int width) { playheadWidth = Mathf.Max(1, width); }
        public void SetPlayheadColor(Color color) { playheadColor = color; }
        public void SeekSeconds(float seconds)
        {
            if (!Finite(seconds) || State == TransportState.Loading || State == TransportState.Empty) return;
            position = Math.Max(0, Math.Min(DurationSeconds, seconds));
            if (State == TransportState.Playing) Schedule();
            PublishTransport();
        }
        public void SeekNormalized(float value) { if (Finite(value)) SeekSeconds((float)(Mathf.Clamp01(value) * DurationSeconds)); }
        public void SetMasterVolume(float volume) { if (!Finite(volume)) return; masterVolume = Mathf.Clamp01(volume); ApplyMix(); PublishTransport(); }
        public void SetMasterMuted(bool muted) { masterMuted = muted; ApplyMix(); PublishTransport(); }

        public void SetTrackVolume(string trackName, float volume) { var t = Find(trackName); if (t == null || !Finite(volume)) return; t.Volume = Mathf.Clamp01(volume); MixChanged(t); }
        public void SetTrackPan(string trackName, float pan) { var t = Find(trackName); if (t == null || !Finite(pan)) return; t.Pan = Mathf.Clamp(pan, -1, 1); MixChanged(t); }
        public void SetTrackMuted(string trackName, bool muted) { var t = Find(trackName); if (t == null) return; t.Muted = muted; MixChanged(t); }
        public void SetTrackSolo(string trackName, bool solo) { var t = Find(trackName); if (t == null) return; t.Solo = solo; MixChanged(t); }
        public void SetTrackName(string trackName, string newTrackName)
        {
            var t = Find(trackName);
            if (t == null || newTrackName == t.Name) return;
            if (string.IsNullOrWhiteSpace(newTrackName) || tracks.Exists(track => track != t && track.Name == newTrackName))
            {
                Report("Rename", trackName, "Track names must be unique and nonblank.");
                return;
            }
            string previousName = t.Name;
            t.Name = newTrackName;
            t.Source.gameObject.name = "Track - " + t.Name;
            var payload = Snapshot(t);
            payload.PreviousTrackName = previousName;
            TrackChanged.Invoke(payload);
        }
        public AudioSource GetTrackSource(string trackName) => Find(trackName)?.Source;
        public TrackPayload GetTrack(string trackName) { var t = Find(trackName); return t == null ? null : Snapshot(t); }
        public TrackPayload GetTrackAt(int trackIndex) => trackIndex >= 0 && trackIndex < tracks.Count ? Snapshot(tracks[trackIndex]) : null;

        // Numeric IDs are 1-based layout slots, assigned automatically from list order.
        public TrackPayload GetTrack(int trackId) => GetTrackAt(trackId - 1);
        public AudioSource GetTrackSource(int trackId) => TrackAtId(trackId)?.Source;
        public void SetTrackVolume(int trackId, float volume) { var t = TrackAtId(trackId); if (t != null) SetTrackVolume(t.Name, volume); }
        public void SetTrackPan(int trackId, float pan) { var t = TrackAtId(trackId); if (t != null) SetTrackPan(t.Name, pan); }
        public void SetTrackMuted(int trackId, bool muted) { var t = TrackAtId(trackId); if (t != null) SetTrackMuted(t.Name, muted); }
        public void SetTrackSolo(int trackId, bool solo) { var t = TrackAtId(trackId); if (t != null) SetTrackSolo(t.Name, solo); }
        public void SetTrackName(int trackId, string newTrackName) { var t = TrackAtId(trackId); if (t != null) SetTrackName(t.Name, newTrackName); }
        public bool CopyTrackOutput(int trackId, float[] samples, int channel = 0)
        {
            var t = TrackAtId(trackId);
            return CopyOutput(t, samples, channel);
        }
        private Track TrackAtId(int trackId) => trackId >= 1 && trackId <= tracks.Count ? tracks[trackId - 1] : null;

        public void MoveTrackUp(int trackId) { MoveTrack(trackId, trackId - 1); }
        public void MoveTrackDown(int trackId) { MoveTrack(trackId, trackId + 1); }
        public void MoveTrack(int fromTrackId, int toTrackId)
        {
            if (fromTrackId == toTrackId || TrackAtId(fromTrackId) == null || TrackAtId(toTrackId) == null) return;
            var moved = tracks[fromTrackId - 1];
            tracks.RemoveAt(fromTrackId - 1);
            tracks.Insert(toTrackId - 1, moved);
            for (int i = 0; i < tracks.Count; i++) tracks[i].Source.transform.SetSiblingIndex(i);
            PublishLayout(fromTrackId, toTrackId);
        }

        private void PublishLayout(int fromTrackId = 0, int toTrackId = 0)
        {
            var snapshots = new TrackPayload[tracks.Count];
            for (int i = 0; i < tracks.Count; i++) snapshots[i] = Snapshot(tracks[i]);
            TrackLayoutChanged.Invoke(new TrackLayoutPayload { FromTrackId = fromTrackId, ToTrackId = toTrackId, Tracks = snapshots });
        }

        // Caller owns this power-of-two buffer; no per-frame array allocation.
        public bool CopyTrackOutput(string trackName, float[] samples, int channel = 0)
        {
            var t = Find(trackName);
            return CopyOutput(t, samples, channel);
        }

        private bool CopyOutput(Track t, float[] samples, int channel)
        {
            if (t == null || samples == null || samples.Length < 64 || samples.Length > 8192 ||
                (samples.Length & (samples.Length - 1)) != 0 || channel < 0 || channel >= t.Source.clip.channels) return false;
            if (State != TransportState.Playing || AudioSettings.dspTime < anchorDsp || !t.Source.isPlaying || t.Source.mute)
                Array.Clear(samples, 0, samples.Length);
            else
            {
                ReadOutput(t);
                // All consumers share one native history size, regardless of their buffer size.
                var output = t.Output[channel];
                Array.Copy(output, output.Length - samples.Length, samples, 0, samples.Length);
            }
            return true;
        }

        private void ReadOutput(Track track)
        {
            if (track.OutputFrame == Time.frameCount && track.OutputSource == track.Source) return;
            track.OutputFrame = Time.frameCount;
            track.OutputSource = track.Source;
            for (int channel = 0; channel < track.Output.Length; channel++)
                track.Source.GetOutputData(track.Output[channel], channel);
        }

        /// <summary>Per-sample signed extrema across channels, without stereo phase cancellation.</summary>
        public bool CopyTrackOutputEnvelope(int trackId, float[] minima, float[] maxima)
        {
            var track = TrackAtId(trackId);
            if (minima == null || maxima == null || minima == maxima || minima.Length != maxima.Length ||
                !CopyOutput(track, maxima, 0)) return false;
            Array.Copy(maxima, minima, maxima.Length);
            if (State != TransportState.Playing || AudioSettings.dspTime < anchorDsp || !track.Source.isPlaying || track.Source.mute) return true;
            int offset = track.Output[0].Length - maxima.Length;
            for (int channel = 1; channel < track.Output.Length; channel++)
                for (int i = 0; i < maxima.Length; i++)
                {
                    float value = track.Output[channel][offset + i];
                    minima[i] = Mathf.Min(minima[i], value);
                    maxima[i] = Mathf.Max(maxima[i], value);
                }
            return true;
        }

        private Track Find(string id)
        {
            foreach (var t in tracks) if (t.Name == id) return t;
            Report("Track lookup", id, "No loaded track has this name.");
            return null;
        }
        private void MixChanged(Track t)
        {
            ApplyMix();
            // Solo changes the effective audibility of every track.
            foreach (var track in tracks) TrackChanged.Invoke(Snapshot(track));
        }
        private void ApplyMix()
        {
            bool anySolo = tracks.Exists(t => t.Solo);
            foreach (var t in tracks)
            {
                t.Source.volume = t.Volume * masterVolume;
                t.Source.panStereo = t.Pan;
                t.Source.mute = masterMuted || t.Muted || (anySolo && !t.Solo);
                t.NextSource.volume = t.Source.volume;
                t.NextSource.panStereo = t.Source.panStereo;
                t.NextSource.mute = t.Source.mute;
                if (t.Source.mute || t.Source.volume <= 0) t.Peak = t.Rms = 0;
            }
        }
        private void StopSources() { foreach (var t in tracks) { t.Source.Stop(); t.NextSource.Stop(); t.Peak = t.Rms = 0; } }
        private void Update()
        {
            if (State == TransportState.Playing && PositionSeconds >= DurationSeconds)
            {
                if ((loop || scheduledLoop) && DurationSeconds > 0)
                {
                    AdvanceLoop();
                    PublishTransport();
                    PlaybackLooped.Invoke();
                }
                else
                {
                    position = DurationSeconds;
                    StopSources();
                    State = TransportState.Stopped;
                    PublishTransport();
                    PlaybackCompleted.Invoke();
                }
            }
            // Also honor changes made directly in the Inspector during playback.
            if (State == TransportState.Playing && scheduledLoop != loop) PrepareLoop();
            if (Time.unscaledTime < nextUpdate || State == TransportState.Loading || State == TransportState.Empty) return;
            nextUpdate = Time.unscaledTime + 1 / Mathf.Max(1, updateRate);
            foreach (var t in tracks)
            {
                float peak = 0;
                double sum = 0;
                for (int channel = 0; channel < t.Source.clip.channels; channel++)
                {
                    CopyTrackOutput(t.Name, t.Samples, channel);
                    foreach (float sample in t.Samples) { peak = Mathf.Max(peak, Mathf.Abs(sample)); sum += sample * sample; }
                }
                t.Peak = peak;
                t.Rms = (float)Math.Sqrt(sum / (t.Samples.Length * t.Source.clip.channels));
                LevelsChanged.Invoke(Snapshot(t));
            }
            PositionChanged.Invoke(GetTransport());
        }
        public TransportPayload GetTransport() => new TransportPayload { State = State, PositionSeconds = PositionSeconds,
            DurationSeconds = DurationSeconds, NormalizedPosition = DurationSeconds > 0 ? (float)(PositionSeconds / DurationSeconds) : 0,
            MasterVolume = masterVolume, MasterMuted = masterMuted, Loop = loop };
        private TrackPayload Snapshot(Track t) => new TrackPayload { TrackIndex = tracks.IndexOf(t), TrackId = tracks.IndexOf(t) + 1, TrackName = t.Name,
            Clip = t.Source.clip, Color = t.Color, StartSeconds = t.Start, DurationSeconds = t.Source.clip.length,
            Volume = t.Volume, Pan = t.Pan, Muted = t.Muted, Solo = t.Solo, Audible = !t.Source.mute && t.Source.volume > 0,
            Peak = t.Peak, Rms = t.Rms, PeakDecibels = Decibels(t.Peak), RmsDecibels = Decibels(t.Rms) };
        public static float Decibels(float amplitude) => 20 * Mathf.Log10(Mathf.Max(0.00001f, amplitude));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void PublishTransport() { TransportChanged.Invoke(GetTransport()); PositionChanged.Invoke(GetTransport()); }
        private void Report(string operation, string id, string message)
        {
            Debug.LogWarning(operation + ": " + message + " [" + id + "]", this);
            Error.Invoke(new AudioErrorPayload { Operation = operation, TrackName = id, Message = message });
        }
        private void OnDisable()
        {
            if (State == TransportState.Loading) UnloadSession();
            else Pause();
        }
        private void OnDestroy() { foreach (var t in tracks) if (t.Source != null) { t.Source.Stop(); t.NextSource.Stop(); } }
    }
}
