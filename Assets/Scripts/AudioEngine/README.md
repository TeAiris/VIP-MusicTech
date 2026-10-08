# Multitrack audio engine

Namespace: `VIP.MusicTech.Audio`. Runtime assets never change when you mix tracks.

## Unity setup

1. Create an asset with **Assets > Create > Music Studio > Multitrack Session**.
2. Add tracks, give each a unique `TrackName` and AudioClip. Track numbers are assigned automatically from list order; names also work as optional API lookup keys. Volume defaults to 1. `StartSeconds` places the clip on the session timeline; shorter tracks end naturally. Session duration includes offsets. BPM is metadata.
3. Use **Tools > Music Studio > Create Audio Engine** (also **GameObject > Music Studio > Audio Engine**) to create and select an object with `MultitrackEngine`. Creation supports Undo. Assign **Initial Session** and optionally **Play On Start**. Keep one enabled AudioListener in the scene.
4. Optional: assign each track a prefab with an AudioSource on its root and audio filters (low-pass, high-pass, echo, reverb, etc.) on that same object. Disable Play On Awake on prefab sources. The engine instantiates each prefab and configures its source for 2D, non-looping playback at pitch 1. Avoid extra sources on these prefabs.
5. Add `AudioTrackBinding` to each channel UI, assign the engine and **Track Id** (1 for the first track, 2 for the second, etc.). IDs are automatic layout positions; you never enter an ID in the session asset. Connect slider `OnValueChanged(float)` to `SetVolume` (0–1) or `SetPan` (-1–1), toggles to `SetMuted`/`SetSolo`, and buttons to `ToggleMuted`/`ToggleSolo`. Transport buttons call engine `Play`, `Pause`, `Stop`, or `TogglePlayback`; timeline sliders call `SeekNormalized`.
6. Add `AudioWaveformView` to a UI RawImage, assign the track binding, and select `FullClip` or `LiveOutput`. Add `AudioMeterView` to another RawImage for a vertical peak or RMS meter. These components create, update, and dispose their own plain RGBA textures. Their `Texture` property can also feed custom materials; the waveform emits `TextureReady` when a full render completes.

Assign references before enabling bindings/views. Disable and enable them after changing their engine reference. Call waveform `Render()` after changing its track or colors; mode changes refresh automatically. Disable/enable after changing texture dimensions. Full waveforms show the entire clip, without its timeline offset. **LiveOutput** displays scrolling waveform history, combining all clip channels by min/max envelope without phase cancellation. **Live Window Seconds** controls the duration across the texture (default 5 seconds); **Scroll Direction** chooses Right To Left (new audio enters on the right) or Left To Right (new audio enters on the left). History fills from empty, holds when paused, clears on stop or track changes, and continues across loop boundaries. Changing the window duration clears history. **Live Gain** changes visual amplitude without changing audio volume. Full rendering combines all channels by min/max envelope and uses fixed-size sample reads, spread across frames.

Waveform UnityEvent methods include `SetLiveWindowSeconds(float)`, `SetScrollRightToLeft(bool)`, `SetLiveGain(float)`, and `ClearLiveHistory()`. History uses fixed memory proportional to texture width. It aggregates the recent output available at each refresh; a stall or refresh interval longer than Unity's 8192-sample output history can leave gaps. Use at least 20 Hz refresh for a normal 44.1/48 kHz device. This is a visualization, not a recording buffer.

Meter **Floor Decibels** and **Ceiling Decibels** control the visible dBFS range (defaults -60 to 0). **Warning Decibels** starts the yellow band (-12), and **Clip Decibels** starts the red band (-3). Thresholds are absolute dB values, so changing the visible range preserves their meaning. Values below the floor show empty and values above the ceiling fill the meter. The ceiling must be greater than the floor; invalid ranges are corrected. `SetMinimumDecibels(float)`, `SetMaximumDecibels(float)`, `SetWarningDecibels(float)`, and `SetClipDecibels(float)` are callable from UnityEvents. Peak/RMS selection and release speed remain configurable.

Meters and live waveforms share an engine-owned output cache with a fixed native history size (8192 samples per channel). Caller buffer lengths select recent samples from that cache, and native reads occur at most once per track per frame. `CopyTrackOutputEnvelope(trackId, minima, maxima)` exposes the multichannel live envelope. Unity's output history starts empty until audio has been processed after the first read, so allow a short warmup after starting or switching loop voices. Live output requires actual playback and an active audio listener/device; stopped or muted tracks are silent. The `AudioDevice` regression test uses a right-channel-only stereo tone and simultaneous 512/1024/8192-sample readers, and requires audio enabled.

