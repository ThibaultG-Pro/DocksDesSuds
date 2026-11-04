using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// Big arcade button that starts (or stops) a game mode.
    /// Press it by slapping it with a controller, or point the ray at it and squeeze grip.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class ModeButton : MonoBehaviour
    {
        [SerializeField] GameModeBase mode;
        [SerializeField] Transform cap;
        [SerializeField] Renderer capRenderer;
        [Tooltip("Label that turns to face the player (yaw only).")]
        [SerializeField] Transform labelRoot;
        [SerializeField] TextMeshPro hintText;

        [Header("Look")]
        [SerializeField] Color idleGlow = new Color(1f, 0.15f, 0.05f);
        [SerializeField] Color hoverGlow = new Color(1f, 0.6f, 0.2f);
        [SerializeField] Color activeGlow = new Color(0.1f, 1f, 0.3f);
        [SerializeField] float pressDepth = 0.04f;

        [Header("Press")]
        [Tooltip("A controller this close to the cap top presses the button.")]
        [SerializeField] float touchRadius = 0.18f;
        [SerializeField] float cooldown = 1.5f;

        XRSimpleInteractable _interactable;
        MaterialPropertyBlock _mpb;
        Vector3 _capRest;
        float _pressT;
        float _nextAllowed;
        bool _hovered;
        bool _handInside;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            if (cap) _capRest = cap.localPosition;
        }

        void OnEnable()
        {
            _interactable.selectEntered.AddListener(OnSelect);
            _interactable.hoverEntered.AddListener(OnHoverEnter);
            _interactable.hoverExited.AddListener(OnHoverExit);
        }

        void OnDisable()
        {
            _interactable.selectEntered.RemoveListener(OnSelect);
            _interactable.hoverEntered.RemoveListener(OnHoverEnter);
            _interactable.hoverExited.RemoveListener(OnHoverExit);
        }

        void OnSelect(SelectEnterEventArgs _) => Press();
        void OnHoverEnter(HoverEnterEventArgs _) => _hovered = true;
        void OnHoverExit(HoverExitEventArgs _) => _hovered = false;

        public void Press()
        {
            if (Time.time < _nextAllowed || GameModeManager.Instance == null) return;
            _nextAllowed = Time.time + cooldown;
            _pressT = 1f;
            ShooterSfx.Play(ShooterSfx.Button, transform.position + Vector3.up, 0.8f);
            GameModeManager.Instance.Toggle(mode);
        }

        void Update()
        {
            CheckHandTouch();

            _pressT = Mathf.MoveTowards(_pressT, 0f, Time.deltaTime * 3f);
            if (cap) cap.localPosition = _capRest - Vector3.up * (pressDepth * _pressT);

            if (capRenderer)
            {
                _mpb ??= new MaterialPropertyBlock();
                bool active = mode != null && mode.IsRunning;
                Color baseCol = active ? activeGlow : (_hovered ? hoverGlow : idleGlow);
                float pulse = active || _hovered ? 1.2f : 0.7f + 0.3f * Mathf.Sin(Time.time * 3f);
                capRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, baseCol * pulse * 2f);
                capRenderer.SetPropertyBlock(_mpb);
            }

            var gm = GameModeManager.Instance;
            if (labelRoot && gm)
            {
                Vector3 toHead = gm.Head.position - labelRoot.position;
                toHead.y = 0f;
                if (toHead.sqrMagnitude > 0.01f) labelRoot.rotation = Quaternion.LookRotation(-toHead);
            }
            if (hintText) hintText.text = mode != null && mode.IsRunning ? "EN COURS" : "TAPE LE BOUTON !";
        }

        void CheckHandTouch()
        {
            var gm = GameModeManager.Instance;
            if (!gm || !cap) return;
            bool inside = IsNear(gm.LeftHand) || IsNear(gm.RightHand);
            if (inside && !_handInside) Press();
            _handInside = inside;
        }

        bool IsNear(Transform hand) =>
            hand && hand.gameObject.activeInHierarchy && Vector3.Distance(hand.position, cap.position) < touchRadius;
    }
}
