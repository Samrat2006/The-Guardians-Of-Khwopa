using UnityEngine;

/// <summary>
/// Collectible arrow bundle (like Coin).
/// Attach to a pickup; needs at least one trigger collider (one is added at runtime if missing).
/// </summary>
public class ArrowPickup : MonoBehaviour
{
    [SerializeField] private int amount = 5;
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float floatHeight = 0.25f;
    [SerializeField] private AudioClip collectSound;
    [SerializeField] [Range(0f, 1f)] private float collectVolume = 1f;

    private Vector3 startPosition;
    private bool collected;

    private void Awake()
    {
        startPosition = transform.position;
        EnsurePhysicsForTriggers();
        EnsureTriggerCollider();
    }

    private void Start()
    {
        startPosition = transform.position;
    }

    private void EnsurePhysicsForTriggers()
    {
        if (!TryGetComponent(out Rigidbody rb))
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        else
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    /// <summary>
    /// Ensures a trigger volume exists. Solid mesh colliders alone do not fire trigger overlap with the player.
    /// </summary>
    private void EnsureTriggerCollider()
    {
        bool hasTrigger = false;
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
        {
            if (c != null && c.enabled && c.isTrigger)
            {
                hasTrigger = true;
                break;
            }
        }

        if (!hasTrigger)
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.75f;
        }
    }

    private void Update()
    {
        if (collected) return;
        // Unscaled so bob/spin still run during pause / dialogue freeze if needed.
        transform.Rotate(0f, rotationSpeed * Time.unscaledDeltaTime, 0f);
        float newY = startPosition.y + Mathf.Sin(Time.unscaledTime * floatSpeed) * floatHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollectFromCollider(other);
    }

    /// <summary>
    /// Called from <see cref="ArrowPickupCollector"/> on the player (recommended path).
    /// </summary>
    public void TryCollectFromPlayer(GameObject playerObject)
    {
        if (collected || playerObject == null) return;
        if (!HierarchyHasPlayerTag(playerObject.transform)) return;

        ArrowInventory inv = ResolveArrowInventory(playerObject);
        if (inv == null) return;

        ApplyCollect(inv);
    }

    private void TryCollectFromCollider(Collider other)
    {
        if (collected || other == null) return;
        if (!HierarchyHasPlayerTag(other.transform)) return;

        ArrowInventory inv = ResolveArrowInventory(other);
        if (inv == null) return;

        ApplyCollect(inv);
    }

    private void ApplyCollect(ArrowInventory inv)
    {
        collected = true;
        inv.AddArrows(amount);

        if (collectSound != null)
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
            if (x.CompareTag("Player")) return true;
        }
        return false;
    }

    private static ArrowInventory ResolveArrowInventory(Collider other)
    {
        if (other == null) return null;
        ArrowInventory inv = other.GetComponent<ArrowInventory>();
        if (inv == null) inv = other.GetComponentInParent<ArrowInventory>();
        if (inv == null) inv = other.GetComponentInChildren<ArrowInventory>();
        if (inv == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                inv = player.GetComponentInChildren<ArrowInventory>();
        }
        return inv;
    }

    private static ArrowInventory ResolveArrowInventory(GameObject player)
    {
        if (player == null) return null;
        ArrowInventory inv = player.GetComponent<ArrowInventory>();
        if (inv == null) inv = player.GetComponentInChildren<ArrowInventory>();
        return inv;
    }
}
