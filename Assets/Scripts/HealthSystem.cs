using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [SerializeField] float health = 100;
    [SerializeField] GameObject hitVFX;
    [SerializeField] GameObject ragdoll;

    Animator animator;
    bool isDead = false;
    [SerializeField] private bool debugLogs = false;

    void Start()
    {
        // Find animator even if it is on a child object
        animator = GetComponentInChildren<Animator>();
    }

    public void TakeDamage(float damageAmount)
    {
        if (isDead) return;

        if (debugLogs) Debug.Log(gameObject.name + " GOT HIT! Damage: " + damageAmount);

        health -= damageAmount;

        if (debugLogs) Debug.Log(gameObject.name + " Health Remaining: " + health);

        if (animator != null)
        {
            animator.SetTrigger("damage");
        }

        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.ShakeCamera(2f, 0.2f);
        }

        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;

        if (debugLogs) Debug.Log(gameObject.name + " DIED");

        if (animator != null)
        {
            animator.SetTrigger("death");
        }

        if (ragdoll != null)
        {
            Instantiate(ragdoll, transform.position, transform.rotation);
            Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject, 4f);
        }
    }

    public void HitVFX(Vector3 hitPosition)
    {
        if (hitVFX != null)
        {
            GameObject hit = Instantiate(hitVFX, hitPosition, Quaternion.identity);
            Destroy(hit, 3f);
        }
    }
}