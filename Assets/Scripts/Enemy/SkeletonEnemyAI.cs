using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Professional-ish, self-contained enemy AI for the Skeleton Animator Controller.
///
/// Animator parameters used (must exist in the controller):
/// - float  speed
/// - bool   isDead
/// - bool   isVictory
/// - trigger attack1
/// - trigger attack2
/// - trigger attack3
///
/// Design:
/// - NavMeshAgent drives movement, and we feed agent velocity into "speed".
/// - When close enough, we stop and randomly trigger one of the 3 attacks.
/// - Death interrupts everything (isDead = true) and never exits.
/// - When player dies, we set isVictory = true and stop the agent.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SkeletonEnemyAI : MonoBehaviour
{
    /// <summary>Fires exactly once when this skeleton dies.</summary>
    public event System.Action<SkeletonEnemyAI> OnDied;
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform player;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private NavMeshAgent agent;

    [Header("Ranges")]
    [SerializeField] private float aggroRange = 10f;
    [SerializeField] private float attackRange = 2.0f;
    [Tooltip("Extra distance to prevent walk/attack jitter at the edge.")]
    [SerializeField] private float attackRangeHysteresis = 0.35f;

    [Header("Combat")]
    [SerializeField] private float attackCooldown = 1.8f;
    [SerializeField] private float attackMoveLockSeconds = 0.9f;

    [Header("Health")]
    [SerializeField] private float health = 50f;

    [Header("Animator — state names (must match your controller states)")]
    [Tooltip("Base Layer state for death clip. Default matches Assets/Anim/SkeletonEnemy.controller (\"Dead\"). If your state is named e.g. \"Die\", set it here.")]
    [SerializeField] private string deathAnimatorStateName = "Dead";
    [Tooltip("Base Layer state for victory / taunt when the player dies.")]
    [SerializeField] private string victoryAnimatorStateName = "Victory";

    // Animator parameter hashes (fast + avoids typos at runtime)
    private static readonly int SpeedHash = Animator.StringToHash("speed");
    private static readonly int IsDeadHash = Animator.StringToHash("isDead");
    private static readonly int IsVictoryHash = Animator.StringToHash("isVictory");
    private static readonly int Attack1Hash = Animator.StringToHash("attack1");
    private static readonly int Attack2Hash = Animator.StringToHash("attack2");
    private static readonly int Attack3Hash = Animator.StringToHash("attack3");

    private bool dead;
    private bool victory;
    private float nextAttackTime;
    private float moveLockedUntil;
    private bool inAttackRangeLatch;
    private int deathStateHash;
    private int victoryStateHash;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Prevent attack clips from pushing the character underground via root motion.
        if (animator != null)
            animator.applyRootMotion = false;

        // We'll rotate manually (FacePlayer). Let NavMeshAgent only drive planar movement.
        if (agent != null)
        {
            agent.updateRotation = false;
            // Keep whatever offset the prefab/scene currently uses (prevents sudden vertical snapping).
            agent.baseOffset = agent.baseOffset;
        }

        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }

        if (playerHealth == null && player != null)
            playerHealth = player.GetComponent<PlayerHealth>();

        deathStateHash = Animator.StringToHash(deathAnimatorStateName);
        victoryStateHash = Animator.StringToHash(victoryAnimatorStateName);
    }

    private void Update()
    {
        if (dead) return;
        if (DialogueManager.IsBlockingGameplay) return;

        // Player defeated → victory pose (stop everything).
        if (playerHealth != null && playerHealth.IsDead)
        {
            EnterVictoryMode();
            return;
        }

        if (player == null || agent == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        bool locked = Time.time < moveLockedUntil;

        // Always face the player for a cleaner look.
        FacePlayer();

        float enterRange = attackRange;
        float exitRange = attackRange + Mathf.Max(0f, attackRangeHysteresis);
        if (!inAttackRangeLatch)
            inAttackRangeLatch = distance <= enterRange;
        else
            inAttackRangeLatch = distance <= exitRange;

        bool inAttackRange = inAttackRangeLatch;

        if (locked || inAttackRange)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            // Avoid ResetPath spam; it can cause agent to snap/warp on some setups.
            if (agent.hasPath) agent.ResetPath();

            // Attack only when not locked and cooldown is ready.
            if (!locked && Time.time >= nextAttackTime)
            {
                TriggerRandomAttack();
                nextAttackTime = Time.time + attackCooldown;
                moveLockedUntil = Time.time + attackMoveLockSeconds;
            }
        }
        else if (distance <= aggroRange)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        // Feed animator "speed" (normalized to agent speed).
        if (animator != null)
        {
            float speed01 = agent.speed > 0.01f ? agent.velocity.magnitude / agent.speed : 0f;
            animator.SetFloat(SpeedHash, speed01);
        }
    }

    private void FacePlayer()
    {
        if (player == null) return;
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(to.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 10f * Time.deltaTime);
    }

    private void TriggerRandomAttack()
    {
        if (animator == null) return;

        // Random selection: 1,2,3
        int pick = Random.Range(0, 3);
        int trig = pick == 0 ? Attack1Hash : (pick == 1 ? Attack2Hash : Attack3Hash);

        animator.ResetTrigger(trig);
        animator.SetTrigger(trig);
    }

    public void TakeDamage(float amount)
    {
        if (dead) return;
        if (amount <= 0f) return;
        health -= amount;
        if (health <= 0f)
            Die();
    }

    public void Die()
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
        {
            // Highest priority: death. Also disable root motion to avoid sinking/floating.
            animator.applyRootMotion = false;
            animator.SetBool(IsDeadHash, true);
            TryPlayBaseLayerState(deathStateHash, deathAnimatorStateName);
            animator.Update(0f);
        }
    }

    private void EnterVictoryMode()
    {
        if (victory) return;
        victory = true;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (animator != null)
        {
            animator.SetBool(IsVictoryHash, true);
            animator.SetFloat(SpeedHash, 0f);
            TryPlayBaseLayerState(victoryStateHash, victoryAnimatorStateName);
            animator.Update(0f);
        }
    }

    /// <summary>
    /// <see cref="Animator.SetBool"/> alone can fail to enter a state if transitions fight (e.g. exit-time back to blend).
    /// This forces the clip on Base Layer when the state exists.
    /// </summary>
    private void TryPlayBaseLayerState(int shortNameHash, string stateNameForWarning)
    {
        if (animator == null || !animator.isActiveAndEnabled) return;
        if (!animator.HasState(0, shortNameHash))
        {
            Debug.LogWarning(
                $"SkeletonEnemyAI on '{name}': Base Layer has no animator state named '{stateNameForWarning}'. " +
                "Assign the correct Death / Victory Animator State Name to match your controller (e.g. \"Dead\" or \"Die\").",
                this);
            return;
        }

        animator.Play(shortNameHash, 0, 0f);
    }
}

