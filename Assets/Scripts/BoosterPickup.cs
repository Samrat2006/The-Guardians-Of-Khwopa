using UnityEngine;

/// <summary>
/// Adds meter to <see cref="PlayerSuperMeter"/> when the player touches this trigger.
/// </summary>
public class BoosterPickup : MonoBehaviour
{
    [SerializeField] private float meterAmount = 18f;
    [SerializeField] private AudioClip collectSound;
    [SerializeField] [Range(0f, 1f)] private float collectVolume = 0.85f;

    private bool collected;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (!other.CompareTag("Player") && other.GetComponentInParent<PlayerHealth>() == null)
            return;

        GameObject player = other.CompareTag("Player") ? other.gameObject : other.transform.root.gameObject;
        PlayerSuperMeter meter = player.GetComponent<PlayerSuperMeter>();
        if (meter == null) meter = player.GetComponentInChildren<PlayerSuperMeter>(true);
        if (meter == null) return;

        if (player.TryGetComponent(out PlayerHealth ph) && ph.IsDead)
            return;

        collected = true;
        meter.AddMeter(meterAmount);

        if (collectSound != null && collectVolume > 0f)
            AudioSource.PlayClipAtPoint(collectSound, transform.position, collectVolume);

        Destroy(gameObject);
    }

    private void EnsureTriggerCollider()
    {
        Collider c = GetComponent<Collider>();
        if (c == null)
        {
            SphereCollider s = gameObject.AddComponent<SphereCollider>();
            s.isTrigger = true;
            s.radius = 0.6f;
        }
        else
        {
            c.isTrigger = true;
        }
    }
}
