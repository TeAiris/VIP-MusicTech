using UnityEditor;
using UnityEngine;

namespace VIP.MusicTech.Audio.Editor
{
    public static class AudioEngineMenu
    {
        [MenuItem("Tools/Music Studio/Create Audio Engine")]
        [MenuItem("GameObject/Music Studio/Audio Engine", false, 10)]
        private static void CreateAudioEngine(MenuCommand command)
        {
            var engineObject = new GameObject("Audio Engine");
            GameObjectUtility.SetParentAndAlign(engineObject, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(engineObject, "Create Audio Engine");
            Undo.AddComponent<MultitrackEngine>(engineObject);
            Selection.activeGameObject = engineObject;
            EditorGUIUtility.PingObject(engineObject);
        }
    }
}
