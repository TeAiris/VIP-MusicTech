using UnityEngine;

/// <summary>Plays a sound when a collider lands on this object from above.</summary>
[RequireComponent(typeof(Collider))]
public class TopCollisionAudio : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Range(0f, 1f)] private float minimumTopAngle = 0.5f;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (clip == null)
            return;

        // Contact normals point out from this object's surface. A positive dot
        // with transform.up means the contact is on its top-facing side.
        foreach (ContactPoint contact in collision.contacts)
        {
            if (Vector3.Dot(transform.up, contact.normal) >= minimumTopAngle)
            {
                if (audioSource != null)
                    audioSource.PlayOneShot(clip);
                else
                    AudioSource.PlayClipAtPoint(clip, contact.point);

                return;
            }
        }
    }
}
