using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace VIP.MusicTech.Audio.Editor
{
    [CustomEditor(typeof(MultitrackSession))]
    public sealed class MultitrackSessionEditor : UnityEditor.Editor
    {
        private ReorderableList trackList;

        private void OnEnable()
        {
            var tracks = serializedObject.FindProperty("Tracks");
            trackList = new ReorderableList(serializedObject, tracks, true, true, true, true);
            trackList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Tracks");
            trackList.elementHeightCallback = index =>
                EditorGUI.GetPropertyHeight(tracks.GetArrayElementAtIndex(index), true) + 4;
            trackList.drawElementCallback = (rect, index, active, focused) =>
            {
                rect.y += 2;
                rect.height = EditorGUI.GetPropertyHeight(tracks.GetArrayElementAtIndex(index), true);
                var track = tracks.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(rect, track, new GUIContent("Track " + (index + 1) + ": " + track.FindPropertyRelative("TrackName").stringValue), true);
            };
            trackList.onAddCallback = list =>
            {
                // Unity's default list insertion can duplicate the previous entry or zero fields.
                // Construct a fresh definition so Volume is always 1 for a newly added track.
                serializedObject.ApplyModifiedProperties();
                var session = (MultitrackSession)target;
                Undo.RecordObject(session, "Add Audio Track");
                if (session.Tracks == null) session.Tracks = new System.Collections.Generic.List<AudioTrackDefinition>();
                string name = "Track";
                int suffix = 2;
                while (session.Tracks.Exists(track => track != null && track.TrackName == name)) name = "Track " + suffix++;
                session.Tracks.Add(new AudioTrackDefinition { TrackName = name });
                EditorUtility.SetDirty(session);
                serializedObject.Update();
                list.index = session.Tracks.Count - 1;
                tracks.GetArrayElementAtIndex(list.index).isExpanded = true;
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SessionName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("BeatsPerMinute"));
            trackList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
