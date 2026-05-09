using UnityEngine;

public enum LevelBarrierUnlockMode
{
    [Tooltip("Original behaviour: barrier opens only when Gate Enemy dies.")]
    EnemyDefeatedOnly,

    [Tooltip("Barrier opens only when QuestManager marks every tracked objective complete.")]
    AllQuestObjectivesComplete,

    [Tooltip("Gate Enemy must die and every tracked quest objective must be complete.")]
    EnemyDefeatedAndAllQuestObjectives
}

/// <summary>
/// Level barrier / portal combo. Blocking collider hides after unlock; portal VFX shows until crossed.
/// Unlock can depend on Gate Enemy dying, completing all active quest objectives on <see cref="QuestManager"/>, or both.
/// </summary>
[DisallowMultipleComponent]
public class LevelBarrierPortal : MonoBehaviour
{
    [Header("Unlock rules")]
    [SerializeField] private LevelBarrierUnlockMode unlockMode = LevelBarrierUnlockMode.EnemyDefeatedOnly;
    [Tooltip("Tracked objectives with “Track ✓” in Quest Manager must all be complete when mode uses quests.")]
    [SerializeField] private QuestManager questManager;

    [Header("Gate target enemy")]
    [Tooltip("Referenced Enemy (e.g. Warrox). Only used when unlock mode involves Enemy defeated.")]
    [SerializeField] private Enemy warrox;

    [Header("Barrier colliders")]
    [Tooltip("Solid collider that physically blocks the player. Keep Is Trigger OFF.")]
    [SerializeField] private Collider barrierCollider;

    [Header("Portal")]
    [Tooltip("Root GameObject for portal VFX (set inactive at start; enabled when Warrox is defeated).")]
    [SerializeField] private GameObject portalVfxRoot;
    [Tooltip("After player enters the portal, hide the portal after this many seconds.")]
    [SerializeField] private float portalDisableDelaySeconds = 5f;
    [Tooltip("After player enters the portal, rebuild the barrier after this many seconds.")]
    [SerializeField] private float barrierRebuildDelaySeconds = 10f;

    [Header("UI message")]
    [SerializeField] private DialogueManager dialogueManager;
    [TextArea(2, 4)]
    [SerializeField] private string blockedMessage = "Defeat Warrox to enter the next level!";
    [SerializeField] private bool appendIncompleteQuestsToBlockedMessage = true;
    [SerializeField] private float messageCooldownSeconds = 1.25f;

    [Header("Debug")]
    [Tooltip("If off, suppress setup warnings (helps reduce Console spam / editor lag).")]
    [SerializeField] private bool logSetupWarnings = false;

    private bool opened;
    private float nextMessageTime;
    private bool closingScheduled;
    private bool enemyGateSatisfied;

    /// <summary>Quest instance we subscribed to (may differ from serialized field after Resolve).</summary>
    private QuestManager _questListening;

    private void Reset()
    {
        // Best-effort: use the first Collider on this object as barrier.
        Collider c = GetComponent<Collider>();
        if (c != null)
        {
            barrierCollider = c;
            c.isTrigger = false;
        }
    }

    private void Awake()
    {
        if (unlockMode != LevelBarrierUnlockMode.AllQuestObjectivesComplete && warrox == null)
            warrox = FindFirstObjectByType<Enemy>();

        if (questManager == null)
            questManager = QuestManager.Resolve();

        if (dialogueManager == null)
            dialogueManager = FindFirstObjectByType<DialogueManager>();

        if (portalVfxRoot != null)
            portalVfxRoot.SetActive(false);
        if (barrierCollider != null)
            barrierCollider.isTrigger = false;

        // If you placed sensors on child triggers, auto-bind them.
        foreach (LevelBarrierPortalSensor sensor in GetComponentsInChildren<LevelBarrierPortalSensor>(true))
        {
            if (sensor != null)
                sensor.Bind(this);
        }

        ValidateSetup();

        if (UnlockUsesEnemyDeath())
        {
            if (warrox != null)
            {
                warrox.OnDied -= HandleWarroxDied;
                warrox.OnDied += HandleWarroxDied;
            }
        }

        _questListening = questManager != null ? questManager : QuestManager.Resolve();
        if (UnlockUsesQuestChapter() && _questListening != null)
        {
            _questListening.ProgressChanged -= HandleQuestProgressChanged;
            _questListening.ProgressChanged += HandleQuestProgressChanged;
        }
    }

