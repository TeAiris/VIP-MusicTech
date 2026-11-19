using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Room-sized practical lighting for the imported studio, in edit and Play Mode.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class StudioLighting : MonoBehaviour
{
    [SerializeField, Range(0.1f, 3f)] private float brightness = 1f;
    [SerializeField, Min(1f)] private float spotRange = 6f;
    [SerializeField, Min(1f)] private float lampRange = 3.5f;
    [SerializeField] private bool softShadows = false;
    [SerializeField] private bool keyLightShadows = true;
    [SerializeField, Range(0f, 3f)] private float bounceStrength = 1f;
    private bool pending = true;

    private void OnEnable() => pending = true;
    private void OnValidate() => pending = true;

    private void Update()
    {
        if (!pending)
            return;
        pending = false;
        ApplyLighting();
    }

    [ContextMenu("Apply Studio Lighting")]
    public void ApplyLighting()
    {
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            return;

        foreach (var root in gameObject.scene.GetRootGameObjects())
        foreach (var light in root.GetComponentsInChildren<Light>(true))
        {
            if (!light.name.StartsWith("L_", System.StringComparison.Ordinal))
                continue;

            // Absolute values keep repeated application and model reimports consistent.
            float intensity;
            if (light.name.StartsWith("L_CR_Cloud")) intensity = 2.2f;
            else if (light.name == "L_CR_FloorLamp") intensity = 1.1f;
            else if (light.name.StartsWith("L_CR_Rear")) intensity = 0.95f;
            else if (light.name.StartsWith("L_CR_Side")) intensity = 0.7f;
            else if (light.name.StartsWith("L_LB_Pendant")) intensity = 0.85f;
            else if (light.name.StartsWith("L_LB_Spot")) intensity = 1.65f;
            else if (light.name.StartsWith("L_TR_Spot")) intensity = 2f;
            else if (light.name.StartsWith("L_Booth")) intensity = 1.8f;
            else continue;

            light.intensity = intensity * brightness;
            light.range = light.type == LightType.Point ? lampRange : spotRange;
            // Four spot shadow maps instead of shadows on every fixture (including point lights).
            bool keyLight = light.name == "L_CR_Cloud1" || light.name == "L_TR_Spot3"
                || light.name == "L_TR_Spot9" || light.name == "L_Booth0";
            light.shadows = keyLightShadows && keyLight
                ? (softShadows ? LightShadows.Soft : LightShadows.Hard)
                : LightShadows.None;
            light.shadowStrength = 0.65f;
            light.shadowBias = 0.025f;
            light.shadowNormalBias = 0.15f;
            light.shadowNearPlane = 0.1f;

            var data = light.GetComponent<UniversalAdditionalLightData>();
            if (data != null && Application.isPlaying)
                data.additionalLightsShadowResolutionTier = 0;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                if (data != null)
                {
                    var serialized = new UnityEditor.SerializedObject(data);
                    serialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = 0;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(data);
                }
            }
#endif
        }

        // Subtle, room-local fills approximate the indirect light visible in the reference.
        // Coordinates are relative to the imported model so moving the studio keeps them aligned.
        Transform studio = null;
        foreach (var root in gameObject.scene.GetRootGameObjects())
            if (root.name == "RecordingStudio") studio = root.transform;
        if (studio == null) return;
        RestoreFixtureEmission(studio);

        SetBounce("Control Lounge", studio, new Vector3(-2.8f, 1.4f, -1.4f), 1.5f, 4.2f);
        SetBounce("Control Desk", studio, new Vector3(-3f, 1.6f, -3.8f), 0.9f, 3.2f);
        SetBounce("Tracking West", studio, new Vector3(-2.8f, 1.8f, -8f), 1.6f, 4.5f);
        SetBounce("Tracking East", studio, new Vector3(-8.8f, 1.8f, -8f), 1.6f, 4.5f);
        SetBounce("Tracking Rear", studio, new Vector3(-6f, 1.8f, -11.4f), 1.4f, 4.5f);
        SetBounce("Lobby West", studio, new Vector3(-8.3f, 1.6f, 1.2f), 1.5f, 4f);
        SetBounce("Lobby East", studio, new Vector3(-12.2f, 1.6f, 1.2f), 1.5f, 4f);
        SetBounce("Booth", studio, new Vector3(-7.9f, 1.3f, -3.5f), 0.7f, 2.3f);
        SetBounce("Closet", studio, new Vector3(-10.7f, 1.3f, -3.5f), 0.6f, 1.8f);
    }

    private static void RestoreFixtureEmission(Transform studio)
    {
        var block = new MaterialPropertyBlock();
        int gltfEmission = Shader.PropertyToID("emissiveFactor");
        int urpEmission = Shader.PropertyToID("_EmissionColor");
        foreach (var renderer in studio.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null) continue;
                float strength;
                switch (material.name.Replace(" (Instance)", ""))
                {
                    case "lamp": strength = 8f; break;
                    case "shade": strength = 2.2f; break;
                    case "led_warm": case "led_gold": strength = 5f; break;
                    case "led_teal": strength = 3.15f; break;
                    case "led_red": strength = 4f; break;
                    default: continue;
                }
                int property = material.HasProperty(gltfEmission) ? gltfEmission : urpEmission;
                if (!material.HasProperty(property)) continue;
                renderer.GetPropertyBlock(block, i);
                block.SetColor(property, material.GetColor(property) * strength);
                renderer.SetPropertyBlock(block, i);
                block.Clear();
            }
        }
    }

    private void SetBounce(string room, Transform studio, Vector3 position, float intensity, float range)
    {
        string childName = "Bounce Fill - " + room;
        var child = transform.Find(childName);
        if (child == null)
        {
            var fill = new GameObject(childName, typeof(Light));
            child = fill.transform;
            child.SetParent(transform, false);
        }
        child.position = studio.TransformPoint(position);
        var light = child.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.88f, 0.72f);
        light.intensity = intensity * bounceStrength;
        light.range = range;
        light.shadows = LightShadows.None;
        light.bounceIntensity = 0f;
        light.enabled = bounceStrength > 0f;
    }
}
