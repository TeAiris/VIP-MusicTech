using UnityEngine;
using UnityEngine.UI;

namespace VIP.MusicTech.Audio
{
    [RequireComponent(typeof(RawImage))]
    public sealed class AudioMeterView : MonoBehaviour
    {
        public AudioTrackBinding Track;
        public bool UseRms;
        public float FloorDecibels = -60;
        public float CeilingDecibels;
        [Tooltip("Start of the yellow band, in dBFS.")]
        public float WarningDecibels = -12;
        [Tooltip("Start of the red band, in dBFS. This is a visual threshold, not a true-peak detector.")]
        public float ClipDecibels = -3;
        [Min(0)] public float ReleasePerSecond = 1.5f;
        public Color Background = new Color(0.03f, 0.03f, 0.03f, 1);
        public Color LowColor = Color.green;
        public Color HighColor = Color.yellow;
        public Color ClipColor = Color.red;
        public Texture2D Texture { get; private set; }
        private readonly Color32[] pixels = new Color32[16 * 128];
        private RawImage image;
        private float displayed;
        private float nextRefresh;
        private void OnEnable()
        {
            image = GetComponent<RawImage>();
            Texture = AudioTextureUtility.Create(16, 128);
            image.texture = Texture;
            displayed = 0;
        }
        private void Update()
        {
            float target = 0;
            if (Track != null && Track.Engine != null && Track.Engine.TrackCount > 0)
            {
                var track = Track.GetTrack();
                if (track != null && Track.Engine.State == TransportState.Playing)
                {
                    float amplitude = UseRms ? track.Rms : track.Peak;
                    if (amplitude > 0)
                        target = NormalizeDecibels(20 * Mathf.Log10(amplitude), FloorDecibels, CeilingDecibels);
                }
            }
            displayed = target >= displayed ? target : Mathf.MoveTowards(displayed, target, ReleasePerSecond * Time.unscaledDeltaTime);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f / 30;
            AudioTextureUtility.Clear(pixels, Background);
            int filled = Mathf.RoundToInt(displayed * 128);
            for (int y = 0; y < filled; y++)
            {
                float decibels = Mathf.Lerp(FloorDecibels, Mathf.Max(FloorDecibels + 0.1f, CeilingDecibels), (y + 0.5f) / 128);
                Color32 color = decibels >= ClipDecibels ? ClipColor : decibels >= WarningDecibels ? HighColor : LowColor;
                for (int x = 0; x < 16; x++) pixels[y * 16 + x] = color;
            }
            AudioTextureUtility.Upload(Texture, pixels);
        }
        public static float NormalizeDecibels(float decibels, float minimum, float maximum)
            => Mathf.InverseLerp(minimum, Mathf.Max(minimum + 0.1f, maximum), decibels);
        public void SetMinimumDecibels(float value) { if (!Finite(value)) return; FloorDecibels = value; ValidateRange(); }
        public void SetMaximumDecibels(float value) { if (!Finite(value)) return; CeilingDecibels = value; ValidateRange(); }
        public void SetWarningDecibels(float value) { if (Finite(value)) WarningDecibels = value; }
        public void SetClipDecibels(float value) { if (Finite(value)) ClipDecibels = value; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void ValidateRange()
        {
            if (!Finite(FloorDecibels)) FloorDecibels = -60;
            if (!Finite(CeilingDecibels)) CeilingDecibels = 0;
            CeilingDecibels = Mathf.Max(FloorDecibels + 0.1f, CeilingDecibels);
        }
        private void OnValidate() { ValidateRange(); }
        private void OnDisable()
        {
            if (image != null && image.texture == Texture) image.texture = null;
            if (Texture != null) Destroy(Texture);
            Texture = null;
        }
    }
}
