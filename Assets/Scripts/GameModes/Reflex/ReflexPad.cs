using UnityEngine;

namespace DocksDesSuds.GameModes
{
    /// <summary>One touch pad of Reflex Rush. Visual state is driven by <see cref="ReflexMode"/>.</summary>
    public class ReflexPad : MonoBehaviour
    {
        public enum State { Idle, Target, Trap }

        public State Current { get; private set; } = State.Idle;
        public float Remaining01 => _duration > 0f ? Mathf.Clamp01(_timeLeft / _duration) : 0f;

        Renderer _face;
        Transform _timerRing;
        MaterialPropertyBlock _mpb;
        float _timeLeft, _duration, _flash;
        Color _flashColor;
        float _cooldown;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        static readonly Color IdleCol = new Color(0.15f, 0.35f, 0.8f);
        static readonly Color TargetCol = new Color(0.1f, 1f, 0.35f);
        static readonly Color TrapCol = new Color(1f, 0.1f, 0.1f);

        public static ReflexPad Create(Transform parent, Material material, Vector3 position, Vector3 facing, float diameter)
        {
            var root = new GameObject("Pad");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.LookRotation(facing) * Quaternion.Euler(90f, 0f, 0f);

            var face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            face.name = "Face";
            Object.Destroy(face.GetComponent<Collider>());
            face.transform.SetParent(root.transform, false);
            face.transform.localScale = new Vector3(diameter, 0.012f, diameter);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Timer";
            Object.Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.008f, 0f);
            ring.transform.localScale = new Vector3(diameter * 0.9f, 0.006f, diameter * 0.9f);

            var pad = root.AddComponent<ReflexPad>();
            pad._face = face.GetComponent<Renderer>();
            pad._face.sharedMaterial = material;
            var ringR = ring.GetComponent<Renderer>();
            ringR.sharedMaterial = material;
            pad._timerRing = ring.transform;
            pad._mpb = new MaterialPropertyBlock();
            foreach (var r in new[] { pad._face, ringR }) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pad.SetColor(ringR, Color.white * 0.0f, Color.black);
            pad.Apply();
            return pad;
        }

        public bool IsReady => Current == State.Idle && _cooldown <= 0f;

        public void Activate(State state, float duration)
        {
            Current = state;
            _duration = _timeLeft = duration;
        }

        public void Clear(Color flash)
        {
            Current = State.Idle;
            _flash = 1f;
            _flashColor = flash;
            _cooldown = 0.35f;
        }

        /// <summary>Returns true when an active pad just ran out of time.</summary>
        public bool Tick(float dt)
        {
            _cooldown -= dt;
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 4f);
            bool expired = false;
            if (Current != State.Idle)
            {
                _timeLeft -= dt;
                if (_timeLeft <= 0f) expired = true;
            }
            Apply();
            return expired;
        }

        void Apply()
        {
            Color c = Current switch
            {
                State.Target => TargetCol,
                State.Trap => TrapCol,
                _ => IdleCol,
            };
            float pulse = Current == State.Idle ? 0.35f : 2f + 0.8f * Mathf.Sin(Time.time * 18f);
            Color emission = Color.Lerp(c * pulse, _flashColor * 4f, _flash);
            SetColor(_face, c * 0.7f, emission);

            if (_timerRing)
            {
                float s = Current == State.Idle ? 0f : Remaining01;
                // White disc in the middle of the pad, shrinking as time runs out.
                _timerRing.localScale = new Vector3(s * _face.transform.localScale.x * 0.5f, 0.006f, s * _face.transform.localScale.z * 0.5f);
                _timerRing.gameObject.SetActive(s > 0f);
                SetColor(_timerRing.GetComponent<Renderer>(), Color.white, Color.white * 1.2f);
            }
        }

        void SetColor(Renderer r, Color baseCol, Color emission)
        {
            if (!r) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, baseCol);
            _mpb.SetColor(EmissionId, emission);
            r.SetPropertyBlock(_mpb);
        }
    }
}