    private void Start()
    {
        // If quests were finished before Awake/subscription (reload edge) or enemy already dead.
        TryOpenPortalIfSatisfied();
    }

    private void OnDestroy()
    {
        if (warrox != null)
            warrox.OnDied -= HandleWarroxDied;
        if (_questListening != null)
            _questListening.ProgressChanged -= HandleQuestProgressChanged;
    }

    private static bool UsesQuest(LevelBarrierUnlockMode m)
    {
        return m == LevelBarrierUnlockMode.AllQuestObjectivesComplete
            || m == LevelBarrierUnlockMode.EnemyDefeatedAndAllQuestObjectives;
    }

    private static bool UsesEnemy(LevelBarrierUnlockMode m)
    {
        return m == LevelBarrierUnlockMode.EnemyDefeatedOnly
            || m == LevelBarrierUnlockMode.EnemyDefeatedAndAllQuestObjectives;
    }

    private bool UnlockUsesQuestChapter() => UsesQuest(unlockMode);

    private bool UnlockUsesEnemyDeath() => UsesEnemy(unlockMode);

    private void HandleWarroxDied(Enemy deadEnemy)
    {
        enemyGateSatisfied = true;
        TryOpenPortalIfSatisfied();
    }

    private void HandleQuestProgressChanged()
    {
        TryOpenPortalIfSatisfied();
    }

    private bool IsQuestChapterSatisfied()
    {
        if (!UnlockUsesQuestChapter())
            return true;
        var q = _questListening != null ? _questListening : QuestManager.Resolve();
        return q != null && q.AreAllActiveObjectivesComplete();
    }

    private bool IsEnemyGateSatisfied()
    {
        if (!UnlockUsesEnemyDeath())
            return true;
        return enemyGateSatisfied;
    }

    private void TryOpenPortalIfSatisfied()
    {
        if (opened) return;
        if (!IsEnemyGateSatisfied() || !IsQuestChapterSatisfied())
            return;
        OpenPortal();
    }

    private void OpenPortal()
    {
        opened = true;

        if (barrierCollider != null)
            barrierCollider.enabled = false;

        if (portalVfxRoot != null)
        {
            portalVfxRoot.SetActive(true);
            DisablePortalBlockingColliders(portalVfxRoot);
        }
    }

    private void ClosePortalAndRestoreBarrier()
    {
        if (barrierCollider != null)
            barrierCollider.enabled = true;

        if (portalVfxRoot != null)
            portalVfxRoot.SetActive(false);
    }

    /// <summary>Called by the message trigger sensor when player tries to pass before Warrox is defeated.</summary>
    public void NotifyPlayerHitBarrier()
    {
        if (opened) return;
        if (Time.unscaledTime < nextMessageTime) return;
        nextMessageTime = Time.unscaledTime + Mathf.Max(0.1f, messageCooldownSeconds);

        if (dialogueManager != null)
        {
            string msg = blockedMessage;
            if (appendIncompleteQuestsToBlockedMessage && UnlockUsesQuestChapter())
            {
                var q = _questListening != null ? _questListening : QuestManager.Resolve();
                if (q != null && !q.AreAllActiveObjectivesComplete())
                    msg = q.BuildBarrierBlockedMessage(blockedMessage);
            }
            dialogueManager.StartDialogue(new[] { msg });
        }
    }

