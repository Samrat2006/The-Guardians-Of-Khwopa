using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Lightweight swarm skeleton: fast movement, long aggro, low health, weak melee, simple animator
/// (<c>Blend</c> float + <c>attack</c> trigger). No death state — object is destroyed immediately on death.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SmallSkeletonEnemyAI : MonoBehaviour
{
    public event System.Action<SmallSkeletonEnemyAI> OnDied;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform player;
    [SerializeField] private NavMeshAgent agent;

    [Header("Health")]
    [SerializeField] private float health = 18f;

    [Header("Movement")]
    [Tooltip("NavMeshAgent speed — keep high for a fast runner.")]
    [SerializeField] private float moveSpeed = 6.5f;
    [Tooltip("How fast the model turns toward the player.")]
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Ranges")]
    [SerializeField] private float aggroRange = 22f;
    [SerializeField] private float attackRange = 1.45f;
    [Tooltip("Extra distance to reduce walk/attack flicker at the edge.")]
    [SerializeField] private float attackRangeHysteresis = 0.4f;

    [Header("Combat")]
    [SerializeField] private float attackDamage = 5f;
    [SerializeField] private float attackCooldown = 1.65f;
    [Tooltip("Seconds after triggering <c>attack</c> before the hit is tested (match punch contact).")]
    [SerializeField] private float attackHitDelay = 0.32f;
    [SerializeField] private float attackForwardReach = 1.85f;
    [SerializeField] private float attackHeight = 1f;
    [SerializeField] private LayerMask playerHitMask = ~0;

    [Header("Animator (SmallSkeleton.controller)")]
    [SerializeField] private string blendParameterName = "Blend";
    [SerializeField] private string attackTriggerName = "attack";
    [Tooltip("Base Layer state name for the punch clip — must match the state node in the Animator (not the .fbx file name). Try \"punch\" or \"mutant punch\".")]
    [SerializeField] private string attackStateName = "punch";
    [Tooltip("If true and the state exists, CrossFade to Attack State Name; otherwise uses the Attack Trigger only.")]
    [SerializeField] private bool preferCrossFadeToAttackState = true;
    [Tooltip("0 = normalize blend by NavMeshAgent.speed (recommended). Set only if you need a custom scale.")]
    [SerializeField] private float blendSpeedNormalizeOverride = 0f;

    [Header("Death")]
    [Tooltip("Optional one-shot VFX spawned at feet/root before the object is destroyed.")]
    [SerializeField] private GameObject deathVfxPrefab;

    [Header("Debug")]
    [Tooltip("If off, suppress setup warnings that can spam the Console when many skeletons spawn.")]
    [SerializeField] private bool logSetupWarnings = false;

    private static bool s_warnedMissingPunchStateGlobal;
    private static bool s_warnedMissingBlendGlobal;
    private static bool s_warnedMissingAttackTriggerGlobal;

    private int blendHash;
    private int attackHash;
    private bool warnedMissingAttackParam;

    private bool dead;
    private float nextAttackTime;
    private bool attackRoutineRunning;
    private bool inAttackRangeLatch;
    private bool missingAnimatorAbort;

    private void Reset()
    {
        NavMeshAgent a = GetComponent<NavMeshAgent>();
        if (a != null)
        {
            a.speed = moveSpeed;
            a.stoppingDistance = attackRange * 0.85f;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Keeps Inspector references filled so Animator/Agent aren't left None by mistake.
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (GetComponent<Terrain>() != null)
        {
            if (logSetupWarnings)
                Debug.LogWarning(
                    "SmallSkeletonEnemyAI should NOT be on a Terrain. Remove this component from Terrain and add it only to your SmallSkeleton enemy (the GameObject that has the Animator + collider).",
                    this);
        }
    }
#endif

    private void Awake()
    {
        blendHash = Animator.StringToHash(blendParameterName);
        attackHash = Animator.StringToHash(attackTriggerName);

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        if (agent != null)
        {
            agent.updateRotation = false;
            agent.speed = moveSpeed;
            agent.stoppingDistance = Mathf.Max(0.05f, attackRange * 0.85f);
        }

        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }

        if (animator == null)
        {
            missingAnimatorAbort = true;
            bool likelyMisplaced = GetComponent<Terrain>() != null
                || name.IndexOf("terrain", StringComparison.OrdinalIgnoreCase) >= 0;

            if (likelyMisplaced)
            {
                Debug.LogError(
                    $"SmallSkeletonEnemyAI on '{name}': No Animator here — this component is almost certainly on the WRONG object.\n" +
                    "→ Remove Small Skeleton Enemy AI (and NavMeshAgent if you added it) from Terrain / ground.\n" +
                    "→ Add Small Skeleton Enemy AI only to the SmallSkeleton prefab/instance that has the Animator + Capsule Collider + SmallSkeleton.controller.",
                    this);
            }
            else
            {
                Debug.LogError(
                    $"SmallSkeletonEnemyAI on '{name}': No Animator on this object or children. Add an Animator with your controller to this enemy, or parent the mesh so the Animator is found.",
                    this);
            }

            enabled = false;
            return;
        }

        if (agent == null)
            Debug.LogError($"SmallSkeletonEnemyAI on '{name}': No NavMeshAgent. Add one or bake NavMesh.", this);

        ValidateAnimatorParameters();
    }

    private void ValidateAnimatorParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        bool hasBlend = HasParam(blendParameterName, AnimatorControllerParameterType.Float);
        bool hasAttackTrig = HasParam(attackTriggerName, AnimatorControllerParameterType.Trigger);
        if (!hasBlend && blendParameterName.Length > 0 && logSetupWarnings && !s_warnedMissingBlendGlobal)
        {
            s_warnedMissingBlendGlobal = true;
            Debug.LogWarning($"SmallSkeletonEnemyAI: Animator has no Float parameter '{blendParameterName}'. Movement blend won't work.", this);
        }
        if (!hasAttackTrig && attackTriggerName.Length > 0 && logSetupWarnings && !s_warnedMissingAttackTriggerGlobal)
        {
            s_warnedMissingAttackTriggerGlobal = true;
            Debug.LogWarning($"SmallSkeletonEnemyAI: Animator has no Trigger '{attackTriggerName}'. Rely on Attack State Name + CrossFade or fix the controller.", this);
        }

        if (preferCrossFadeToAttackState)
        {
            foreach (string s in GetAttackStateCandidates())
            {
                if (string.IsNullOrEmpty(s)) continue;
                if (animator.HasState(0, Animator.StringToHash(s)))
                    return;
            }

            if (!warnedMissingAttackParam)
            {
                warnedMissingAttackParam = true;
                if (logSetupWarnings && !s_warnedMissingPunchStateGlobal)
                {
                    s_warnedMissingPunchStateGlobal = true;
                    Debug.LogWarning(
                        "SmallSkeletonEnemyAI: No matching punch state on Base Layer (tried: punch, mutant punch, etc.). " +
                        "Fix your SmallSkeleton animator state name OR set Attack State Name to match the state node exactly.",
                        this);
                }
            }
        }
    }

    private bool HasParam(string paramName, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.type == type && p.name == paramName)
                return true;
        }

        return false;
    }

    private void Update()
    {
        if (missingAnimatorAbort) return;
        if (dead) return;
        if (DialogueManager.IsBlockingGameplay) return;
        if (player == null || agent == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        FacePlayer();

        float enterRange = attackRange;
        float exitRange = attackRange + Mathf.Max(0f, attackRangeHysteresis);
        if (!inAttackRangeLatch)
            inAttackRangeLatch = distance <= enterRange;
        else
            inAttackRangeLatch = distance <= exitRange;

        bool inAttackRange = inAttackRangeLatch;

        if (distance <= aggroRange)
        {
            if (inAttackRange)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                if (agent.hasPath) agent.ResetPath();

                if (!attackRoutineRunning && Time.time >= nextAttackTime)
                    StartCoroutine(AttackRoutine());
            }
            else
            {
                agent.isStopped = false;
                agent.SetDestination(player.position);
            }
        }
        else
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        UpdateBlendParameter();
    }

    private void UpdateBlendParameter()
    {
        if (animator == null) return;
        if (!animator.isActiveAndEnabled) return;
        if (agent == null) return;

        // Blend tree 0 = idle, 1 = run. Normalize by agent.speed (or override) so full speed reaches Blend = 1.
        float refSpeed = blendSpeedNormalizeOverride > 0.01f ? blendSpeedNormalizeOverride : agent.speed;
        refSpeed = Mathf.Max(0.01f, refSpeed);

        Vector3 v = agent.velocity;
        v.y = 0f;
        float planar = v.magnitude;

        // NavMeshAgent.velocity often lags or stays ~0 for a frame while a path is valid; desiredVelocity matches intent better.
        if (!agent.isStopped && agent.hasPath && planar < 0.12f)
        {
            Vector3 want = agent.desiredVelocity;
            want.y = 0f;
            planar = Mathf.Max(planar, want.magnitude);
        }

        float blend01 = Mathf.Clamp01(planar / refSpeed);
        animator.SetFloat(blendHash, blend01);
    }

    private void FacePlayer()
    {
        if (player == null) return;
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(to.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
    }

    private IEnumerator AttackRoutine()
    {
        attackRoutineRunning = true;
        nextAttackTime = Time.time + attackCooldown;

        PlayAttackAnimation();

        yield return new WaitForSeconds(attackHitDelay);
        TryHitPlayer();

        attackRoutineRunning = false;
    }

    private void PlayAttackAnimation()
    {
        if (animator == null || !animator.isActiveAndEnabled) return;
        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError($"SmallSkeletonEnemyAI on '{name}': Animator has no Controller assigned.", this);
            return;
        }

        // 1) CrossFade (smooth)
        if (preferCrossFadeToAttackState)
        {
            foreach (string stateName in GetAttackStateCandidates())
            {
                if (string.IsNullOrEmpty(stateName)) continue;
                int h = Animator.StringToHash(stateName);
                if (!animator.HasState(0, h)) continue;
                animator.CrossFadeInFixedTime(stateName, 0.1f, 0, 0f);
                animator.Update(0f);
                return;
            }
        }

        // 2) Play — forces the clip even when CrossFade transitions are picky
        foreach (string stateName in GetAttackStateCandidates())
        {
            if (string.IsNullOrEmpty(stateName)) continue;
            int h = Animator.StringToHash(stateName);
            if (!animator.HasState(0, h)) continue;
            animator.Play(h, 0, 0f);
            animator.Update(0f);
            return;
        }

        // 3) Trigger (Any State -> punch) — needs valid controller transitions
        animator.ResetTrigger(attackHash);
        animator.SetTrigger(attackHash);
        animator.Update(0f);
    }

    /// <summary>Clip file names often differ from Animator state names — we try several common names.</summary>
    private IEnumerable<string> GetAttackStateCandidates()
    {
        var seen = new HashSet<string>();
        string[] fallbacks = { attackStateName, "punch", "Punch", "mutant punch", "Mutant Punch", "punch 0" };
        foreach (string s in fallbacks)
        {
            if (string.IsNullOrEmpty(s)) continue;
            if (seen.Add(s))
                yield return s;
        }
    }

    private void TryHitPlayer()
    {
        if (dead || player == null) return;

        Vector3 origin = transform.position + Vector3.up * attackHeight;
        Vector3 forward = transform.forward;
        if (Physics.Raycast(origin, forward, out RaycastHit hit, attackForwardReach, playerHitMask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<PlayerHealth>() != null)
            {
                PlayerHealth ph = hit.collider.GetComponentInParent<PlayerHealth>();
                if (ph != null && !ph.IsDead)
                    ph.TakeDamage(Mathf.RoundToInt(attackDamage));
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (missingAnimatorAbort) return;
        if (dead) return;
        if (amount <= 0f) return;
        health -= amount;
        if (health <= 0f)
            Die();
    }

    private void Die()
    {
        if (dead) return;
        dead = true;
        OnDied?.Invoke(this);

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }

        if (animator != null)
            animator.enabled = false;

        if (deathVfxPrefab != null)
            Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Vector3 o = transform.position + Vector3.up * attackHeight;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(o, transform.forward * attackForwardReach);
    }
#endif
}
