using UnityEngine;

namespace VIP.MusicTech.Audio
{
    /// <summary>Single-argument methods exposed to Inspector UnityEvents and visual scripting.</summary>
    public sealed class AudioTrackBinding : MonoBehaviour
    {
        public MultitrackEngine Engine;
        [Min(1), Tooltip("Automatic channel number: 1 is the first track in the layout.")]
        public int TrackId = 1;
        public string TrackName => GetTrack()?.TrackName;
        public UnityEngine.Events.UnityEvent TrackCleared = new UnityEngine.Events.UnityEvent();
        public TrackEvent TrackChanged = new TrackEvent();
        public TrackEvent LevelsChanged = new TrackEvent();
        public void SetVolume(float volume) { if (Engine != null) Engine.SetTrackVolume(TrackId, volume); }
        public void SetPan(float pan) { if (Engine != null) Engine.SetTrackPan(TrackId, pan); }
        public void SetMuted(bool muted) { if (Engine != null) Engine.SetTrackMuted(TrackId, muted); }
        public void SetSolo(bool solo) { if (Engine != null) Engine.SetTrackSolo(TrackId, solo); }
        public void SetTrackName(string trackName) { if (Engine != null) Engine.SetTrackName(TrackId, trackName); }
        public void SetTrackId(int trackId) { TrackId = Mathf.Max(1, trackId); Refresh(); }
        public void MoveTrackUp() { if (Engine != null) Engine.MoveTrackUp(TrackId); }
        public void MoveTrackDown() { if (Engine != null) Engine.MoveTrackDown(TrackId); }
        public void ToggleMuted() { var track = GetTrack(); if (track != null) SetMuted(!track.Muted); }
        public void ToggleSolo() { var track = GetTrack(); if (track != null) SetSolo(!track.Solo); }
        public TrackPayload GetTrack() => Engine != null ? Engine.GetTrack(TrackId) : null;
        public AudioSource GetSource() => Engine != null ? Engine.GetTrackSource(TrackId) : null;

        private MultitrackEngine subscribedEngine;
        private int lastTrackId;
        private void OnEnable()
        {
            subscribedEngine = Engine;
            if (subscribedEngine == null) return;
            subscribedEngine.TrackChanged.AddListener(OnTrackChanged);
            subscribedEngine.LevelsChanged.AddListener(OnLevelsChanged);
            subscribedEngine.TrackLayoutChanged.AddListener(OnLayoutChanged);
            Refresh();
        }
        private void OnDisable()
        {
            if (subscribedEngine == null) return;
            subscribedEngine.TrackChanged.RemoveListener(OnTrackChanged);
            subscribedEngine.LevelsChanged.RemoveListener(OnLevelsChanged);
            subscribedEngine.TrackLayoutChanged.RemoveListener(OnLayoutChanged);
        }
        public void Refresh()
        {
            lastTrackId = TrackId;
            var track = GetTrack();
            if (track != null) { TrackChanged.Invoke(track); LevelsChanged.Invoke(track); }
            else TrackCleared.Invoke();
        }
        private void OnLayoutChanged(TrackLayoutPayload layout) { Refresh(); }
        private void OnTrackChanged(TrackPayload track)
        {
            if (track.TrackId == TrackId) TrackChanged.Invoke(track);
        }
        private void OnLevelsChanged(TrackPayload track) { if (track.TrackId == TrackId) LevelsChanged.Invoke(track); }
        private void OnValidate() { TrackId = Mathf.Max(1, TrackId); }
        private void Update() { if (lastTrackId != TrackId) Refresh(); }
    }
}
