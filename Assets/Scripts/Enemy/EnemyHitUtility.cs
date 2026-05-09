using UnityEngine;

/// <summary>
/// Routes damage from player weapons to either <see cref="Enemy"/> or <see cref="SmallSkeletonEnemyAI"/>.
/// </summary>
public static class EnemyHitUtility
{
    public static bool TryApplyDamage(Collider collider, float damage)
    {
        if (collider == null) return false;

        // Prefer parent chain, then root search — collider is often on a mesh child while AI is on a sibling/root.
        Enemy enemy = collider.GetComponentInParent<Enemy>();
        if (enemy == null)
            enemy = collider.transform.root.GetComponentInChildren<Enemy>(true);
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            return true;
        }

        SmallSkeletonEnemyAI small = collider.GetComponentInParent<SmallSkeletonEnemyAI>();
        if (small == null)
            small = collider.transform.root.GetComponentInChildren<SmallSkeletonEnemyAI>(true);
        if (small != null)
        {
            small.TakeDamage(damage);
            return true;
        }

        return false;
    }
}