## API example

```csharp
using UnityEngine;
using VIP.MusicTech.Audio;

public class StudioControls : MonoBehaviour
{
    public MultitrackEngine Engine;
    public MultitrackSession Song;

    private void Start()
    {
        Engine.SessionLoaded.AddListener(OnLoaded);
        Engine.LevelsChanged.AddListener(OnLevels);
        Engine.LoadSession(Song);
    }

    private void OnLoaded()
    {
        Engine.SetTrackVolume("drums", 0.8f);
        Engine.Play();
    }

    private void OnLevels(TrackPayload track)
    {
        // TrackName, Peak, Rms, PeakDecibels, RmsDecibels, etc.
    }

    private void OnDestroy()
    {
        if (Engine == null) return;
        Engine.SessionLoaded.RemoveListener(OnLoaded);
        Engine.LevelsChanged.RemoveListener(OnLevels);
    }
}
```

`LoadSession` loads asynchronously. Wait for `SessionLoaded`; loading errors emit `Error` with `Operation`, `TrackName`, and `Message`. Set Play On Start off when you control playback yourself. It applies to each successful load. `UnloadSession` cancels loading, stops sources, and destroys owned track objects. Disabling the engine pauses playback or cancels an in-progress load; re-enabling does not automatically resume.

`GetTrack(trackName)` and `GetTrackAt(index)` return labeled snapshots; `TrackCount` supports enumeration. Invalid lookups return null and emit an error. `TrackChanged` reports mix/name edits, while `LevelsChanged` and `PositionChanged` publish at the configured update rate using unscaled time. `TransportChanged` reports transport/master edits. All events are serialized UnityEvents, usable from Inspector or reflected visual nodes. `AudioTrackBinding` filters track and level events by numeric layout ID. Bind payload fields in visual scripting for labels and readouts. No FlowCanvas-specific dependency is required.

`GetTrackSource(trackName)` exposes the instantiated source for adding or adjusting audio filters and mixer routing. Leave playback, clip, pitch, volume, mute, pan, and loop under engine control to preserve synchronization. Track output buffers use `CopyTrackOutput(trackName, callerOwnedBuffer, channel)`; lengths must be powers of two from 64 to 8192. Peak/RMS combine all clip channels, in linear amplitude and dBFS (floor -100 dB), sampled from source output. Meters are UI readings, not guaranteed inter-sample or true-peak measurements. Mixer effects/attenuation downstream are not included.

## Mixing and timing

Master volume multiplies each track volume. Master mute, track mute, and solo gating combine without stopping sources, so unmuting preserves sync. Mute wins over solo. Optional track `OutputGroup` overrides the engine default mixer group. There is no dedicated master source or actual summed master bus meter: attach shared bus effects to an AudioMixer group and route all sources there when needed.

Tracks use a shared `AudioSettings.dspTime` anchor and `PlayScheduled`, including tracks with delayed starts. Pause cancels scheduled sources; resume and seek reschedule from the stored position with a short preparation lead (default 100 ms). The transport holds during that lead. Stop resets to zero; natural completion holds at the end and emits `PlaybackCompleted`. Play at the end restarts from zero. Seeks clamp to session duration. Transport intentionally has no independent track playback, pitch changes, or recording yet.

Enable **Loop** on the engine or call `SetLoop(true)` / `ToggleLoop()`. The whole session repeats at its duration, retaining each track's start offset and silence after shorter clips. Each track object has two AudioSource voices: the next pass is scheduled ahead against the DSP clock, without adding the preparation lead on each wrap. `PlaybackLooped` fires on wrap; `PlaybackCompleted` fires only when playback ends with looping disabled. `TransportPayload.Loop` exposes the setting to visual nodes. Pause, stop, seek, and unload cancel queued voices. A main-thread stall longer than a complete session pass triggers synchronized recovery with the preparation lead. Very short sessions still require the main thread to queue subsequent passes before their boundaries.

