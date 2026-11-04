using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// Small hidden button in the level. Pressing it (poke, grab or ray + grip) toggles a game mode.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class HiddenModeButton : MonoBehaviour
    {
        [SerializeField] GameModeBase mode;
        [SerializeField] Transform cap;
        [SerializeField] Renderer capRenderer;
        [SerializeField] Color idleGlow = new Color(0.6f, 0.05f, 0.05f);
        [SerializeField] Color hoverGlow = new Color(1f, 0.25f, 0.1f);
        [SerializeField] Color activeGlow = new Color(0.1f, 1f, 0.3f);
        [SerializeField] float pressDepth = 0.015f;
        [SerializeField] float cooldown = 1f;

        XRSimpleInteractable _interactable;
        MaterialPropertyBlock _mpb;
        Vector3 _capRest;
        float _pressT;
        float _nextAllowed;
        bool _hovered;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            _interactable = GetComponent<XRSimpleInteractable>();
            _mpb = new MaterialPropertyBlock();
            if (cap) _capRest = cap.localPosition;
        }

        void OnEnable()
        {
            _interactable.selectEntered.AddListener(OnSelect);
            _interactable.hoverEntered.AddListener(_ => _hovered = true);
            _interactable.hoverExited.AddListener(_ => _hovered = false);
        }

        void OnDisable()
        {
            _interactable.selectEntered.RemoveListener(OnSelect);
            _interactable.hoverEntered.RemoveAllListeners();
            _interactable.hoverExited.RemoveAllListeners();
        }

        void OnSelect(SelectEnterEventArgs _) => Press();

        public void Press()
        {
            if (Time.time < _nextAllowed || GameModeManager.Instance == null) return;
            _nextAllowed = Time.time + cooldown;
            _pressT = 1f;
            ShooterSfx.Play(ShooterSfx.Button, transform.position, 0.6f);
            GameModeManager.Instance.Toggle(mode);
        }

        void Update()
        {
            _pressT = Mathf.MoveTowards(_pressT, 0f, Time.deltaTime * 4f);
            if (cap) cap.localPosition = _capRest - Vector3.up * (pressDepth * _pressT);

            if (capRenderer)
            {
                _mpb ??= new MaterialPropertyBlock();
                bool active = mode != null && mode.IsRunning;
                Color baseCol = active ? activeGlow : (_hovered ? hoverGlow : idleGlow);
                float pulse = active || _hovered ? 1f : 0.55f + 0.45f * Mathf.Sin(Time.time * 2.5f);
                capRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, baseCol * pulse * 2f);
                capRenderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
