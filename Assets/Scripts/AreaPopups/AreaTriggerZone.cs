using UnityEngine;
using UnityEngine.Events;

namespace VIP.MusicTech.AreaPopups
{
    /// <summary>
    /// Trigger box for a studio area. Fires enter/exit when the presence probe
    /// (or a Player-tagged collider) moves into the volume.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class AreaTriggerZone : MonoBehaviour
    {
        [SerializeField] private StudioAreaId areaId = StudioAreaId.MixingBoard;
        [SerializeField] private string areaTitle = "Mixing Board";
        [SerializeField, TextArea] private string areaHint = "Adjust levels, mute, solo, and transport.";
        [SerializeField, Tooltip("Optional: also treat the player as inside when within this radius of the zone center.")]
        private bool useProximityFallback;
        [SerializeField, Min(0.1f)] private float proximityRadius = 1.5f;
        [SerializeField] private Transform proximityTarget;

        public UnityEvent<AreaTriggerZone> PlayerEntered = new UnityEvent<AreaTriggerZone>();
        public UnityEvent<AreaTriggerZone> PlayerExited = new UnityEvent<AreaTriggerZone>();

        public StudioAreaId AreaId => areaId;
        public string AreaTitle => areaTitle;
        public string AreaHint => areaHint;
        public bool PlayerInside { get; private set; }

        private Collider zoneCollider;
        private bool proximityInside;

        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();
            zoneCollider.isTrigger = true;
        }

        private void Reset()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2f, 2.5f, 2f);
            box.center = new Vector3(0f, 1.25f, 0f);
        }

        private void Update()
        {
            if (!useProximityFallback) return;
            var probe = ResolveProbe();
            if (probe == null) return;

            float radius = proximityRadius;
            Vector3 center = transform.position;
            if (zoneCollider != null) center = zoneCollider.bounds.center;

            bool inside = Vector3.Distance(probe.TrackingPoint.position, center) <= radius;
            if (inside == proximityInside) return;
            proximityInside = inside;
            SetPlayerInside(inside);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            SetPlayerInside(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            if (useProximityFallback && proximityInside) return;
            SetPlayerInside(false);
        }

        public void Configure(StudioAreaId id, string title, string hint, Vector3 localColliderSize)
        {
            areaId = id;
            areaTitle = title;
            areaHint = hint;
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = localColliderSize;
            box.center = Vector3.zero;
        }

        private void SetPlayerInside(bool inside)
        {
            if (PlayerInside == inside) return;
            PlayerInside = inside;
            if (inside) PlayerEntered.Invoke(this);
            else PlayerExited.Invoke(this);
        }

        private static bool IsPlayer(Collider other)
        {
            if (other == null) return false;
            if (other.GetComponentInParent<AreaPresenceProbe>() != null) return true;
            return other.CompareTag("Player");
        }

        private AreaPresenceProbe ResolveProbe()
        {
            if (proximityTarget != null)
            {
                var onTarget = proximityTarget.GetComponentInParent<AreaPresenceProbe>();
                if (onTarget != null) return onTarget;
            }

            return FindFirstObjectByType<AreaPresenceProbe>();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = PlayerInside
                ? new Color(0.3f, 0.85f, 0.55f, 0.35f)
                : new Color(0.28f, 0.55f, 0.72f, 0.25f);

            var col = zoneCollider != null ? zoneCollider : GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col != null)
            {
                Gizmos.DrawWireSphere(col.bounds.center, col.bounds.extents.magnitude);
            }

            if (useProximityFallback)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.2f);
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawWireSphere(transform.position, proximityRadius);
            }
        }
#endif
    }
}
