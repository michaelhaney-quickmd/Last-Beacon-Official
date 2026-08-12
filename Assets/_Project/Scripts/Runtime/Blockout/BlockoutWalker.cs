using UnityEngine;
using UnityEngine.InputSystem;

namespace LastBeacon.Blockout
{
    /// <summary>
    /// PLACEHOLDER — delete in Phase 2 when the real PlayerController lands.
    ///
    /// Exists only so the Phase 1 compound blockout can be walked in first person
    /// to judge scale, sightlines and traversal times (workflow doc, Phase 1 step 5).
    /// Deliberately dumb: no networking, no interaction, no crouch, no head bob.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class BlockoutWalker : MonoBehaviour
    {
        [Header("Traversal reference (GDD Section 7)")]
        [Tooltip("Compound should be crossable in 8-15 seconds at this speed.")]
        [SerializeField] float walkSpeed = 4.5f;
        [SerializeField] float sprintSpeed = 7f;
        [SerializeField] float lookSensitivity = 0.12f;
        [SerializeField] float gravity = -20f;

        [Header("Readout")]
        [Tooltip("Shows a small on-screen distance/time readout for judging the layout.")]
        [SerializeField] bool showTraversalReadout = true;

        CharacterController _controller;
        Transform _camera;
        float _pitch;
        float _verticalVelocity;
        Vector3 _lastPosition;
        float _distanceTravelled;
        float _movingTime;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _camera = GetComponentInChildren<Camera>().transform;
            _lastPosition = transform.position;
        }

        void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                    ? CursorLockMode.None
                    : CursorLockMode.Locked;
                Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
            }

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
                transform.Rotate(Vector3.up, delta.x);
                _pitch = Mathf.Clamp(_pitch - delta.y, -89f, 89f);
                _camera.localEulerAngles = new Vector3(_pitch, 0f, 0f);
            }

            Vector3 input = Vector3.zero;
            if (keyboard.wKey.isPressed) input.z += 1f;
            if (keyboard.sKey.isPressed) input.z -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;

            float speed = keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
            Vector3 move = transform.TransformDirection(Vector3.ClampMagnitude(input, 1f)) * speed;

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;
            move.y = _verticalVelocity;

            _controller.Move(move * Time.deltaTime);

            Vector3 flat = transform.position - _lastPosition;
            flat.y = 0f;
            if (flat.magnitude > 0.001f)
            {
                _distanceTravelled += flat.magnitude;
                _movingTime += Time.deltaTime;
            }
            _lastPosition = transform.position;

            if (keyboard.rKey.wasPressedThisFrame)
            {
                _distanceTravelled = 0f;
                _movingTime = 0f;
            }
        }

        void OnGUI()
        {
            if (!showTraversalReadout)
                return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            GUI.Label(new Rect(12, 10, 500, 22),
                $"Travelled {_distanceTravelled:0.0} m in {_movingTime:0.0} s   (R resets, Shift sprints, Esc frees cursor)",
                style);
            GUI.Label(new Rect(12, 30, 500, 22),
                $"Position  x {transform.position.x:0.0}   z {transform.position.z:0.0}", style);
        }
    }
}
