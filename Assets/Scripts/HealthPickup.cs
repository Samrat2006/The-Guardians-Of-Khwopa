using UnityEngine;

/// <summary>
/// Collectible: restores player HP (default 60). Add to your prefab + a <b>trigger</b> collider (or let this add a sphere trigger).
/// Player root should use tag <c>Player</c>.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HealthPickup : MonoBehaviour
{
    [SerializeField] private int healAmount = 60;
    [SerializeField] private AudioClip collectSound;
    [SerializeField] [Range(0f, 1f)] private float collectVolume = 1f;
    [SerializeField] private bool addSphereTriggerIfMissing = true;

    private bool collected;

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null)
            c.isTrigger = true;
    }

    private void Awake()
    {
        Collider c = GetComponent<Collider>();
        if (c != null && !c.isTrigger)
            c.isTrigger = true;

        if (addSphereTriggerIfMissing && !HasAnyTriggerCollider())
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.75f;
        }
    }

    private bool HasAnyTriggerCollider()
    {
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col != null && col.enabled && col.isTrigger)
                return true;
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider other)
    {
        if (collected || other == null) return;
        if (!HierarchyHasPlayerTag(other.transform)) return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null)
            health = other.GetComponent<PlayerHealth>();
        if (health == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                health = p.GetComponentInChildren<PlayerHealth>();
        }

        if (health == null) return;
        if (health.IsDead) return;
        if (health.CurrentHealth >= health.MaxHealth) return;

        collected = true;
        health.Heal(healAmount);

        if (collectSound != null && collectVolume > 0f)
            AudioSource.PlayClipAtPoint(collectSound, transform.position, collectVolume);

        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col != null) col.enabled = false;
        }

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;

        Destroy(gameObject, 0.1f);
    }

    private static bool HierarchyHasPlayerTag(Transform t)
    {
        for (Transform x = t; x != null; x = x.parent)
        {
            if (x.CompareTag("Player"))
                return true;
        }

        return false;
    }
}
