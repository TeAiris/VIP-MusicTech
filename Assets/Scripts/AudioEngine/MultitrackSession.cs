using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace VIP.MusicTech.Audio
{
    [Serializable]
    public sealed class AudioTrackDefinition
    {
        [Tooltip("Unique name used both for display and to look up this track in the API.")]
        public string TrackName = "Track";
        public AudioClip Clip;
        [Min(0)] public float StartSeconds;
        [Range(0, 1)] public float Volume = 1;
        [Range(-1, 1)] public float Pan;
        public bool Muted;
        public bool Solo;
        public Color Color = Color.cyan;
        [Tooltip("Optional prefab with an AudioSource on its root. Add Unity audio filters here.")]
        public GameObject SourcePrefab;
        public AudioMixerGroup OutputGroup;
    }

    [CreateAssetMenu(menuName = "Music Studio/Multitrack Session", fileName = "MultitrackSession")]
    public sealed class MultitrackSession : ScriptableObject
    {
        public string SessionName = "New Session";
        [Min(1)] public float BeatsPerMinute = 120;
        public List<AudioTrackDefinition> Tracks = new List<AudioTrackDefinition>();
    }
}
