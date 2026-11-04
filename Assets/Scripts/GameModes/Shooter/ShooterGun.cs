using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// Hitscan blaster parented to the right controller. Hold the trigger for automatic fire.
    /// </summary>
    public class ShooterGun : MonoBehaviour
    {
        [SerializeField] Transform muzzle;
        [SerializeField] LineRenderer laserSight;
        [SerializeField] LineRenderer tracer;
        [SerializeField] Light muzzleLight;

        [Header("Shooting")]
        [SerializeField] float fireInterval = 0.16f;
        [SerializeField] float range = 60f;
        [Tooltip("Small sphere cast radius to make aiming in VR more forgiving.")]
        [SerializeField] float aimAssistRadius = 0.08f;
        [SerializeField] int damage = 1;
        [SerializeField] LayerMask hitMask;

        [Header("Input")]
        [SerializeField] string[] fireBindings =
        {
            "<XRController>{RightHand}/triggerPressed",
            "<XRController>{RightHand}/{TriggerButton}",
            "<Mouse>/leftButton",
            "<Keyboard>/space",
        };

        public event Action<bool> Fired; // true = hit an enemy

        InputAction _fire;
        HapticImpulsePlayer _haptics;
        float _nextFire;
        float _tracerT;

        void Awake()
        {
            _fire = new InputAction("Fire", InputActionType.Button);
            foreach (var b in fireBindings) _fire.AddBinding(b);
        }

        void OnEnable()
        {
            _fire.Enable();
            _haptics = GetComponentInParent<HapticImpulsePlayer>();
            if (tracer) tracer.enabled = false;
            if (muzzleLight) muzzleLight.intensity = 0f;
        }

        void OnDisable() => _fire.Disable();
        void OnDestroy() => _fire.Dispose();

        void Update()
        {
            Vector3 origin = muzzle.position, dir = muzzle.forward;
            bool aimingAtEnemy = Physics.SphereCast(origin, aimAssistRadius, dir, out var hit, range, hitMask, QueryTriggerInteraction.Collide);
            Vector3 end = aimingAtEnemy ? hit.point : origin + dir * range;

            if (laserSight)
            {
                laserSight.SetPosition(0, origin);
                laserSight.SetPosition(1, end);
                var c = aimingAtEnemy ? new Color(1f, 0.2f, 0.2f, 1f) : new Color(1f, 0.2f, 0.2f, 0.35f);
                laserSight.startColor = laserSight.endColor = c;
            }

            if (_fire.IsPressed() && Time.time >= _nextFire)
            {
                _nextFire = Time.time + fireInterval;
                Fire(origin, end, aimingAtEnemy ? hit : (RaycastHit?)null);
            }

            _tracerT = Mathf.MoveTowards(_tracerT, 0f, Time.deltaTime * 20f);
            if (tracer) tracer.enabled = _tracerT > 0f;
            if (muzzleLight) muzzleLight.intensity = _tracerT * 3f;
        }

        void Fire(Vector3 origin, Vector3 end, RaycastHit? hit)
        {
            bool hitEnemy = false;
            if (hit.HasValue)
            {
                var enemy = hit.Value.collider.GetComponentInParent<ShooterEnemy>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damage, hit.Value.point);
                    hitEnemy = true;
                }
            }

            if (tracer)
            {
                tracer.SetPosition(0, origin);
                tracer.SetPosition(1, end);
            }
            _tracerT = 1f;

            ShooterSfx.Play(ShooterSfx.Laser, origin, 0.5f, 0.08f);
            if (_haptics) _haptics.SendHapticImpulse(0.35f, 0.04f);
            Fired?.Invoke(hitEnemy);
        }
    }
}
