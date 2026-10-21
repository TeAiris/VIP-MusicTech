using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace VIP.MusicTech.AreaPopups
{
    /// <summary>
    /// Builds a test layout of studio area trigger boxes + world-space popups.
    /// Safe for headset or editor play mode with a presence probe on the camera/rig.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class AreaPopupBootstrap : MonoBehaviour
    {
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private bool createEditorFlyProbe = true;
        [SerializeField] private float zoneSpacing = 3.5f;

        public AreaPopupDirector Director { get; private set; }

        private void Awake()
        {
            if (buildOnAwake) Build();
        }

        [ContextMenu("Rebuild Area Popup Demo")]
        public void Build()
        {
            EnsureEventSystem();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            var probe = FindFirstObjectByType<AreaPresenceProbe>();
            if (probe == null && createEditorFlyProbe)
                probe = CreateEditorProbe();

            var zonesRoot = new GameObject("Area Zones").transform;
            zonesRoot.SetParent(transform, false);

            var popupsRoot = new GameObject("Area Popups").transform;
            popupsRoot.SetParent(transform, false);

            var definitions = new[]
            {
                new AreaDef(StudioAreaId.MixingBoard, "Mixing Board", "Move into this zone to open the mixing board hint. Later this can launch the mixer UI.", new Vector3(-zoneSpacing, 0f, 0f)),
                new AreaDef(StudioAreaId.MicBooth, "Mic Booth", "Vocal / spoken-word capture area. Popup appears when you enter the booth volume.", new Vector3(0f, 0f, zoneSpacing)),
                new AreaDef(StudioAreaId.Instruments, "Instruments", "Instrument tracking area. Use this popup for gear tips or next steps.", new Vector3(zoneSpacing, 0f, 0f)),
                new AreaDef(StudioAreaId.EffectsRack, "Effects Rack", "Processing / FX area. Trigger boxes drive when this hint is shown.", new Vector3(0f, 0f, -zoneSpacing))
            };

            var zones = new List<AreaTriggerZone>();
            var panels = new List<AreaPopupPanel>();

            for (int i = 0; i < definitions.Length; i++)
            {
                var def = definitions[i];
                var zoneGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                zoneGo.name = def.Id + " Zone";
                zoneGo.transform.SetParent(zonesRoot, false);
                zoneGo.transform.position = def.Position + Vector3.up * 1.25f;
                zoneGo.transform.localScale = new Vector3(2.2f, 2.5f, 2.2f);

                // Visual volume (semi-transparent); collider must be trigger.
                var renderer = zoneGo.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = CreateVolumeMaterial();
                }

                var box = zoneGo.GetComponent<BoxCollider>();
                box.isTrigger = true;

                // Remove default physics material interaction noise.
                var rb = zoneGo.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    if (Application.isPlaying) Destroy(rb);
                    else DestroyImmediate(rb);
                }

                var zone = zoneGo.AddComponent<AreaTriggerZone>();
                zone.Configure(def.Id, def.Title, def.Hint, Vector3.one);

                var popup = AreaPopupUIFactory.CreateWorldPopup(popupsRoot, def.Id, def.Position + new Vector3(0f, 1.8f, 0f));
                popup.ApplyContent(def.Title, def.Hint);

                zones.Add(zone);
                panels.Add(popup);
            }

            var directorGo = new GameObject("Area Popup Director");
            directorGo.transform.SetParent(transform, false);
            Director = directorGo.AddComponent<AreaPopupDirector>();
            Director.SetZones(zones);
            Director.SetPanels(panels);

            // Director OnEnable already ran before zones were assigned — rebind by bounce.
            Director.enabled = false;
            Director.enabled = true;

            if (probe != null)
                Debug.Log("Area popups ready. Walk/fly the presence probe into a translucent zone to show its popup.", this);
            else
                Debug.LogWarning("No AreaPresenceProbe found. Add one to your XR Origin or Main Camera.", this);
        }

        private AreaPresenceProbe CreateEditorProbe()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camGo.tag = "MainCamera";
                camGo.transform.position = new Vector3(0f, 1.6f, -4f);
                camera = camGo.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f, 0.08f, 0.10f, 1f);
            }

            var probe = camera.GetComponent<AreaPresenceProbe>();
            if (probe == null) probe = camera.gameObject.AddComponent<AreaPresenceProbe>();
            probe.EnsurePhysicsBody();

            if (camera.GetComponent<EditorFlyController>() == null)
                camera.gameObject.AddComponent<EditorFlyController>();

            return probe;
        }

        private static Material CreateVolumeMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            var color = new Color(0.28f, 0.55f, 0.72f, 0.18f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent if supported
            return mat;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        private readonly struct AreaDef
        {
            public readonly StudioAreaId Id;
            public readonly string Title;
            public readonly string Hint;
            public readonly Vector3 Position;

            public AreaDef(StudioAreaId id, string title, string hint, Vector3 position)
            {
                Id = id;
                Title = title;
                Hint = hint;
                Position = position;
            }
        }
    }
}
