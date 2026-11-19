using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Desktop fly-through controls for inspecting the studio in Play Mode.</summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class StudioFlyCamera : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float moveSpeed = 2.5f;
    [SerializeField, Min(1f)] private float boostMultiplier = 3f;
    [SerializeField, Min(0.01f)] private float lookSensitivity = 0.12f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private float yaw;
    private float pitch;
    private bool looking;
    private CursorLockMode previousLock;
    private bool previousVisible;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        SyncAngles();
    }

    private void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (!Application.isFocused || mouse == null || keyboard == null)
        {
            ReleaseMouse();
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame)
            ReleaseMouse();
        else if (mouse.rightButton.wasPressedThisFrame)
        {
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            looking = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (!mouse.rightButton.isPressed)
            ReleaseMouse();

        if (keyboard.homeKey.wasPressedThisFrame)
        {
            transform.SetPositionAndRotation(startPosition, startRotation);
            SyncAngles();
        }

        if (!looking)
            return;

        Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
        yaw += delta.x;
        pitch = Mathf.Clamp(pitch - delta.y, -85f, 85f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        float right = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        float up = (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
        Vector3 direction = transform.right * right + transform.forward * forward + Vector3.up * up;
        float speed = moveSpeed * (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed
            ? boostMultiplier : 1f);
        transform.position += Vector3.ClampMagnitude(direction, 1f) * (speed * Time.unscaledDeltaTime);
    }

    private void SyncAngles()
    {
        yaw = transform.eulerAngles.y;
        pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
    }

    private void ReleaseMouse()
    {
        if (!looking)
            return;
        looking = false;
        Cursor.lockState = previousLock;
        Cursor.visible = previousVisible;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            ReleaseMouse();
    }

    private void OnDisable() => ReleaseMouse();
}
