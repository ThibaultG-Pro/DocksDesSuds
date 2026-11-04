using UnityEngine;
using UnityEngine.InputSystem;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// Owns the currently running game mode, teleports the player between the hub and the
    /// activity zones, and exposes the player rig references modes need.
    /// Only one mode can run at a time.
    /// </summary>
    public class GameModeManager : MonoBehaviour
    {
        public static GameModeManager Instance { get; private set; }

        [Header("Player rig")]
        [SerializeField] Transform xrOrigin;
        [SerializeField] CharacterController characterController;
        [SerializeField] Transform head;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;
        [Tooltip("Objects hidden on the left hand while a mode equips something there.")]
        [SerializeField] GameObject[] leftHandDefaultObjects;
        [Tooltip("Objects hidden on the right hand while a mode equips something there.")]
        [SerializeField] GameObject[] rightHandDefaultObjects;

        [Header("Hub")]
        [Tooltip("Where the player comes back to when a mode ends.")]
        [SerializeField] Transform hubSpawn;

        [Header("Quit")]
        [Tooltip("Hold this (left controller menu button) to quit the running mode.")]
        [SerializeField] float quitHoldSeconds = 1.5f;

        [Header("Debug (Editor only): F1, F2... toggle these modes")]
        [SerializeField] GameModeBase[] debugModes;

        public Transform Head => head ? head : (Camera.main ? Camera.main.transform : transform);
        public Transform LeftHand => leftHand;
        public Transform RightHand => rightHand;
        public GameModeBase Current { get; private set; }

        public event System.Action<GameModeBase> ModeStarted;
        public event System.Action<GameModeBase> ModeEnded;

        InputAction _quitAction;
        float _quitHeld;

        void Awake()
        {
            Instance = this;
            _quitAction = new InputAction("QuitMode", InputActionType.Button);
            _quitAction.AddBinding("<XRController>{LeftHand}/menuButton");
            _quitAction.AddBinding("<Keyboard>/escape");
        }

        void OnEnable() => _quitAction.Enable();
        void OnDisable() => _quitAction.Disable();

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _quitAction.Dispose();
        }

        void Update()
        {
            if (Current != null && _quitAction.IsPressed())
            {
                _quitHeld += Time.unscaledDeltaTime;
                if (_quitHeld >= quitHoldSeconds) StopCurrent();
            }
            else _quitHeld = 0f;

#if UNITY_EDITOR
            if (debugModes != null && Keyboard.current != null)
                for (int i = 0; i < debugModes.Length && i < 12; i++)
                    if (debugModes[i] && Keyboard.current[Key.F1 + i].wasPressedThisFrame)
                        Toggle(debugModes[i]);
#endif
        }

        public void Toggle(GameModeBase mode)
        {
            if (Current == mode) StopCurrent();
            else StartMode(mode);
        }

        public void StartMode(GameModeBase mode)
        {
            if (mode == null) return;
            StopCurrent();
            Current = mode;
            mode.Ended += OnModeEnded;
            if (mode.Zone) mode.Zone.SetZoneActive(true);
            if (mode.ArrivalPoint) TeleportPlayer(mode.ArrivalPoint);
            mode.Begin(this);
            ModeStarted?.Invoke(mode);
        }

        public void StopCurrent()
        {
            if (Current != null) Current.End();
        }

        void OnModeEnded(GameModeBase mode)
        {
            mode.Ended -= OnModeEnded;
            if (Current == mode) Current = null;
            SetHandDefaultsVisible(true, true);
            SetHandDefaultsVisible(false, true);
            if (mode.Zone) mode.Zone.SetZoneActive(false);
            if (mode.ArrivalPoint && hubSpawn) TeleportPlayer(hubSpawn);
            ModeEnded?.Invoke(mode);
        }

        public void SetHandDefaultsVisible(bool left, bool visible)
        {
            var objects = left ? leftHandDefaultObjects : rightHandDefaultObjects;
            if (objects == null) return;
            foreach (var go in objects)
                if (go) go.SetActive(visible);
        }

        /// <summary>
        /// Moves the rig so the player's head ends up above <paramref name="target"/> and faces its forward.
        /// Works whatever the player's physical position in their play space.
        /// </summary>
        public void TeleportPlayer(Transform target)
        {
            if (!xrOrigin || !target) return;
            if (characterController) characterController.enabled = false;

            Transform cam = Head;
            float targetYaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(target.forward, Vector3.up)).eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(cam.eulerAngles.y, targetYaw);
            xrOrigin.RotateAround(cam.position, Vector3.up, deltaYaw);

            Vector3 camOnFloor = new Vector3(cam.position.x, xrOrigin.position.y, cam.position.z);
            xrOrigin.position += target.position - camOnFloor;

            if (characterController) characterController.enabled = true;
            ShooterSfx.Play(ShooterSfx.Teleport, target.position + Vector3.up * 1.5f, 0.6f);
        }
    }
}
