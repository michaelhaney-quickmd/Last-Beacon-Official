using UnityEngine;
using UnityEngine.InputSystem;

namespace LastBeacon
{
    /// <summary>
    /// Minimal scripted double door for the generator shed visual test.
    /// Each leaf is rotated about its own imported hinge pivot on a single local
    /// axis; closed is the identity rotation. Deliberately local-only — no
    /// networking, no damage states — until the door is promoted to a real
    /// gameplay system (GDD sign-off required for that).
    /// </summary>
    public class ScriptedDoubleDoor : MonoBehaviour
    {
        [Header("Leaves (children with hinge-edge pivots)")]
        public Transform leftLeaf;
        public Transform rightLeaf;

        [Header("Angles")]
        [Tooltip("Closed pose. Must stay 0 — the leaves are authored closed at identity.")]
        public float closedAngle = 0f;
        [Tooltip("Target open angle. The art is verified collision-free to 150 degrees.")]
        public float openAngle = 95f;
        [Tooltip("Leaves swing in opposite directions.")]
        public bool leftOpensPositive = true;

        [Header("Motion")]
        [Tooltip("Degrees per second. Interpolated, never teleported.")]
        public float degreesPerSecond = 110f;

        public enum Axis { X, Y, Z }
        [Header("Hinge axis")]
        [Tooltip("Local axis each leaf turns about. The Blender source hinges on local Z, " +
                 "and the FBX import preserves that — the leaf's local Z is world up, not Y. " +
                 "Verified by measurement: turning about Z keeps the leaf level (dY = 0) while " +
                 "X and Y tumble it.")]
        public Axis hingeAxis = Axis.Z;

        // ---------------------------------------------------------------- SCAFFOLDING
        // TEMPORARY. There is no interaction system in the project yet — no
        // Interactable, no player interaction probe, no prompt UI — so this stands in
        // so the door can be tested in play mode. Delete this whole region, and the
        // temporaryKeyboardTest field, once Interactable (GDD Section 40) exists.
        [Header("TEMPORARY test hook — remove when Interactable lands")]
        [Tooltip("Press Interact (E / gamepad North) within range to toggle the doors. " +
                 "Stand-in for the real interaction system.")]
        public bool temporaryKeyboardTest = true;
        [Tooltip("How close the player must be, in metres, measured to this object.")]
        public float testRange = 4.5f;
        [Tooltip("Draws a small on-screen hint while in range, so it is obvious the hook is live.")]
        public bool showTestPrompt = true;

        InputAction interactAction;
        Transform playerT;
        bool inRange;
        // -------------------------------------------------------------- END SCAFFOLDING

        [SerializeField] bool isOpen;
        float current;   // current angle magnitude

        void OnEnable()
        {
            current = isOpen ? openAngle : closedAngle;
            Apply();
            if (temporaryKeyboardTest)
            {
                // The project runs Input System only (activeInputHandler = 1), so the
                // legacy Input class is unavailable. Bindings mirror the Interact
                // action in InputSystem_Actions: <Keyboard>/e and <Gamepad>/buttonNorth.
                interactAction = new InputAction("TempInteract", InputActionType.Button);
                interactAction.AddBinding("<Keyboard>/e");
                interactAction.AddBinding("<Gamepad>/buttonNorth");
                interactAction.Enable();
            }
        }

        void OnDisable() { interactAction?.Disable(); interactAction?.Dispose(); interactAction = null; }

        void Update()
        {
            if (temporaryKeyboardTest) TemporaryInput();

            float target = isOpen ? openAngle : closedAngle;
            if (!Mathf.Approximately(current, target))
            {
                current = Mathf.MoveTowards(current, target, degreesPerSecond * Time.deltaTime);
                Apply();
            }
        }

        void TemporaryInput()
        {
            if (playerT == null)
            {
                var cc = FindFirstObjectByType<CharacterController>();
                if (cc != null) playerT = cc.transform;
                else if (Camera.main != null) playerT = Camera.main.transform;
            }
            inRange = playerT != null &&
                      Vector3.Distance(playerT.position, transform.position) <= testRange;
            if (inRange && interactAction != null && interactAction.WasPressedThisFrame())
                Toggle();
        }

        void OnGUI()
        {
            if (!temporaryKeyboardTest || !showTestPrompt || !inRange) return;
            var style = new GUIStyle(GUI.skin.label)
            { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.62f, Screen.width, 24),
                      isOpen ? "[E]  Close doors  (temporary test hook)"
                             : "[E]  Open doors  (temporary test hook)", style);
        }

        /// <summary>Open or close. Motion is interpolated in Update.</summary>
        public void SetOpen(bool open) => isOpen = open;
        public void Toggle() => isOpen = !isOpen;
        public bool IsOpen => isOpen;
        /// <summary>True once the leaves have finished travelling.</summary>
        public bool IsSettled => Mathf.Approximately(current, isOpen ? openAngle : closedAngle);

        Quaternion Turn(float degrees) => hingeAxis switch
        {
            Axis.X => Quaternion.Euler(degrees, 0f, 0f),
            Axis.Y => Quaternion.Euler(0f, degrees, 0f),
            _      => Quaternion.Euler(0f, 0f, degrees),
        };

        void Apply()
        {
            float s = leftOpensPositive ? 1f : -1f;
            if (leftLeaf != null) leftLeaf.localRotation = Turn(current * s);
            if (rightLeaf != null) rightLeaf.localRotation = Turn(current * -s);
        }

        /// <summary>Editor/test helper: pose the leaves without entering play mode.</summary>
        public void PoseImmediate(float angle)
        {
            current = angle;
            Apply();
        }
    }
}