On the engine, **Global Waveform Playhead** provides **Show Playhead**, **Playhead Width** (texture pixels), and **Playhead Color**. Full-clip waveform textures overlay the line at `(engine position - track start) / clip duration`; it hides outside the clip's time range, holds when paused, resets on stop, and wraps with looping. All waveform views read these global settings. Live oscilloscope windows have no timeline playhead. API methods are `SetShowPlayhead(bool)`, `SetPlayheadWidth(int)`, and `SetPlayheadColor(Color)`. Updates follow each view's Refresh Rate. The cached waveform is preserved beneath the overlay.

`GetTrackSource` returns the voice for the current pass, which changes at a loop boundary. Add or adjust effect components on its GameObject to control the shared track object. If changing AudioSource-specific settings beyond the mix API, update both voices on that object. The engine mirrors mixer routing to the next voice when queuing a pass.

For accurate seeking and full waveform rendering use **Decompress On Load** and **Preload Audio Data** in clip import settings. Compressed/streamed sources can play, but seek precision and preparation latency depend on the codec/platform. Raise Schedule Lead Seconds for slow streaming media. Full waveform rendering rejects non-decompressed clips and invokes `RenderFailed`, instead of silently displaying unreadable data.

Unity API references: [PlayScheduled](https://docs.unity.com/en-us/engine/6000.7/script-reference/unityengine/audiosource/playscheduled), [GetOutputData](https://docs.unity.com/en-us/engine/6000.7/script-reference/unityengine/audiosource/getoutputdata), [AudioClip.GetData](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/audioclip/getdata).

## Verification

The `Tests` assembly contains Play Mode tests for loading/offset duration, mute/solo/master composition, asset isolation, seek/pause/stop, duplicate names and rename validation, snapshot semantics, silent stopped output, and unload cleanup. Run them from Unity **Window > General > Test Runner > PlayMode**. Test actual audible sync, filter behavior, and RawImage appearance in a scene on your target device; standalone C# compilation cannot verify Unity's audio device behavior.

Track names are case-sensitive and must be nonblank and unique within a session for the optional name-based API. Numeric bindings are unaffected by renaming. Existing name-based binding components must be assigned their desired numeric Track Id once. Runtime renaming and reordering leave the session asset unchanged; reloading restores asset order.

## Mixer layout and automatic track IDs

The Inspector labels session entries **Track 1**, **Track 2**, etc., based on their list order. `TrackPayload.TrackId` is that 1-based position; `TrackIndex` remains the zero-based index for code. Engine control methods accept numeric IDs as well as names, e.g. `SetTrackVolume(2, 0.8f)`, `GetTrack(2)`, and `GetTrackSource(2)`. `AudioTrackBinding.TrackName` is a read-only convenience property describing the current occupant.

Use `MoveTrackUp(trackId)` to swap with the previous slot or `MoveTrackDown(trackId)` to swap with the next. `MoveTrack(fromTrackId, toTrackId)` inserts at a destination and shifts intervening tracks. Out-of-range moves and moves beyond the first/last slot are no-ops. Binding components expose parameterless `MoveTrackUp()` / `MoveTrackDown()` for UnityEvent buttons, and `SetTrackId(int)` for selecting a channel.

Bindings stay attached to their numbered mixer slots. Moving drums from 1 to 2 means channel 1 now controls the previous channel 2 track, and channel 2 controls drums. Sources, effects, gain, mute/solo, and playback move with the track; audio is not restarted. All enabled bindings refresh through `TrackLayoutChanged`, whose labeled payload contains the source/destination IDs and a snapshot of the complete new order. `TrackChanged` and `LevelsChanged` on each binding refresh labels, controls, and meters. Full waveforms automatically rebuild when the bound clip or offset changes; live waveforms immediately sample the new slot. `TrackCleared` handles channels beyond the loaded track count and unloading.

For a mixer prefab, assign consecutive IDs to its channel bindings and connect each channel's waveform/meter to that binding. Connect labels and sliders to binding `TrackChanged` payload fields; use slider `SetValueWithoutNotify` when reflecting volume values to avoid feedback into setters. New sessions automatically populate those same slots in their asset order.

The session Inspector adds fresh tracks with Volume 1 and a unique suggested name. Existing saved volume values remain unchanged.

In Play Mode, the engine Inspector's **Live Audio Diagnostics** shows each numeric track ID, playback state, sample position, track/effective gain, and measured peak/RMS. It warns about zero saved volume and mute/solo suppression. **Set Track Volume to 1** repairs a zero runtime gain without editing the asset. Full waveforms read clip data independently of gain, so they can render even while a track's live output is silent. Stop and restart Play Mode after changing saved session volumes to reload those values.
