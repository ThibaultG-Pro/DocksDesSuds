using UnityEngine;

namespace DocksDesSuds.GameModes
{
    /// <summary>
    /// A walkable area: a teleport floor + invisible boundary walls (children).
    /// The hub zone stays active; activity zones are only active while their mode runs.
    /// </summary>
    public class PlayZone : MonoBehaviour
    {
        [SerializeField] bool activeOnStart;
        [Tooltip("Children enabled/disabled with the zone (teleport floor, boundary walls...).")]
        [SerializeField] GameObject[] zoneObjects;
        [SerializeField] Vector3 gizmoSize = new Vector3(10f, 3f, 10f);

        public bool IsActive { get; private set; }

        void Awake() => SetZoneActive(activeOnStart);

        public void SetZoneActive(bool active)
        {
            IsActive = active;
            if (zoneObjects == null) return;
            foreach (var go in zoneObjects)
                if (go) go.SetActive(active);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * gizmoSize.y * 0.5f, gizmoSize);
        }
    }
}
