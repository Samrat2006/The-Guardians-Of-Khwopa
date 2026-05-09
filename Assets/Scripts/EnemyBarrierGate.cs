using UnityEngine;

/// <summary>
/// Invisible wall that blocks the player until a target enemy is defeated (single-scene levels).
/// Drag the enemy GameObject (e.g. Kawaa with <see cref="SkeletonEnemyAI"/>, or an <see cref="Enemy"/> like Warrox).
/// Use with a child trigger that has <see cref="EnemyBarrierGateSensor"/> to show a dialogue message.
/// </summary>
[DisallowMultipleComponent]
public class EnemyBarrierGate : MonoBehaviour
{
    [Header("Gate target")]
    [Tooltip("The enemy that must die to open this gate. Drag the Skeleton / Kawaa GameObject (or any object with SkeletonEnemyAI or Enemy).")]
    [SerializeField] private GameObject targetEnemyObject;

    [Header("Barrier")]
    [Tooltip("Solid collider that physically blocks the player. Keep Is Trigger OFF.")]
    [SerializeField] private Collider barrierCollider;

    [Header("UI message")]
    [SerializeField] private DialogueManager dialogueManager;
    [TextArea(2, 4)]
    [SerializeField] private string blockedMessage = "Defeat the enemy to pass through!";
    [SerializeField] private float messageCooldownSeconds = 1.25f;

    private bool opened;
    private float nextMessageTime;

    private Enemy _enemy;
    private SkeletonEnemyAI _skeleton;

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null)
        {
            barrierCollider = c;
            c.isTrigger = false;
        }
    }

    private void Awake()
    {
        if (dialogueManager == null)
            dialogueManager = FindFirstObjectByType<DialogueManager>();

        if (barrierCollider != null)
            barrierCollider.isTrigger = false;

        foreach (EnemyBarrierGateSensor sensor in GetComponentsInChildren<EnemyBarrierGateSensor>(true))
        {
            if (sensor != null)
                sensor.Bind(this);
        }

        ResolveTargetComponents();
        AutoResolveTargetIfMissing();
        ResolveTargetComponents();
        SubscribeToTarget();
        ValidateSetup();
    }

    private void OnDestroy()
    {
        UnsubscribeFromTarget();
    }

    private void ResolveTargetComponents()
    {
        _enemy = null;
        _skeleton = null;

        if (targetEnemyObject == null)
            return;

        if (!targetEnemyObject.TryGetComponent(out _skeleton))
            _skeleton = targetEnemyObject.GetComponentInChildren<SkeletonEnemyAI>(true);

        if (!targetEnemyObject.TryGetComponent(out _enemy))
            _enemy = targetEnemyObject.GetComponentInChildren<Enemy>(true);
    }

    private void SubscribeToTarget()
    {
        if (_skeleton != null)
        {
            _skeleton.OnDied -= HandleSkeletonDied;
            _skeleton.OnDied += HandleSkeletonDied;
        }
        else if (_enemy != null)
        {
            _enemy.OnDied -= HandleEnemyDied;
            _enemy.OnDied += HandleEnemyDied;
        }
    }

    private void AutoResolveTargetIfMissing()
    {
        if (_skeleton != null || _enemy != null)
            return;

        SkeletonEnemyAI[] all = FindObjectsByType<SkeletonEnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            string n = all[i].name ?? string.Empty;
            if (n.Contains("Kawaa", System.StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Skeleton", System.StringComparison.OrdinalIgnoreCase))
            {
                _skeleton = all[i];
                if (_skeleton != null)
                    targetEnemyObject = _skeleton.gameObject;
                return;
            }
        }

        _skeleton = FindFirstObjectByType<SkeletonEnemyAI>();
        if (_skeleton != null)
        {
            targetEnemyObject = _skeleton.gameObject;
            return;
        }

        _enemy = FindFirstObjectByType<Enemy>();
        if (_enemy != null)
            targetEnemyObject = _enemy.gameObject;
    }

    private void UnsubscribeFromTarget()
    {
        if (_skeleton != null)
            _skeleton.OnDied -= HandleSkeletonDied;
        else if (_enemy != null)
            _enemy.OnDied -= HandleEnemyDied;
    }

    private void HandleEnemyDied(Enemy _)
    {
        if (opened) return;
        OpenGate();
    }

    private void HandleSkeletonDied(SkeletonEnemyAI _)
    {
        if (opened) return;
        OpenGate();
    }

    private void OpenGate()
    {
        opened = true;
        if (barrierCollider != null)
            barrierCollider.enabled = false;
    }

    /// <summary>Called by sensors when player touches the gate before it's opened.</summary>
    public void NotifyPlayerHitBarrier()
    {
        if (opened) return;
        if (Time.unscaledTime < nextMessageTime) return;
        nextMessageTime = Time.unscaledTime + Mathf.Max(0.1f, messageCooldownSeconds);

        if (dialogueManager != null)
            dialogueManager.StartDialogue(new[] { blockedMessage });
    }

    private void ValidateSetup()
    {
        if (barrierCollider == null)
            Debug.LogError("EnemyBarrierGate: Barrier Collider is not assigned. Drag the solid BoxCollider (Is Trigger OFF).", this);
        else if (barrierCollider.isTrigger)
            Debug.LogError("EnemyBarrierGate: Barrier Collider must NOT be a trigger (Is Trigger OFF).", this);

        if (_skeleton == null && _enemy == null)
            Debug.LogError("EnemyBarrierGate: Target Enemy Object has no SkeletonEnemyAI or Enemy. Drag the Kawaa/Skeleton/Warrox GameObject.", this);

        EnemyBarrierGateSensor[] sensors = GetComponentsInChildren<EnemyBarrierGateSensor>(true);
        if (sensors == null || sensors.Length == 0)
            Debug.LogWarning("EnemyBarrierGate: No sensor found. Add a child trigger with EnemyBarrierGateSensor to show the message.", this);
    }
}
