using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VIP.MusicTech.Audio
{
    public enum WaveformMode { FullClip, LiveOutput }
    public enum WaveformScrollDirection { RightToLeft, LeftToRight }
    [System.Serializable] public sealed class AudioTextureEvent : UnityEvent<Texture2D> { }

    [RequireComponent(typeof(RawImage))]
    public sealed class AudioWaveformView : MonoBehaviour
    {
        public AudioTrackBinding Track;
        public WaveformMode Mode;
        [Range(32, 4096)] public int Width = 1024;
        [Range(16, 512)] public int Height = 128;
        [Range(1, 60)] public float RefreshRate = 20;
        [Min(0.1f), Tooltip("Visual amplitude multiplier for live output; does not change audio volume.")]
        public float LiveGain = 1;
        [Min(0.1f), Tooltip("Seconds of live waveform history visible across the texture.")]
        public float LiveWindowSeconds = 5;
        public WaveformScrollDirection ScrollDirection = WaveformScrollDirection.RightToLeft;
        public Color Foreground = Color.cyan;
        public Color Background = new Color(0, 0, 0, 0);
        public AudioTextureEvent TextureReady = new AudioTextureEvent();
        public UnityEvent RenderFailed = new UnityEvent();
        public Texture2D Texture { get; private set; }
        private Color32[] pixels;
        private Color32[] displayPixels;
        private bool fullClipReady;
        private float clipStartSeconds;
        private double clipDurationSeconds;
        private readonly float[] samples = new float[8192];
        private readonly float[] minimumSamples = new float[8192];
        private AudioWaveformHistory liveHistory;
        private double liveClock, lastLiveDsp;
        private float renderedWindowSeconds;
        private string liveTrackName;
        private WaveformMode renderedMode;
        private RawImage image;
        private AudioTrackBinding subscribedBinding;
        private AudioClip renderedClip;
        private Coroutine renderRoutine;
        private float nextRefresh;

        private void OnEnable()
        {
            image = GetComponent<RawImage>();
            Texture = AudioTextureUtility.Create(Mathf.Clamp(Width, 32, 4096), Mathf.Clamp(Height, 16, 512));
            pixels = new Color32[Texture.width * Texture.height];
            displayPixels = new Color32[pixels.Length];
            image.texture = Texture;
            subscribedBinding = Track;
            if (subscribedBinding != null)
            {
                subscribedBinding.TrackChanged.AddListener(OnBoundTrackChanged);
                subscribedBinding.TrackCleared.AddListener(Render);
            }
            Render();
        }

        public void Render()
        {
            if (!isActiveAndEnabled || Texture == null) return;
            if (renderRoutine != null) { StopCoroutine(renderRoutine); renderRoutine = null; }
            fullClipReady = false;
            renderedMode = Mode;
            renderedClip = null;
            ResetLiveHistory();
            AudioTextureUtility.Clear(pixels, Background);
            AudioTextureUtility.Upload(Texture, pixels);
            if (Mode == WaveformMode.FullClip && Track != null && Track.Engine != null && Track.Engine.TrackCount > 0)
            {
                var track = Track.GetTrack();
                if (track != null)
                {
                    clipStartSeconds = track.StartSeconds;
                    renderedClip = track.Clip;
                    clipDurationSeconds = (double)track.Clip.samples / track.Clip.frequency;
                    renderRoutine = StartCoroutine(RenderClip(track.Clip));
                }
            }
        }

        private IEnumerator RenderClip(AudioClip clip)
        {
            yield return null;
            // Fixed-size reads keep memory independent of song length.
            if (clip == null || clip.loadState != AudioDataLoadState.Loaded || clip.loadType != AudioClipLoadType.DecompressOnLoad)
            { Fail(); yield break; }
            int width = Texture.width;
            var minima = new float[width];
            var maxima = new float[width];
            const int framesPerRead = 4096;
            var buffer = new float[framesPerRead * clip.channels];
            int reads = 0;
            for (int offset = 0; offset < clip.samples; offset += framesPerRead)
            {
                if (!clip.GetData(buffer, offset)) { Fail(); yield break; }
                int validFrames = Mathf.Min(framesPerRead, clip.samples - offset);
                for (int frame = 0; frame < validFrames; frame++)
                {
                    int x = (int)((long)(offset + frame) * width / clip.samples);
                    for (int channel = 0; channel < clip.channels; channel++)
                    {
                        float sample = buffer[frame * clip.channels + channel];
                        minima[x] = Mathf.Min(minima[x], sample);
                        maxima[x] = Mathf.Max(maxima[x], sample);
                    }
                }
                if (++reads % 16 == 0) yield return null;
            }
            for (int x = 0; x < width; x++) AudioTextureUtility.Column(pixels, width, Texture.height, x, minima[x], maxima[x], Foreground);
            AudioTextureUtility.Upload(Texture, pixels);
            renderRoutine = null;
            fullClipReady = true;
            DrawPlayhead();
            TextureReady.Invoke(Texture);
        }

        private void Update()
        {
            if (Mode != renderedMode) Render();
            if (Track == null || Track.Engine == null || Texture == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1 / Mathf.Max(1, RefreshRate);
            if (Mode == WaveformMode.FullClip)
            {
                if (fullClipReady) DrawPlayhead();
                return;
            }
            if (Track.Engine.TrackCount == 0) return;
            DrawLiveHistory();
        }

        private void ResetLiveHistory()
        {
            if (Texture == null) return;
            renderedWindowSeconds = SafeWindowSeconds();
            liveHistory = new AudioWaveformHistory(Texture.width, renderedWindowSeconds);
            liveClock = 0;
            lastLiveDsp = AudioSettings.dspTime;
        }

        private float SafeWindowSeconds() => float.IsNaN(LiveWindowSeconds) || float.IsInfinity(LiveWindowSeconds)
            ? 5 : Mathf.Max(0.1f, LiveWindowSeconds);
        public void SetLiveWindowSeconds(float seconds) { LiveWindowSeconds = seconds; ResetLiveHistory(); }
        public void SetScrollRightToLeft(bool enabled) { ScrollDirection = enabled ? WaveformScrollDirection.RightToLeft : WaveformScrollDirection.LeftToRight; }
        public void SetLiveGain(float gain) { if (!float.IsNaN(gain) && !float.IsInfinity(gain)) LiveGain = Mathf.Max(0.1f, gain); }
        public void ClearLiveHistory() { ResetLiveHistory(); }

        private void DrawLiveHistory()
        {
            if (liveHistory == null || renderedWindowSeconds != SafeWindowSeconds()) ResetLiveHistory();
            double now = AudioSettings.dspTime;
            double elapsed = System.Math.Max(0, now - lastLiveDsp);
            lastLiveDsp = now;
            if (Track.Engine.State == TransportState.Stopped || Track.Engine.State == TransportState.Empty)
            {
                ResetLiveHistory();
                AudioTextureUtility.Clear(pixels, Background);
                AudioTextureUtility.Upload(Texture, pixels);
                return;
            }
            if (Track.Engine.State == TransportState.Playing)
            {
                liveClock += elapsed;
                if (Track.Engine.CopyTrackOutputEnvelope(Track.TrackId, minimumSamples, samples))
                {
                    int rate = Mathf.Max(1, AudioSettings.outputSampleRate);
                    int count = Mathf.Clamp((int)System.Math.Min(samples.Length, elapsed * rate), 0, samples.Length);
                    for (int i = samples.Length - count; i < samples.Length; i++)
                        liveHistory.Add(liveClock - (samples.Length - 1 - i) / (double)rate, minimumSamples[i], samples[i]);
                }
                liveHistory.Advance(liveClock);
            }
            AudioTextureUtility.Clear(pixels, Background);
            int width = Texture.width;
            for (int x = 0; x < width; x++)
            {
                if (!liveHistory.ReadColumn(x, ScrollDirection == WaveformScrollDirection.RightToLeft, out float low, out float high)) continue;
                float gain = float.IsNaN(LiveGain) || float.IsInfinity(LiveGain) ? 1 : Mathf.Max(0.1f, LiveGain);
                AudioTextureUtility.Column(pixels, width, Texture.height, x, low * gain, high * gain, Foreground);
            }
            AudioTextureUtility.Upload(Texture, pixels);
        }

        private void DrawPlayhead()
        {
            // Keep the rendered waveform pristine; each display frame starts from that cache.
            System.Array.Copy(pixels, displayPixels, pixels.Length);
            var engine = Track.Engine;
            double localSeconds = engine.PositionSeconds - clipStartSeconds;
            if (engine.ShowPlayhead && engine.TrackCount > 0 && clipDurationSeconds > 0 &&
                localSeconds >= 0 && localSeconds <= clipDurationSeconds)
            {
                int width = Texture.width;
                int lineWidth = Mathf.Min(width, engine.PlayheadWidth);
                int center = Mathf.RoundToInt((float)(localSeconds / clipDurationSeconds) * (width - 1));
                int first = Mathf.Clamp(center - lineWidth / 2, 0, width - lineWidth);
                Color32 color = engine.PlayheadColor;
                for (int y = 0; y < Texture.height; y++)
                    for (int x = first; x < first + lineWidth; x++) displayPixels[y * width + x] = color;
            }
            AudioTextureUtility.Upload(Texture, displayPixels);
        }

        private void OnBoundTrackChanged(TrackPayload track)
        {
            if (Mode == WaveformMode.FullClip && (track.Clip != renderedClip || track.StartSeconds != clipStartSeconds)) Render();
            if (Mode == WaveformMode.LiveOutput && (liveTrackName != track.TrackName || renderedClip != track.Clip))
            {
                liveTrackName = track.TrackName;
                renderedClip = track.Clip;
                ResetLiveHistory();
            }
        }
        private void Fail()
        {
            renderRoutine = null;
            Debug.LogWarning("Full waveform requires readable audio imported with Decompress On Load. Live output can still be used.", this);
            RenderFailed.Invoke();
        }
        private void OnDisable()
        {
            if (subscribedBinding != null)
            {
                subscribedBinding.TrackChanged.RemoveListener(OnBoundTrackChanged);
                subscribedBinding.TrackCleared.RemoveListener(Render);
            }
            if (renderRoutine != null) { StopCoroutine(renderRoutine); renderRoutine = null; }
            if (image != null && image.texture == Texture) image.texture = null;
            if (Texture != null) Destroy(Texture);
            Texture = null;
            fullClipReady = false;
        }
    }
}
