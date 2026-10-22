using UnityEngine;
using UnityEngine.InputSystem;

// A capsule with a camera: WASD to move, hold the right mouse button and move the mouse to look.
// Esc stops the game. The cursor is only locked while the right button is held, so it can never get stuck.
[RequireComponent(typeof(CharacterController))]
public class SimplePlayer : MonoBehaviour
{
    public float speed = 8f;
    public float lookSpeed = 0.1f;

    CharacterController controller;
    Transform cam;
    float pitch;
    float fall;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>().transform;
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            Stop();
            return;
        }

        // Ignore every input while the window is not focused, so a missed key release cannot keep the capsule moving.
        bool active = Application.isFocused;

        bool looking = active && mouse.rightButton.isPressed;
        Cursor.lockState = looking ? CursorLockMode.Locked : CursorLockMode.None;
        if (looking)
        {
            Vector2 look = mouse.delta.ReadValue() * lookSpeed;
            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -89f, 89f);
            cam.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        Vector3 move = Vector3.zero;
        if (active)
        {
            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            move = transform.right * x + transform.forward * z;
            if (move.sqrMagnitude > 1f) move.Normalize();
        }

        fall = controller.isGrounded ? -2f : fall - 20f * Time.deltaTime;
        controller.Move((move * speed + Vector3.up * fall) * Time.deltaTime);
    }

    static void Stop()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
