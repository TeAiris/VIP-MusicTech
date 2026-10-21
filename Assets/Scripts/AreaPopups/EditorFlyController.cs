using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VIP.MusicTech.AreaPopups
{
    /// <summary>
    /// Simple editor/simulator movement so trigger popups can be tested without a headset.
    /// WASD + mouse look, Q/E up/down. Disable this when using a real XR rig.
    /// </summary>
    public sealed class EditorFlyController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float lookSensitivity = 0.15f;
        [SerializeField] private bool requireRightMouseForLook = true;

        private float yaw;
        private float pitch;

        private void Start()
        {
            var eulers = transform.eulerAngles;
            yaw = eulers.y;
            pitch = eulers.x;
        }

        private void Update()
        {
            Vector2 look = ReadLookDelta();
            bool looking = !requireRightMouseForLook || IsRightMouseHeld();
            if (looking)
            {
                yaw += look.x * lookSensitivity;
                pitch -= look.y * lookSensitivity;
                pitch = Mathf.Clamp(pitch, -85f, 85f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            Vector3 input = ReadMoveInput();
            Vector3 world = transform.right * input.x + transform.forward * input.z + Vector3.up * input.y;
            transform.position += world * (moveSpeed * Time.deltaTime);
        }

        private static Vector3 ReadMoveInput()
        {
            float x = 0f, y = 0f, z = 0f;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) z += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) z -= 1f;
                if (kb.eKey.isPressed) y += 1f;
                if (kb.qKey.isPressed) y -= 1f;
            }
#else
            x = Input.GetAxisRaw("Horizontal");
            z = Input.GetAxisRaw("Vertical");
            if (Input.GetKey(KeyCode.E)) y += 1f;
            if (Input.GetKey(KeyCode.Q)) y -= 1f;
#endif
            return new Vector3(x, y, z).normalized;
        }

        private static Vector2 ReadLookDelta()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
#else
            return new Vector2(Input.GetAxisRaw("Mouse X") * 10f, Input.GetAxisRaw("Mouse Y") * 10f);
#endif
        }

        private static bool IsRightMouseHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
#else
            return Input.GetMouseButton(1);
#endif
        }
    }
}
