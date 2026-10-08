using UnityEditor;
using UnityEngine;

namespace VIP.MusicTech.Audio.Editor
{
    [CustomEditor(typeof(MultitrackEngine))]
    public sealed class MultitrackEngineEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var engine = (MultitrackEngine)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live Audio Diagnostics", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect each numbered track's playback, gain, and measured output.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("Transport", engine.State.ToString());
            EditorGUILayout.LabelField("Position", engine.PositionSeconds.ToString("F2") + " / " + engine.DurationSeconds.ToString("F2") + " s");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Play")) engine.Play();
            if (GUILayout.Button("Pause")) engine.Pause();
            if (GUILayout.Button("Stop")) engine.Stop();
            EditorGUILayout.EndHorizontal();
            if (AudioListener.pause) EditorGUILayout.HelpBox("AudioListener.pause is enabled: Unity audio is paused.", MessageType.Warning);
            if (engine.MasterMuted || engine.MasterVolume <= 0)
                EditorGUILayout.HelpBox("The master channel is silent. Unmute it and raise Master Volume to see live output.", MessageType.Warning);
            for (int id = 1; id <= engine.TrackCount; id++)
            {
                var track = engine.GetTrack(id);
                var source = engine.GetTrackSource(id);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Track " + id + ": " + track.TrackName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Track / effective volume", track.Volume.ToString("F3") + " / " + source.volume.ToString("F3"));
                EditorGUILayout.LabelField("Source", (source.isPlaying ? "Playing" : "Stopped") + (source.isVirtual ? " (virtualized)" : "") + (source.mute ? " (muted)" : ""));
                EditorGUILayout.LabelField("Audio sample position", source.timeSamples.ToString());
                EditorGUILayout.LabelField("Peak / RMS", track.PeakDecibels.ToString("F1") + " / " + track.RmsDecibels.ToString("F1") + " dBFS");
                if (track.Volume <= 0)
                {
                    EditorGUILayout.HelpBox("This track's saved volume is zero. Full waveforms can still render the clip, but live output and meters are silent.", MessageType.Warning);
                    if (GUILayout.Button("Set Track " + id + " Volume to 1")) engine.SetTrackVolume(id, 1);
                }
                else if (!track.Audible)
                    EditorGUILayout.HelpBox("This track is suppressed by master mute, track mute, or another track's solo setting.", MessageType.Warning);
            }
        }
    }
}
