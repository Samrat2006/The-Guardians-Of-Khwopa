using UnityEngine;

/// <summary>
/// Simple physics arrow: flies along +Z, damages <see cref="Enemy"/> on trigger, self-destructs.
/// Add a CapsuleCollider (Is Trigger), Rigidbody (useGravity on/off per taste), and assign this script on your arrow prefab.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ArrowProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 45f;
    [SerializeField] private float damage = 12f;
    [SerializeField] private float lifetimeSeconds = 8f;
    [SerializeField] private LayerMask hitMask = ~0;

    private Rigidbody body;
    private Animator animator;
    private bool launched;
    private bool consumed;
    private CapsuleCollider runtimeCapsule;
    private bool useGravity;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        Destroy(gameObject, lifetimeSeconds);
    }

    /// <summary>Call right after Instantiate so velocity uses correct forward.</summary>
    public void LaunchWithDefaults()
    {
        if (launched) return;
        if (body == null)
            body = GetComponent<Rigidbody>();
        if (body == null)
        {
            Debug.LogWarning("ArrowProjectile: Rigidbody is required. Add one to the arrow prefab (or add this script so Unity adds it).");
            return;
        }

        launched = true;

        EnsureSupportedColliderSetup();

        // Animator clips often write root/transform each frame and cancel Rigidbody motion — arrow looks "stuck" in the air.
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator != null)
            animator.enabled = false;

        body.isKinematic = false;
        body.constraints = RigidbodyConstraints.None;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.useGravity = useGravity;
        body.WakeUp();
        body.velocity = transform.forward * speed;
    }

    /// <summary>Uses <see cref="PlayerBowShoot"/> damage and speed instead of prefab defaults.</summary>
    public void Launch(float overrideSpeed, float overrideDamage)
    {
        speed = overrideSpeed;
        damage = overrideDamage;
        LaunchWithDefaults();
    }

    public void SetUseGravity(bool enabled)
    {
        useGravity = enabled;
        if (body != null)
            body.useGravity = enabled;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryHit(collision.collider);
    }

    private void TryHit(Collider other)
    {
        if (consumed) return;
        if (((1 << other.gameObject.layer) & hitMask) == 0) return;

        if (EnemyHitUtility.TryApplyDamage(other, damage))
        {
            consumed = true;
            Destroy(gameObject);
            return;
        }

        // World / props: stop the arrow (trigger or solid collider).
        consumed = true;
        Destroy(gameObject);
    }

    private void EnsureSupportedColliderSetup()
    {
        // Unity error source:
        // Non-convex MeshCollider + non-kinematic Rigidbody is not supported.
        // Many asset-store arrows come with a non-convex MeshCollider by default.
        MeshCollider meshCol = GetComponent<MeshCollider>();
        if (meshCol != null && meshCol.enabled && !meshCol.convex)
        {
            // Disable the unsupported collider and replace it with a lightweight projectile collider.
            meshCol.enabled = false;

            CapsuleCollider cap = GetComponent<CapsuleCollider>();
            if (cap == null)
                cap = gameObject.AddComponent<CapsuleCollider>();

            runtimeCapsule = cap;
            cap.isTrigger = true;
            cap.direction = 2; // Z axis (arrow forward)

            // Best-effort sizing from renderer bounds.
            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null)
            {
                Vector3 size = r.bounds.size;
                float radius = Mathf.Max(0.01f, Mathf.Min(size.x, size.y) * 0.25f);
                float height = Mathf.Max(radius * 2f, size.z);
                cap.radius = radius;
                cap.height = height;
                cap.center = transform.InverseTransformPoint(r.bounds.center);
            }
        }
    }
}
