using UnityEngine;

/// <summary>
/// Attach to the boss/villain (same GameObject as <see cref="Enemy"/>).
/// When this enemy dies, the "defeat villain" quest objective is completed.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public class QuestVillainTarget : MonoBehaviour
{
    private Enemy _enemy;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        if (_enemy != null)
            _enemy.OnDied += OnEnemyDied;
    }

    private void OnDestroy()
    {
        if (_enemy != null)
            _enemy.OnDied -= OnEnemyDied;
    }

    private void OnEnemyDied(Enemy _)
    {
        var qm = QuestManager.Resolve();
        qm?.NotifyVillainDefeated();
    }
}
