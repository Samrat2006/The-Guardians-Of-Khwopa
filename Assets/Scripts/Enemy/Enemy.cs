using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    /// <summary>Fires exactly once when this enemy dies.</summary>
    public event System.Action<Enemy> OnDied;
    [Header("Health")]
    [SerializeField] float health = 3f;
    [Tooltip("Optional: spawned when this enemy **takes** damage. Not used on melee swings. For Warrox super swing VFX, use Warrox Super Attack Vfx Prefab only.")]
    [SerializeField] GameObject hitVFX;

    float maxHealth;

    [Header("Combat")]
    [SerializeField] float attackRange = 2f;
    [Tooltip("Extra distance so enemy doesn't jitter between walk/attack at the edge.")]
    [SerializeField] float attackRangeHysteresis = 0.35f;
    [SerializeField] float aggroRange = 8f;
    [SerializeField] float attackCooldown = 2f;
    [SerializeField] float attackDamage = 10f;
    [SerializeField] float colliderSeparationBuffer = 0.35f;
    [SerializeField] float attackMoveLockDuration = 1.0f;
    [SerializeField] float postDeathDisableSeconds = 6f;
    [Tooltip("Used for crossfade when Attack State Names list is empty.")]
    [SerializeField] string defaultAttackStateName = "Mutant Punch";
    [Tooltip("Used for crossfade on non-lethal hits (enemy reaction).")]
    [SerializeField] string defaultHitStateName = "Emeny_damage";
    [SerializeField] string[] attackStateNames;
    [SerializeField] bool useAnimationEventsForHit = false;
    [SerializeField] float fallbackHitDelay = 0.25f;
    [SerializeField] float rotationSpeed = 8f;
    [Header("Death")]
    [Tooltip("Snaps the enemy to ground on death to avoid floating during the death clip.")]
    [SerializeField] private bool snapToGroundOnDeath = true;
    [Tooltip("Raycast distance used for death snap.")]
    [SerializeField] private float deathSnapMaxDistance = 80f;
    [Tooltip("If empty, uses DefaultRaycastLayers.")]
    [SerializeField] private LayerMask deathSnapGroundMask = 0;

    [Header("Warrox attacks (optional)")]
    [Tooltip("If set, Warrox will randomly pick between these state names when attacking.")]
    [SerializeField] private string[] warroxAttackStateNames;

    [Header("Warrox super attack (optional)")]
    [Tooltip("Animator Trigger name for super (e.g. superattack → Any State → SuperAttack). Must match Animator exactly. Leave empty if you only use State Name below.")]
    [SerializeField] private string warroxSuperAttackTriggerName = "superattack";
    [Tooltip("Optional: crossfade to this state during super. If Super Attack Trigger Name is set and exists on the Animator, trigger alone is used (no crossfade). Otherwise use this or triggers + default attacks.")]
    [SerializeField] private string warroxSuperAttackStateName = "";
    [Tooltip("First super queues when current HP / max HP is at or below this percent (e.g. 60).")]
    [SerializeField] [Range(1f, 100f)] private float warroxSuperThresholdFirstPercent = 60f;
    [Tooltip("Second super queues when HP / max HP is at or below this percent (e.g. 10).")]
    [SerializeField] [Range(1f, 100f)] private float warroxSuperThresholdSecondPercent = 10f;
    [Tooltip("If > 0, super hit uses this damage instead of normal Attack Damage (recommended: higher than Attack Damage). If 0, super uses multiplier below.")]
    [SerializeField] private float warroxSuperAttackDamageOverride = 0f;
    [Tooltip("Super damage = Attack Damage × this when override is 0. Use > 1 so super hits harder than normal (e.g. 2.5).")]
    [SerializeField] private float warroxSuperAttackDamageMultiplier = 2.5f;
    [Tooltip("If > 0, overrides attack move lock duration while a super is playing.")]
    [SerializeField] private float warroxSuperAttackMoveLockDuration = 0f;
    [Tooltip("Minimum time before super hit applies when not using animation events.")]
    [SerializeField] private float warroxSuperAttackHitDelay = 0.4f;

    [Header("Warrox super attack VFX (super only)")]
    [Tooltip("Prefab spawned **only** when a Warrox **super** attack starts. Normal punches/kicks never spawn this — leave empty if you have no super VFX.")]
    [SerializeField] private GameObject warroxSuperAttackVfxPrefab;
    [Tooltip("0 = auto lifetime from particles. Uses real time (not timeScale).")]
    [SerializeField] private float warroxSuperVfxLifetimeSeconds = 0f;
    [SerializeField] private float warroxSuperVfxMaxLifetimeSeconds = 5f;

    [Header("Warrox super attack audio (super only)")]
    [Tooltip("Placeholder: Warrox voice / roar / power sting when a super attack starts.")]
    [SerializeField] private AudioClip warroxSuperAttackWarroxSound;
    [SerializeField] [Range(0f, 1f)] private float warroxSuperAttackWarroxSoundVolume = 1f;
    [Tooltip("Placeholder: Super swing whoosh / heavy hit / magic layer when a super attack starts.")]
    [SerializeField] private AudioClip warroxSuperAttackEffectSound;
    [SerializeField] [Range(0f, 1f)] private float warroxSuperAttackEffectSoundVolume = 1f;

    [Header("Audio (optional — assign on Warrox / mutants as needed)")]
    [Tooltip("Played once the first time the player enters aggro range (roar / wake-up).")]
    [SerializeField] private AudioClip aggroActivationSound;
    [SerializeField] [Range(0f, 1f)] private float aggroActivationVolume = 1f;
    [Tooltip("Played each time this enemy starts a melee swing (trigger + attack motion).")]
    [SerializeField] private AudioClip attackSound;
    [SerializeField] [Range(0f, 1f)] private float attackSoundVolume = 1f;
    [SerializeField] private float audioEmitHeight = 1.2f;

    [Header("Debug")]
    [Tooltip("Turn off to prevent Console spam (improves editor FPS).")]
    [SerializeField] private bool debugLogs = false;

    [Header("When player is defeated")]
    [Tooltip("Animator bool on Warrox: Any State → victory pose (e.g. muscle flex).")]
    [SerializeField] bool useVictoryParameter = true;
    [Tooltip("If the controller has no bool, cross-fade to this state name on the base layer.")]
    [SerializeField] string victoryStateFallback = "muscle Flex 0";

    GameObject player;
    NavMeshAgent agent;
    Animator animator;
    PlayerHealth playerHealth;

    float attackTimer;
    bool isDead = false;
    bool pendingDamage;
    float pendingDamageTimer;
    bool warnedAboutMissingAttackState;
    bool warnedAboutMissingAttackingTrigger;
    bool hasAttackingTrigger;
    bool hasAttackingTriggerAlt;
    bool hasAttackTrigger;
    bool hasAttackTriggerAlt;
    bool hasHitTrigger;
    bool hasIsDeadTrigger;
    bool hasIsDeadBool;
    bool hasSpeedFloat;
    bool hasVictoryBool;
    bool hasDeadBoolParam;
    float minSeparationDistance;
    float attackMoveLockUntil;
    bool playerDefeatedMode;
    bool inAttackRangeLatch;
    bool playedAggroActivationSound;

    bool warroxSuperCrossed60;
    bool warroxSuperCrossed10;
    int pendingWarroxSuperCount;
    bool activeMeleeIsSuperAttack;

    int warroxSuperAttackTriggerHash;
    bool hasWarroxSuperAttackTrigger;

    static readonly int AttackingTrigger = Animator.StringToHash("attacking");
    static readonly int AttackingTriggerAlt = Animator.StringToHash("attacking1");
    // Support other controllers that use attack/attack2 triggers.
    static readonly int AttackTrigger = Animator.StringToHash("attack");
    static readonly int AttackTriggerAlt = Animator.StringToHash("attack2");
    static readonly int HitTrigger = Animator.StringToHash("hit");
    static readonly int IsDeadHash = Animator.StringToHash("isdead");
    static readonly int VictoryBool = Animator.StringToHash("victory");
    static readonly int DeadBool = Animator.StringToHash("dead");
    static readonly int SpeedFloat = Animator.StringToHash("speed");

    public bool IsEnemyDead => isDead;
    public float CurrentHealth => health;
    public float MaxHealth => maxHealth;
    public float HealthNormalized => maxHealth > 0.001f ? Mathf.Clamp01(health / maxHealth) : 0f;

    /// <summary>Called when the player dies: all enemies stop combat and play victory where configured.</summary>
    public static void NotifyPlayerDefeated()
    {
        Enemy[] enemies = Object.FindObjectsOfType<Enemy>();
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null && !enemies[i].isDead)
                enemies[i].EnterPlayerDefeatedMode();
        }
    }

    void Awake()
    {
        maxHealth = Mathf.Max(health, 0.01f);
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.updateRotation = false;
        }
        animator = ResolveAnimator();
        attackTimer = attackCooldown;
        CacheAnimatorParameters();
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            minSeparationDistance = CalculateMinimumSeparation(player);
            if (agent != null)
            {
                // Stop outside collider overlap so enemy doesn't "merge" with player.
                agent.stoppingDistance = Mathf.Max(minSeparationDistance, 0.1f);
            }
        }

        EnemyHealthBar.EnsureForEnemy(this);

        // If Warrox spawns already under a super threshold, queue those supers without waiting for damage.
        EvaluateWarroxSuperCharges();
    }

    void Update()
    {
        if (isDead) return;
        if (DialogueManager.IsBlockingGameplay) return;

        TryRefreshPlayerReferences();
        if (playerHealth != null && playerHealth.IsDead)
        {
            if (!playerDefeatedMode)
                EnterPlayerDefeatedMode();
            MaintainPlayerDefeatedBehaviour();
            return;
        }

        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.transform.position);

        if (!playedAggroActivationSound && distance <= aggroRange)
        {
            playedAggroActivationSound = true;
            PlayEnemySound(aggroActivationSound, aggroActivationVolume);
        }

        bool attackLocked = Time.time < attackMoveLockUntil;

        RotateTowardsPlayer();

        attackTimer += Time.deltaTime;

        if (!useAnimationEventsForHit && pendingDamage)
        {
            pendingDamageTimer += Time.deltaTime;
            float hitDelay = activeMeleeIsSuperAttack
                ? Mathf.Max(fallbackHitDelay, warroxSuperAttackHitDelay)
                : fallbackHitDelay;
            if (pendingDamageTimer >= hitDelay)
            {
                pendingDamage = false;
                pendingDamageTimer = 0f;
                DamagePlayer();
            }
        }

        float effectiveAttackRange = Mathf.Max(attackRange, minSeparationDistance);
        float enterRange = effectiveAttackRange;
        float exitRange = effectiveAttackRange + Mathf.Max(0f, attackRangeHysteresis);

        // Hysteresis latch: once we're in range, stay "in range" until a bit farther away.
        if (!inAttackRangeLatch)
            inAttackRangeLatch = distance <= enterRange;
        else
            inAttackRangeLatch = distance <= exitRange;

        bool inAttackRange = inAttackRangeLatch;
        if (attackLocked || inAttackRange)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();

            // While locked, don't switch to chase logic (prevents "back after punch").
            if (!attackLocked && attackTimer >= attackCooldown)
            {
                bool doSuper = pendingWarroxSuperCount > 0 && WarroxSuperConfigured();
                if (doSuper)
                {
                    pendingWarroxSuperCount--;
                    activeMeleeIsSuperAttack = true;
                    PlayWarroxSuperAttackAnimation();
                    SpawnWarroxSuperVfx();
                    PlayWarroxSuperAttackSounds();
                    if (debugLogs) Debug.Log("Warrox super attack");
                    QueueDamage();
                    attackTimer = 0f;
                    float lockDur = warroxSuperAttackMoveLockDuration > 0f
                        ? warroxSuperAttackMoveLockDuration
                        : attackMoveLockDuration;
                    attackMoveLockUntil = Time.time + lockDur;
                }
                else
                {
                    // Normal melee: no swing VFX (only warroxSuperAttackVfxPrefab on super, above).
                    activeMeleeIsSuperAttack = false;
                    FireRandomAttackTrigger();
                    TryPlayRandomAttackState();
                    PlayEnemySound(attackSound, attackSoundVolume);
                    if (debugLogs) Debug.Log("Enemy Attacking");
                    QueueDamage();
                    attackTimer = 0f;
                    attackMoveLockUntil = Time.time + attackMoveLockDuration;
                }
            }
        }
        else if (distance <= aggroRange)
        {
            agent.isStopped = false;
            Vector3 targetPoint = GetChaseStopPoint();
            agent.SetDestination(targetPoint);
        }
        else
        {
            agent.isStopped = true;
        }

        if (agent != null && hasSpeedFloat)
        {
            float speedBlend = (attackLocked || inAttackRange)
                ? 0f
                : (agent.speed > 0f ? agent.velocity.magnitude / agent.speed : 0f);
            animator.SetFloat(SpeedFloat, speedBlend);
        }
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        health -= damage;

        if (debugLogs) Debug.Log("Enemy took damage. Health: " + health);

        // If this hit kills the enemy, do death only (avoid hit trigger fighting with death).
        if (health <= 0f)
        {
            Die();
            return;
        }

        EvaluateWarroxSuperCharges();

        if (hasHitTrigger)
        {
            animator.ResetTrigger(HitTrigger);
            animator.SetTrigger(HitTrigger);
        }
        else
        {
            // Fallback: still play a hit reaction even if Animator parameters are missing.
            if (animator != null && !string.IsNullOrWhiteSpace(defaultHitStateName))
            {
                animator.CrossFadeInFixedTime(defaultHitStateName, 0.1f, 0);
            }
        }

        if (hitVFX != null)
            Instantiate(hitVFX, transform.position, Quaternion.identity);
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        pendingDamage = false;
        pendingDamageTimer = 0f;

        OnDied?.Invoke(this);

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        if (animator != null)
        {
            // Ensure death is the final animation (avoid hit/attack triggers after death).
            animator.ResetTrigger(AttackingTrigger);
            if (hasAttackingTriggerAlt) animator.ResetTrigger(AttackingTriggerAlt);
            if (hasAttackTrigger) animator.ResetTrigger(AttackTrigger);
            if (hasAttackTriggerAlt) animator.ResetTrigger(AttackTriggerAlt);
            if (hasWarroxSuperAttackTrigger) animator.ResetTrigger(warroxSuperAttackTriggerHash);
            if (hasHitTrigger) animator.ResetTrigger(HitTrigger);

            // Root motion can lift/offset the enemy during death clips.
            animator.applyRootMotion = false;

            if (snapToGroundOnDeath)
                SnapToGroundForDeath();

            for (int layer = 1; layer < animator.layerCount; layer++)
                animator.SetLayerWeight(layer, 0f);

            if (hasSpeedFloat)
                animator.SetFloat(SpeedFloat, 0f);

            if (hasIsDeadTrigger)
            {
                animator.ResetTrigger(IsDeadHash);
                animator.SetTrigger(IsDeadHash);
            }
            else if (hasIsDeadBool)
            {
                animator.SetBool(IsDeadHash, true);
            }

            // Extra reliability: crossfade to state "death" (works alongside Any State → death on isdead).
            int deathStateHash = Animator.StringToHash("death");
            int deathLayer = FindLayerForState(deathStateHash);
            if (deathLayer >= 0)
            {
                animator.CrossFadeInFixedTime("death", 0.1f, deathLayer);
            }
            else
            {
                animator.CrossFadeInFixedTime("death", 0.1f, 0);
                animator.Play("death", 0);
            }

            animator.Update(0f);
        }

        Destroy(gameObject, postDeathDisableSeconds);
    }

    private void SnapToGroundForDeath()
    {
        LayerMask mask = deathSnapGroundMask.value != 0 ? deathSnapGroundMask : Physics.DefaultRaycastLayers;
        Vector3 origin = transform.position + Vector3.up * 4f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, deathSnapMaxDistance, mask, QueryTriggerInteraction.Ignore))
        {
            origin = transform.position + Vector3.up * 0.5f;
            if (!Physics.Raycast(origin, Vector3.down, out hit, deathSnapMaxDistance, mask, QueryTriggerInteraction.Ignore))
                return;
        }

        // Use collider bounds if possible so we place the feet on the ground.
        float extra = 0.02f;
        Collider c = GetComponent<Collider>();
        float halfHeight = c != null ? (c.bounds.size.y * 0.5f) : 1f;
        Vector3 p = transform.position;
        p.y = hit.point.y + halfHeight + extra;
        transform.position = p;
    }

    void DamagePlayer()
    {
        if (playerHealth == null) return;
        if (playerHealth.IsDead) return;

        if (debugLogs) Debug.Log("Hit Registered");
        float dmg = attackDamage;
        if (activeMeleeIsSuperAttack)
        {
            if (warroxSuperAttackDamageOverride > 0f)
                dmg = warroxSuperAttackDamageOverride;
            else
                dmg = attackDamage * Mathf.Max(0.01f, warroxSuperAttackDamageMultiplier);
        }

        playerHealth.TakeDamage(dmg);
        activeMeleeIsSuperAttack = false;
    }

    void QueueDamage()
    {
        pendingDamage = true;
        pendingDamageTimer = 0f;
        if (!useAnimationEventsForHit)
        {
            // fallback uses timer in Update()
            return;
        }
    }

    void RotateTowardsPlayer()
    {
        if (player == null) return;

        Vector3 direction = player.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    Vector3 GetChaseStopPoint()
    {
        Vector3 playerPosition = player.transform.position;
        Vector3 toEnemy = transform.position - playerPosition;
        toEnemy.y = 0f;

        if (toEnemy.sqrMagnitude < 0.0001f)
        {
            toEnemy = -transform.forward;
            toEnemy.y = 0f;
        }

        Vector3 direction = toEnemy.normalized;
        return playerPosition + direction * Mathf.Max(minSeparationDistance, 0.1f);
    }

    float CalculateMinimumSeparation(GameObject playerObject)
    {
        float enemyRadius = 0.5f;
        CapsuleCollider enemyCapsule = GetComponent<CapsuleCollider>();
        if (enemyCapsule != null)
        {
            enemyRadius = Mathf.Max(enemyCapsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z), 0.1f);
        }

        float playerRadius = 0.5f;
        CharacterController playerController = playerObject.GetComponent<CharacterController>();
        if (playerController != null)
        {
            playerRadius = Mathf.Max(playerController.radius * Mathf.Max(playerObject.transform.lossyScale.x, playerObject.transform.lossyScale.z), 0.1f);
        }
        else
        {
            CapsuleCollider playerCapsule = playerObject.GetComponent<CapsuleCollider>();
            if (playerCapsule != null)
            {
                playerRadius = Mathf.Max(playerCapsule.radius * Mathf.Max(playerObject.transform.lossyScale.x, playerObject.transform.lossyScale.z), 0.1f);
            }
        }

        return enemyRadius + playerRadius + colliderSeparationBuffer;
    }

    // Animation Event hook (recommended): call this from the enemy attack clip at the hit frame.
    public void AnimEvent_AttackHit()
    {
        if (!useAnimationEventsForHit) return;
        if (!pendingDamage) return;
        if (playerHealth != null && playerHealth.IsDead) return;
        pendingDamage = false;
        pendingDamageTimer = 0f;
        DamagePlayer();
    }

    void TryPlayRandomAttackState()
    {
        // Warrox override: if provided, use these two (or more) attack states randomly.
        if (warroxAttackStateNames != null && warroxAttackStateNames.Length > 0)
        {
            string selected = warroxAttackStateNames[Random.Range(0, warroxAttackStateNames.Length)];
            if (!string.IsNullOrWhiteSpace(selected))
            {
                TryCrossFadeToState(selected);
                return;
            }
        }

        // If no random list provided, use a single default state.
        if (attackStateNames == null || attackStateNames.Length == 0)
        {
            if (string.IsNullOrWhiteSpace(defaultAttackStateName))
            {
                return;
            }

            TryCrossFadeToState(defaultAttackStateName);
            return;
        }

        string selectedState = attackStateNames[Random.Range(0, attackStateNames.Length)];
        if (string.IsNullOrWhiteSpace(selectedState)) return;

        TryCrossFadeToState(selectedState);
    }

    void TryCrossFadeToState(string stateName)
    {
        int stateHash = Animator.StringToHash(stateName);
        int foundLayer = FindLayerForState(stateHash);
        if (foundLayer >= 0)
        {
            animator.CrossFadeInFixedTime(stateName, 0.05f, foundLayer);
        }
        else
        {
            // Fallback to default state if the inspector state name doesn't match the controller.
            if (!string.IsNullOrWhiteSpace(defaultAttackStateName) && !stateName.Equals(defaultAttackStateName))
            {
                int defaultHash = Animator.StringToHash(defaultAttackStateName);
                int defaultLayer = FindLayerForState(defaultHash);
                if (defaultLayer >= 0)
                {
                    animator.CrossFadeInFixedTime(defaultAttackStateName, 0.05f, defaultLayer);
                    return;
                }
            }

            // Do not CrossFade to a missing state — that breaks controllers that rely on triggers (e.g. Warrox Any State → Attack1).
            if (!warnedAboutMissingAttackState)
            {
                Debug.LogWarning(
                    $"Enemy on '{name}': animator has no state '{stateName}' (and no valid default). Attack triggers still fire; fix Default Attack State Name / Warrox Attack State Names to match your Animator.",
                    this);
                warnedAboutMissingAttackState = true;
            }
        }
    }

    int FindLayerForState(int stateHash)
    {
        if (animator == null) return -1;

        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            if (animator.HasState(layer, stateHash))
            {
                return layer;
            }
        }

        return -1;
    }

    Animator ResolveAnimator()
    {
        Animator ownAnimator = GetComponent<Animator>();
        if (ownAnimator != null && ownAnimator.runtimeAnimatorController != null)
        {
            return ownAnimator;
        }

        Animator[] childAnimators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < childAnimators.Length; i++)
        {
            Animator candidate = childAnimators[i];
            if (candidate != null && candidate.runtimeAnimatorController != null)
            {
                return candidate;
            }
        }

        return ownAnimator;
    }

    void CacheAnimatorParameters()
    {
        hasAttackingTrigger = HasParameter(AttackingTrigger, AnimatorControllerParameterType.Trigger);
        hasAttackingTriggerAlt = HasParameter(AttackingTriggerAlt, AnimatorControllerParameterType.Trigger);
        hasAttackTrigger = HasParameter(AttackTrigger, AnimatorControllerParameterType.Trigger);
        hasAttackTriggerAlt = HasParameter(AttackTriggerAlt, AnimatorControllerParameterType.Trigger);
        hasHitTrigger = HasParameter(HitTrigger, AnimatorControllerParameterType.Trigger);
        hasIsDeadTrigger = HasParameter(IsDeadHash, AnimatorControllerParameterType.Trigger);
        hasIsDeadBool = HasParameter(IsDeadHash, AnimatorControllerParameterType.Bool);
        hasVictoryBool = HasParameter(VictoryBool, AnimatorControllerParameterType.Bool);
        hasDeadBoolParam = HasParameter(DeadBool, AnimatorControllerParameterType.Bool);
        hasSpeedFloat = HasParameter(SpeedFloat, AnimatorControllerParameterType.Float);

        warroxSuperAttackTriggerHash = string.IsNullOrWhiteSpace(warroxSuperAttackTriggerName)
            ? 0
            : Animator.StringToHash(warroxSuperAttackTriggerName.Trim());
        hasWarroxSuperAttackTrigger = warroxSuperAttackTriggerHash != 0
            && HasParameter(warroxSuperAttackTriggerHash, AnimatorControllerParameterType.Trigger);

        if (!hasAttackingTrigger && !hasAttackingTriggerAlt && !hasAttackTrigger && !hasAttackTriggerAlt && !warnedAboutMissingAttackingTrigger)
        {
            Debug.LogWarning("Enemy: Animator controller missing attack Trigger. Add Trigger 'attacking'/'attacking1' or 'attack'/'attack2'.");
            warnedAboutMissingAttackingTrigger = true;
        }
    }

    bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == hash && parameter.type == type)
            {
                return true;
            }
        }

        return false;
    }

    void TryRefreshPlayerReferences()
    {
        if (player != null && playerHealth != null) return;

        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
    }

    void EnterPlayerDefeatedMode()
    {
        if (isDead || playerDefeatedMode) return;
        playerDefeatedMode = true;

        pendingDamage = false;
        pendingDamageTimer = 0f;
        attackMoveLockUntil = 0f;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        if (animator == null) return;

        animator.ResetTrigger(AttackingTrigger);
        if (hasWarroxSuperAttackTrigger)
            animator.ResetTrigger(warroxSuperAttackTriggerHash);
        if (hasHitTrigger)
            animator.ResetTrigger(HitTrigger);

        for (int layer = 1; layer < animator.layerCount; layer++)
            animator.SetLayerWeight(layer, 0f);

        if (hasSpeedFloat)
            animator.SetFloat(SpeedFloat, 0f);

        if (useVictoryParameter)
        {
            if (hasVictoryBool)
                animator.SetBool(VictoryBool, true);
            else if (hasDeadBoolParam)
                animator.SetBool(DeadBool, true);
            else if (!string.IsNullOrWhiteSpace(victoryStateFallback))
                TryCrossFadeToState(victoryStateFallback);
        }

        animator.Update(0f);
    }

    void MaintainPlayerDefeatedBehaviour()
    {
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        if (animator != null)
        {
            if (hasSpeedFloat)
                animator.SetFloat(SpeedFloat, 0f);
            for (int layer = 1; layer < animator.layerCount; layer++)
                animator.SetLayerWeight(layer, 0f);
        }

        if (player != null)
            RotateTowardsPlayer();
    }

    public void HitVFX(Vector3 pos)
    {
        if (hitVFX != null)
        {
            GameObject hit = Instantiate(hitVFX, pos, Quaternion.identity);
            Destroy(hit, 2f);
        }
    }

    private void PlayEnemySound(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f) return;
        Vector3 pos = transform.position + Vector3.up * audioEmitHeight;
        AudioSource.PlayClipAtPoint(clip, pos, volume);
    }

    /// <summary>Super-only: two placeholder slots. Falls back to normal <see cref="attackSound"/> if both are empty.</summary>
    private void PlayWarroxSuperAttackSounds()
    {
        bool played = false;
        if (warroxSuperAttackWarroxSound != null && warroxSuperAttackWarroxSoundVolume > 0f)
        {
            PlayEnemySound(warroxSuperAttackWarroxSound, warroxSuperAttackWarroxSoundVolume);
            played = true;
        }

        if (warroxSuperAttackEffectSound != null && warroxSuperAttackEffectSoundVolume > 0f)
        {
            PlayEnemySound(warroxSuperAttackEffectSound, warroxSuperAttackEffectSoundVolume);
            played = true;
        }

        if (!played && attackSound != null)
            PlayEnemySound(attackSound, attackSoundVolume);
    }

    bool WarroxSuperConfigured()
    {
        if (hasWarroxSuperAttackTrigger)
            return true;
        return !string.IsNullOrWhiteSpace(warroxSuperAttackStateName);
    }

    /// <summary>Uses <see cref="warroxSuperAttackTriggerName"/> when present on the Animator; otherwise attack triggers + optional super state crossfade.</summary>
    void PlayWarroxSuperAttackAnimation()
    {
        if (animator == null) return;

        if (hasWarroxSuperAttackTrigger)
        {
            animator.ResetTrigger(warroxSuperAttackTriggerHash);
            animator.SetTrigger(warroxSuperAttackTriggerHash);
            animator.Update(0f);
            return;
        }

        FireRandomAttackTrigger();
        if (!string.IsNullOrWhiteSpace(warroxSuperAttackStateName))
            TryCrossFadeToState(warroxSuperAttackStateName);
    }

    void EvaluateWarroxSuperCharges()
    {
        if (!WarroxSuperConfigured() || maxHealth <= 0.001f)
            return;

        float n = health / maxHealth;
        float t60 = Mathf.Clamp01(warroxSuperThresholdFirstPercent / 100f);
        float t10 = Mathf.Clamp01(warroxSuperThresholdSecondPercent / 100f);

        if (!warroxSuperCrossed60 && n <= t60)
        {
            warroxSuperCrossed60 = true;
            pendingWarroxSuperCount++;
        }

        if (!warroxSuperCrossed10 && n <= t10)
        {
            warroxSuperCrossed10 = true;
            pendingWarroxSuperCount++;
        }
    }

    void FireRandomAttackTrigger()
    {
        if (animator == null) return;
        if (!hasAttackingTrigger && !hasAttackingTriggerAlt && !hasAttackTrigger && !hasAttackTriggerAlt)
            return;

        int trig;
        bool canUseAttackingPair = hasAttackingTrigger || hasAttackingTriggerAlt;
        bool canUseAttackPair = hasAttackTrigger || hasAttackTriggerAlt;

        if (canUseAttackingPair && canUseAttackPair)
        {
            bool useAttackScheme = Random.value < 0.5f;
            bool useAlt = useAttackScheme
                ? (hasAttackTriggerAlt && (!hasAttackTrigger || Random.value < 0.5f))
                : (hasAttackingTriggerAlt && (!hasAttackingTrigger || Random.value < 0.5f));
            trig = useAttackScheme ? (useAlt ? AttackTriggerAlt : AttackTrigger) : (useAlt ? AttackingTriggerAlt : AttackingTrigger);
        }
        else if (canUseAttackingPair)
        {
            bool useAlt = hasAttackingTriggerAlt && (!hasAttackingTrigger || Random.value < 0.5f);
            trig = useAlt ? AttackingTriggerAlt : AttackingTrigger;
        }
        else
        {
            bool useAlt = hasAttackTriggerAlt && (!hasAttackTrigger || Random.value < 0.5f);
            trig = useAlt ? AttackTriggerAlt : AttackTrigger;
        }

        animator.ResetTrigger(trig);
        animator.SetTrigger(trig);
    }

    void SpawnWarroxSuperVfx()
    {
        if (warroxSuperAttackVfxPrefab == null) return;

        Vector3 pos = transform.position + Vector3.up * audioEmitHeight;
        Vector3 flat = player != null ? player.transform.position - pos : transform.forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f)
            flat = transform.forward;
        Quaternion rot = Quaternion.LookRotation(flat.normalized);

        float maxL = warroxSuperVfxMaxLifetimeSeconds > 0f ? warroxSuperVfxMaxLifetimeSeconds : 8f;
        VfxLifetimeUtility.SpawnBurstAndDestroy(
            warroxSuperAttackVfxPrefab,
            pos,
            rot,
            warroxSuperVfxLifetimeSeconds,
            maxL);
    }
}