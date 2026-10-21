using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using VIP.MusicTech.AreaPopups;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace VIP.MusicTech.AreaPopups.Editor
{
    public static class AreaPopupMenu
    {
        private const string ScenePath = "Assets/Scenes/StudioHub.unity";

        [MenuItem("Tools/Music Studio/Create Area Popup Demo")]
        [MenuItem("GameObject/Music Studio/Area Popup Demo", false, 12)]
        private static void CreateAreaPopupDemo(MenuCommand command)
        {
            var root = new GameObject("Area Popup Demo");
            GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(root, "Create Area Popup Demo");

            var bootstrap = Undo.AddComponent<AreaPopupBootstrap>(root);
            bootstrap.Build();

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
        }

        [MenuItem("Tools/Music Studio/Add Presence Probe To Selection")]
        private static void AddPresenceProbe()
        {
            var target = Selection.activeGameObject;
            if (target == null)
            {
                EditorUtility.DisplayDialog("Presence Probe", "Select your XR Origin or Main Camera first.", "OK");
                return;
            }

            Undo.AddComponent<AreaPresenceProbe>(target);
            var probe = target.GetComponent<AreaPresenceProbe>();
            probe.EnsurePhysicsBody();
            EditorGUIUtility.PingObject(target);
        }

        [MenuItem("Tools/Music Studio/Create StudioHub Area Popup Scene")]
        private static void CreateStudioHubAreaPopupScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f, 0.08f, 0.10f, 1f);
                if (camera.GetComponent<AudioListener>() == null)
                    camera.gameObject.AddComponent<AudioListener>();
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            var demo = new GameObject("Area Popup Demo");
            var bootstrap = demo.AddComponent<AreaPopupBootstrap>();
            bootstrap.Build();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"Saved area-popup demo scene to {ScenePath}. Press Play and fly into a zone (WASD, right-mouse look).");
        }
    }
}