    /// <summary>Called by the portal trigger sensor when player crosses the portal.</summary>
    public void NotifyPlayerCrossedPortal()
    {
        if (!opened) return;
        if (closingScheduled) return;
        closingScheduled = true;

        // Let the player pass through now. We delay both portal disable and barrier rebuild.
        StartCoroutine(ClosePortalAndRebuildBarrierRoutine());
    }

    private System.Collections.IEnumerator ClosePortalAndRebuildBarrierRoutine()
    {
        float portalDelay = Mathf.Max(0f, portalDisableDelaySeconds);
        float barrierDelay = Mathf.Max(0f, barrierRebuildDelaySeconds);

        // Ensure the barrier stays open while crossing.
        if (barrierCollider != null)
            barrierCollider.enabled = false;

        // Hide portal after N seconds.
        if (portalDelay > 0f)
            yield return new WaitForSecondsRealtime(portalDelay);
        if (portalVfxRoot != null)
            portalVfxRoot.SetActive(false);

        // Rebuild barrier after M seconds since crossing (not after portal delay).
        float remaining = Mathf.Max(0f, barrierDelay - portalDelay);
        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        if (barrierCollider != null)
            barrierCollider.enabled = true;
    }

    private static void DisablePortalBlockingColliders(GameObject root)
    {
        if (root == null) return;

        // Portal VFX prefabs sometimes include non-trigger colliders that act as invisible walls.
        // We disable ONLY non-trigger colliders so trigger sensors still work.
        foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
        {
            if (c == null) continue;
            if (c.isTrigger) continue;
            c.enabled = false;
        }
    }

    private void ValidateSetup()
    {
        if (UnlockUsesEnemyDeath() && warrox == null)
            Debug.LogError("LevelBarrierPortal: Gate Enemy (Warrox) is required for this unlock mode. Assign the Enemy or switch Unlock Mode to quest-only.", this);

        if (UnlockUsesQuestChapter() && questManager == null && QuestManager.Resolve() == null && logSetupWarnings)
            Debug.LogWarning("LevelBarrierPortal: No QuestManager in scene but unlock mode requires quest completion. Add QuestManager.", this);

        if (barrierCollider == null)
            Debug.LogError("LevelBarrierPortal: Barrier Collider is not assigned. Drag the solid BoxCollider (Is Trigger OFF).", this);
        else if (barrierCollider.isTrigger)
            Debug.LogError("LevelBarrierPortal: Barrier Collider must NOT be a trigger (Is Trigger OFF).", this);

        if (portalVfxRoot == null)
            Debug.LogError("LevelBarrierPortal: Portal Vfx Root is not assigned. Drag the portal VFX GameObject (scene instance).", this);

        if (dialogueManager == null && logSetupWarnings)
            Debug.LogWarning("LevelBarrierPortal: DialogueManager not assigned (message will not show). Drag your Canvas/DialogueManager.", this);

        LevelBarrierPortalSensor[] sensors = GetComponentsInChildren<LevelBarrierPortalSensor>(true);
        bool hasMessage = false;
        bool hasPortal = false;
        for (int i = 0; i < sensors.Length; i++)
        {
            if (sensors[i] == null) continue;
            if (sensors[i].Mode == LevelBarrierPortalSensor.SensorMode.MessageBarrier) hasMessage = true;
            if (sensors[i].Mode == LevelBarrierPortalSensor.SensorMode.Portal) hasPortal = true;
        }

        if (!hasMessage && logSetupWarnings)
            Debug.LogWarning("LevelBarrierPortal: No MessageBarrier sensor found. Add a child trigger with LevelBarrierPortalSensor (Mode=MessageBarrier) to show the message.", this);
        if (!hasPortal && logSetupWarnings)
            Debug.LogWarning("LevelBarrierPortal: No Portal sensor found. Add a trigger inside the portal with LevelBarrierPortalSensor (Mode=Portal) to detect crossing.", this);
    }
}

