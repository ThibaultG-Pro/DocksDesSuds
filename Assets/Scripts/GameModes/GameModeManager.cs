using UnityEngine;
using UnityEngine.InputSystem;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// Owns the currently running game mode and exposes the player rig references modes need.
    /// Only one mode can run at a time.
    /// </summary>
    public class GameModeManager : MonoBehaviour
    {
        public static GameModeManager Instance { get; private set; }

        [Header("Player rig")]
        [SerializeField] Transform head;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;
        [Tooltip("Objects hidden on the right hand while a mode equips something there (controller model, near-far ray...).")]
        [SerializeField] GameObject[] rightHandDefaultObjects;

        [Header("Quit")]
        [Tooltip("Hold this (left controller menu button) to quit the running mode.")]
        [SerializeField] float quitHoldSeconds = 1.5f;

        [Header("Debug (Editor only)")]
        [SerializeField] GameModeBase debugMode;
        [SerializeField] Key debugToggleKey = Key.F1;

        public Transform Head => head ? head : (Camera.main ? Camera.main.transform : transform);
        public Transform LeftHand => leftHand;
        public Transform RightHand => rightHand;
        public GameModeBase Current { get; private set; }

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
            if (debugMode != null && Keyboard.current != null && Keyboard.current[debugToggleKey].wasPressedThisFrame)
                Toggle(debugMode);
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
            mode.Begin(this);
        }

        public void StopCurrent()
        {
            if (Current != null) Current.End();
        }

        void OnModeEnded(GameModeBase mode)
        {
            mode.Ended -= OnModeEnded;
            if (Current == mode) Current = null;
            SetRightHandDefaultsVisible(true);
        }

        public void SetRightHandDefaultsVisible(bool visible)
        {
            if (rightHandDefaultObjects == null) return;
            foreach (var go in rightHandDefaultObjects)
                if (go) go.SetActive(visible);
        }
    }
}
