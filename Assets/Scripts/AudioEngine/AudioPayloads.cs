using System;
using UnityEngine;
using UnityEngine.Events;

namespace VIP.MusicTech.Audio
{
    public enum TransportState { Empty, Loading, Stopped, Playing, Paused }

    [Serializable]
    public sealed class TransportPayload
    {
        public TransportState State;
        public double PositionSeconds;
        public double DurationSeconds;
        public float NormalizedPosition;
        public float MasterVolume;
        public bool MasterMuted;
        public bool Loop;
    }

    [Serializable]
    public sealed class TrackPayload
    {
        public int TrackIndex;
        [Tooltip("Automatic 1-based channel number in the current layout.")]
        public int TrackId;
        public string TrackName;
        [Tooltip("Set only on rename events, so bindings can follow the new name.")]
        public string PreviousTrackName;
        public AudioClip Clip;
        public Color Color;
        public float StartSeconds;
        public float DurationSeconds;
        public float Volume;
        public float Pan;
        public bool Muted;
        public bool Solo;
        public bool Audible;
        public float Peak;
        public float Rms;
        public float PeakDecibels;
        public float RmsDecibels;
    }

    [Serializable]
    public sealed class AudioErrorPayload
    {
        public string Operation;
        public string TrackName;
        public string Message;
    }

    [Serializable] public sealed class TransportEvent : UnityEvent<TransportPayload> { }
    [Serializable]
    public sealed class TrackLayoutPayload
    {
        public int FromTrackId;
        public int ToTrackId;
        public TrackPayload[] Tracks;
    }
    [Serializable] public sealed class TrackLayoutEvent : UnityEvent<TrackLayoutPayload> { }
    [Serializable] public sealed class TrackEvent : UnityEvent<TrackPayload> { }
    [Serializable] public sealed class AudioErrorEvent : UnityEvent<AudioErrorPayload> { }
}
