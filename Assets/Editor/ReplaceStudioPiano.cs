using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ReplaceStudioPiano
{
    private const string Request = "Temp/replace-grand-piano.request";
    private const string Result = "Temp/replace-grand-piano.result";

    [InitializeOnLoadMethod]
    private static void CheckRequest()
    {
        if (File.Exists(Request)) EditorApplication.delayCall += Replace;
    }

    [MenuItem("Tools/Studio/Replace Piano With Fixed Model")]
    public static void Replace()
    {
        try
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Studio.unity")
                throw new InvalidOperationException("Open Studio in Edit Mode before replacing the piano.");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            if (all.Any(t => t.name == "GrandPiano (Fixed)"))
                throw new InvalidOperationException("The fixed piano is already present.");
            var old = all.Single(t => t.name == "TR_GrandPiano" && t.gameObject.activeInHierarchy);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/GrandPiano.glb");
            if (prefab == null) throw new InvalidOperationException("The fixed GLB has not imported.");
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(replacement, "Replace studio piano");
            replacement.name = "GrandPiano (Fixed)";
            replacement.transform.SetParent(old.parent, false);
            replacement.transform.localPosition = old.localPosition;
            replacement.transform.localRotation = old.localRotation;
            replacement.transform.localScale = old.localScale;
            var keys = replacement.GetComponentsInChildren<Transform>(true)
                .Count(t => t.name.StartsWith("TR_GrandPiano_Key_", StringComparison.Ordinal));
            if (keys != 88) throw new InvalidOperationException("Expected 88 replacement keys; got " + keys);
            Undo.RecordObject(old.gameObject, "Hide original piano");
            old.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(old.gameObject);
            // The supplied replacement includes its own matching bench.
            foreach (var bench in all.Where(t => t.name == "TR_PianoBench" && t.parent == old.parent))
            {
                Undo.RecordObject(bench.gameObject, "Hide original piano bench");
                bench.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bench.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Studio.");
            Selection.activeGameObject = replacement;
            SceneView.lastActiveSceneView?.FrameSelected();
            File.WriteAllText(Result, "SUCCESS: Replaced piano and bench; 88 keys; preserved original transform; Studio saved.");
        }
        catch (Exception error)
        {
            File.WriteAllText(Result, "ERROR: " + error);
            Debug.LogException(error);
        }
        finally
        {
            if (File.Exists(Request)) File.Delete(Request);
        }
    }
}
