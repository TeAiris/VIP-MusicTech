using UnityEngine;

namespace VIP.MusicTech.AreaPopups
{
    /// <summary>
    /// Put this on the XR Origin / player rig (or Main Camera while testing in the editor).
    /// Trigger zones look for this component, or for a collider tagged Player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AreaPresenceProbe : MonoBehaviour
    {
        [SerializeField, Tooltip("If set, proximity checks use this transform instead of this object.")]
        private Transform trackingPoint;

        public Transform TrackingPoint => trackingPoint != null ? trackingPoint : transform;

        private void Reset()
        {
            trackingPoint = transform;
            EnsurePhysicsBody();
        }

        [ContextMenu("Ensure Physics Body For Triggers")]
        public void EnsurePhysicsBody()
        {
            var col = GetComponent<Collider>();
            if (col == null)
            {
                var capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.height = 1.6f;
                capsule.radius = 0.2f;
                capsule.center = new Vector3(0f, 0.8f, 0f);
                capsule.isTrigger = false;
            }

            var body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }
}
