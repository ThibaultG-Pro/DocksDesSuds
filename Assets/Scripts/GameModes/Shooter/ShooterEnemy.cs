using UnityEngine;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// Flying drone that weaves toward the player's head. Flies through walls (like the player).
    /// Touching the player deals damage and destroys the drone.
    /// </summary>
    public class ShooterEnemy : MonoBehaviour
    {
        [SerializeField] int maxHp = 1;
        [SerializeField] float speed = 1.8f;
        [SerializeField] int scoreValue = 10;
        [SerializeField] int contactDamage = 1;
        [SerializeField] float contactDistance = 0.6f;

        [Header("Motion")]
        [SerializeField] float weaveAmplitude = 1.2f;
        [SerializeField] float weaveFrequency = 1.3f;
        [SerializeField] Transform spinner;
        [SerializeField] float spinSpeed = 720f;

        [Header("Visuals")]
        [SerializeField] Renderer[] flashRenderers;
        [SerializeField] Color debrisColor = new Color(1f, 0.3f, 0.1f);
        [SerializeField] float debrisScale = 1f;

        public int ScoreValue => scoreValue;
        public int ContactDamage => contactDamage;

        ShooterMode _mode;
        Transform _target;
        int _hp;
        float _speedMul = 1f;
        float _seed;
        float _flash;
        MaterialPropertyBlock _mpb;
        bool _dead;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public void Init(ShooterMode mode, Transform target, float speedMultiplier)
        {
            _mode = mode;
            _target = target;
            _speedMul = speedMultiplier;
            _hp = maxHp;
            _seed = Random.value * 100f;
            _mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (_target == null || _dead) return;

            Vector3 toTarget = _target.position - transform.position;
            float dist = toTarget.magnitude;
            if (dist < contactDistance)
            {
                _mode?.OnEnemyReachedPlayer(this);
                Die(false);
                return;
            }

            Vector3 dir = toTarget / dist;
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            float t = Time.time * weaveFrequency + _seed;
            // Weave fades out when close so the final approach is readable.
            float weave = weaveAmplitude * Mathf.Clamp01((dist - 1.5f) / 4f);
            Vector3 lateral = right * Mathf.Sin(t) * weave + Vector3.up * Mathf.Cos(t * 1.7f) * weave * 0.4f;

            transform.position += (dir * speed * _speedMul + lateral) * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);

            if (spinner) spinner.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

            if (_flash > 0f)
            {
                _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 8f);
                ApplyFlash(_flash);
            }
        }

        public void TakeDamage(int amount, Vector3 point)
        {
            if (_dead) return;
            _hp -= amount;
            _flash = 1f;
            ApplyFlash(1f);
            ShooterSfx.Play(ShooterSfx.Hit, point, 0.5f, 0.1f);
            if (_hp <= 0)
            {
                _mode?.OnEnemyKilled(this);
                Die(true);
            }
        }

        void Die(bool killed)
        {
            if (_dead) return;
            _dead = true;
            ShooterFx.Debris(transform.position, debrisColor, killed ? 14 : 8, debrisScale, gameObject.layer);
            ShooterSfx.Play(ShooterSfx.Explosion, transform.position, killed ? 0.8f : 0.5f, 0.1f);
            Destroy(gameObject);
        }

        /// <summary>Removes the enemy silently (mode stopped).</summary>
        public void Despawn()
        {
            _dead = true;
            Destroy(gameObject);
        }

        void ApplyFlash(float k)
        {
            if (flashRenderers == null) return;
            foreach (var r in flashRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, Color.white * (k * 3f));
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
