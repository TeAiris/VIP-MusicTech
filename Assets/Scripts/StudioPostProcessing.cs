using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Enables the studio volume on scene cameras, including rigs spawned at runtime.</summary>
[DisallowMultipleComponent]
public sealed class StudioPostProcessing : MonoBehaviour
{
    private struct CameraState
    {
        public Camera camera;
        public UniversalAdditionalCameraData data;
        public bool postProcessing;
        public bool hdr;
        public LayerMask volumeMask;
    }

    private Camera[] cameras = new Camera[8];
    private readonly Dictionary<Camera, CameraState> configured = new Dictionary<Camera, CameraState>();

    private void LateUpdate()
    {
        if (cameras.Length < Camera.allCamerasCount)
            cameras = new Camera[Camera.allCamerasCount];

        int count = Camera.GetAllCameras(cameras);
        for (int i = 0; i < count; i++)
        {
            Camera camera = cameras[i];
            if (camera.gameObject.scene != gameObject.scene || camera.cameraType != CameraType.Game
                || configured.ContainsKey(camera))
                continue;

            var data = camera.GetUniversalAdditionalCameraData();
            // Overlay cameras inherit the base camera's grading; avoid applying it twice.
            if (data.renderType == CameraRenderType.Overlay)
                continue;

            configured.Add(camera, new CameraState
            {
                camera = camera,
                data = data,
                postProcessing = data.renderPostProcessing,
                hdr = camera.allowHDR,
                volumeMask = data.volumeLayerMask
            });
            camera.allowHDR = true;
            data.volumeLayerMask |= 1 << gameObject.layer;
            data.renderPostProcessing = true;
        }
    }

    private void OnDisable()
    {
        foreach (var state in configured.Values)
        {
            if (state.camera == null || state.data == null)
                continue;
            state.camera.allowHDR = state.hdr;
            state.data.volumeLayerMask = state.volumeMask;
            state.data.renderPostProcessing = state.postProcessing;
        }
        configured.Clear();
    }
}
