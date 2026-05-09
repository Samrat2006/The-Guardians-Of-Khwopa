using System.Collections;
using StarterAssets;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class PlayerCombat : MonoBehaviour
{
    [Header("Combat")]
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private float attackCooldown = 0.8f;
    [SerializeField] private float attackLockDuration = 0.45f;
    [SerializeField] private float hitDelay = 0.1f;
    [SerializeField] private LayerMask enemyLayerMask = ~0;
    [SerializeField] private bool useAnimationEventsForHit = false;

    [Header("Super attack (meter full + R)")]
    [SerializeField] private PlayerSuperMeter superMeter;
    [SerializeField] private KeyCode superAttackKey = KeyCode.R;
    [Tooltip("Must match your Animator (Trigger recommended: Any State → super clip).")]
    [SerializeField] private string superAttackTriggerParameter = "superattack";
    [Tooltip("Optional: random super variants by state name (Base Layer). If empty, only the trigger above is used.")]
    [SerializeField] private string[] superAttackStateNames;
    [SerializeField] private float superAttackDamage = 18f;
    [SerializeField] private float superAttackRange = 3.2f;
    [SerializeField] private float superHitDelay = 0.22f;
    [SerializeField] private float superAttackLockDuration = 0.95f;
    [Tooltip("If on, call AnimEvent_SuperAttackHit from the super clip at the strike frame instead of timed hit.")]
    [SerializeField] private bool useAnimationEventsForSuperHit = false;

    [Header("Optional Attack State Names (Base Layer)")]
    [SerializeField] private string[] attackStateNames =
    {
        "Erika Archer@Standing Melee Punch",
        "Erika Archer@Side Kick"
    };

    [Header("Audio")]
    [Tooltip("Played when a melee attack starts (punch/kick wind-up).")]
    [SerializeField] private AudioClip attackStartSound;
    [SerializeField] [Range(0f, 1f)] private float attackStartVolume = 1f;
    [Tooltip("Optional: played when the attack ray hits an enemy.")]
    [SerializeField] private AudioClip attackHitEnemySound;
    [SerializeField] [Range(0f, 1f)] private float attackHitEnemyVolume = 1f;

    [Header("Debug")]
    [Tooltip("Turn off to prevent Console spam (improves editor FPS).")]
    [SerializeField] private bool debugLogs = false;

    private Animator animator;
    private ThirdPersonController thirdPersonController;
    private PlayerHealth playerHealth;
    private PlayerBowAim playerBowAim;
    private PlayerBowShoot playerBowShoot;
    private bool isAttacking;
    private float nextAttackTime;
    private bool hitAlreadyAppliedThisAttack;
    private bool warnedAboutMissingAttackState;
    private bool hasAttackingTrigger;
    private bool hasIsAttackingBool;
    private bool hasAttackVariantInt;
    private bool warnedAboutMissingAttackingTrigger;
    private bool isSuperAttacking;
    private bool hitAlreadyAppliedThisSuper;
    private int superAttackTriggerHash;
    private bool hasSuperAttackTrigger;
    private bool hasSuperAttackBool;
    private bool warnedMissingSuperMeter;
    private bool warnedMissingSuperAnim;

    /// <summary>True while a normal or super melee attack lockout is playing (used by camera rig).</summary>
    public bool IsAttacking => isAttacking || isSuperAttacking;

    private static readonly int AttackingTrigger = Animator.StringToHash("attacking");
    private static readonly int IsAttackingBool = Animator.StringToHash("isattacking");
    private static readonly int AttackVariantIntHash = Animator.StringToHash("attackVariant");

    private void Awake()
    {
        animator = ResolveAnimator();
        thirdPersonController = GetComponent<ThirdPersonController>();
        playerHealth = GetComponent<PlayerHealth>();
        playerBowAim = GetComponent<PlayerBowAim>();
        playerBowShoot = GetComponent<PlayerBowShoot>();
        if (superMeter == null)
            superMeter = GetComponent<PlayerSuperMeter>();
        if (superMeter == null)
            superMeter = GetComponentInParent<PlayerSuperMeter>();
        superAttackTriggerHash = Animator.StringToHash(superAttackTriggerParameter);
        CacheAnimatorParameters();
        CacheSuperAttackParameters();

        if (enemyLayerMask.value == 0)
        {
            if (debugLogs)
                Debug.LogWarning(
                "PlayerCombat: Enemy Layer Mask is set to Nothing — melee raycasts never hit anything. " +
                "Setting to all layers. Also ensure targets are NOT on the Ignore Raycast layer (Unity ignores those for Physics.Raycast).",
                this);
            enemyLayerMask = (LayerMask)(-1);
        }
    }

    private void Update()
    {
        if (DialogueManager.IsBlockingGameplay) return;
        if (playerHealth != null && playerHealth.IsDead) return;
        if (playerBowAim != null && playerBowAim.IsAiming) return;
        if (playerBowShoot != null && playerBowShoot.IsShooting) return;

        if (Input.GetMouseButtonDown(0))
        {
            TryAttack();
        }

        if (Input.GetKeyDown(superAttackKey))
        {
            TrySuperAttack();
        }
    }

    private void TryAttack()
    {
        if (playerHealth != null && playerHealth.IsDead) return;
        if (playerBowAim != null && playerBowAim.IsAiming) return;
        if (playerBowShoot != null && playerBowShoot.IsShooting) return;
        if (isAttacking || isSuperAttacking) return;
        if (Time.time < nextAttackTime) return;
        if (animator == null) return;

        StartCoroutine(AttackRoutine());
    }

    private void TrySuperAttack()
    {
        if (playerHealth != null && playerHealth.IsDead) return;
        if (DialogueManager.IsBlockingGameplay) return;
        if (playerBowAim != null && playerBowAim.IsAiming) return;
        if (playerBowShoot != null && playerBowShoot.IsShooting) return;
        if (isAttacking || isSuperAttacking) return;
        if (animator == null) return;

        if (superMeter == null)
        {
            if (!warnedMissingSuperMeter)
            {
                warnedMissingSuperMeter = true;
                Debug.LogWarning("PlayerCombat: Add PlayerSuperMeter to the player (or assign the field) to use super attack (R).", this);
            }
            return;
        }

        if (!superMeter.IsFull) return;
        if (!superMeter.TryConsumeFullMeter()) return;

        StartCoroutine(SuperAttackRoutine());
    }

    private IEnumerator SuperAttackRoutine()
    {
        isSuperAttacking = true;
        hitAlreadyAppliedThisSuper = false;
        nextAttackTime = Time.time + attackCooldown;

        if (thirdPersonController != null)
            thirdPersonController.enabled = false;

        bool playedAnim = TryPlaySuperAttackAnimation();
        if (!playedAnim && !warnedMissingSuperAnim)
        {
            warnedMissingSuperAnim = true;
            Debug.LogWarning(
                $"PlayerCombat: No super animation fired. Add Trigger '{superAttackTriggerParameter}' to the Animator and/or fill Super Attack State Names.",
                this);
        }

        PlayAttackSound(attackStartSound, attackStartVolume);

        if (!useAnimationEventsForSuperHit)
        {
            yield return new WaitForSeconds(superHitDelay);
            TryApplySuperHit();
        }

        yield return new WaitForSeconds(superAttackLockDuration);

        if (thirdPersonController != null)
            thirdPersonController.enabled = true;

        if (hasSuperAttackBool && animator != null)
            animator.SetBool(superAttackTriggerHash, false);

        isSuperAttacking = false;
    }

    /// <summary>Animation Event: call from super clip at hit frame when Use Animation Events For Super Hit is on.</summary>
    public void AnimEvent_SuperAttackHit()
    {
        if (!useAnimationEventsForSuperHit) return;
        TryApplySuperHit();
    }

    private void TryApplySuperHit()
    {
        if (!isSuperAttacking) return;
        if (hitAlreadyAppliedThisSuper) return;
        if (animator == null) return;

        hitAlreadyAppliedThisSuper = true;
        PerformMeleeHitRaycasts(superAttackDamage, superAttackRange);
    }

    private bool TryPlaySuperAttackAnimation()
    {
        if (animator == null) return false;

        if (superAttackStateNames != null && superAttackStateNames.Length > 0)
        {
            int idx = Random.Range(0, superAttackStateNames.Length);
            string stateName = superAttackStateNames[idx];
            if (!string.IsNullOrWhiteSpace(stateName))
            {
                int stateHash = Animator.StringToHash(stateName);
                int layer = FindLayerForState(stateHash);
                if (layer >= 0)
                {
                    if (hasSuperAttackBool)
                        animator.SetBool(superAttackTriggerHash, true);
                    animator.CrossFadeInFixedTime(stateName, 0.08f, layer);
                    animator.Update(0f);
                    return true;
                }
            }
        }

        if (hasSuperAttackTrigger)
        {
            animator.ResetTrigger(superAttackTriggerHash);
            animator.SetTrigger(superAttackTriggerHash);
            animator.Update(0f);
            return true;
        }

        if (hasSuperAttackBool)
        {
            animator.SetBool(superAttackTriggerHash, true);
            animator.Update(0f);
            return true;
        }

        return false;
    }

    private void CacheSuperAttackParameters()
    {
        hasSuperAttackTrigger = HasParameter(superAttackTriggerHash, AnimatorControllerParameterType.Trigger);
        hasSuperAttackBool = !hasSuperAttackTrigger
            && HasParameter(superAttackTriggerHash, AnimatorControllerParameterType.Bool);
    }

    private IEnumerator AttackRoutine()
    {
        if (playerBowAim != null && playerBowAim.IsAiming)
            yield break;

        isAttacking = true;
        hitAlreadyAppliedThisAttack = false;
        nextAttackTime = Time.time + attackCooldown;

        if (hasIsAttackingBool)
        {
            animator.SetBool(IsAttackingBool, true);
        }
        if (hasAttackingTrigger)
        {
            animator.ResetTrigger(AttackingTrigger);
            animator.SetTrigger(AttackingTrigger);
        }
        if (debugLogs) Debug.Log("Player Attacking");

        if (thirdPersonController != null)
        {
            thirdPersonController.enabled = false;
        }

        TryPlayRandomAttackState();

        PlayAttackSound(attackStartSound, attackStartVolume);

        if (!useAnimationEventsForHit)
        {
            yield return new WaitForSeconds(hitDelay);
            TryApplyHit();
        }

        yield return new WaitForSeconds(attackLockDuration);

        if (hasIsAttackingBool)
        {
            animator.SetBool(IsAttackingBool, false);
        }

        if (thirdPersonController != null)
        {
            thirdPersonController.enabled = true;
        }

        isAttacking = false;
    }

    // Animation Event hook (recommended): call this from Punch/Kick clip at the hit frame.
    public void AnimEvent_AttackHit()
    {
        if (!useAnimationEventsForHit) return;
        TryApplyHit();
    }

    private void TryPlayRandomAttackState()
    {
        if (attackStateNames == null || attackStateNames.Length == 0)
        {
            return;
        }

        int selectedIndex = Random.Range(0, attackStateNames.Length);
        string selectedState = attackStateNames[selectedIndex];
        if (string.IsNullOrWhiteSpace(selectedState))
        {
            return;
        }

        // Most reliable random selection: drive Animator transitions using an int parameter.
        // If you don't have 'attackVariant' in your Animator controller, we fall back to crossfading by state name.
        if (hasAttackVariantInt)
        {
            // Convention: first entry = Punch, second entry = Kick.
            int variant = selectedIndex == 0 ? 0 : 1;
            animator.SetInteger(AttackVariantIntHash, variant);
            if (debugLogs) Debug.Log(variant == 0 ? "Player attack: Punch" : "Player attack: Kick");
            return;
        }

        int stateHash = Animator.StringToHash(selectedState);
        int foundLayer = FindLayerForState(stateHash);
        if (foundLayer >= 0)
        {
            animator.CrossFadeInFixedTime(selectedState, 0.05f, foundLayer);
            if (debugLogs) Debug.Log(selectedIndex == 0 ? "Player attack: Punch" : "Player attack: Kick");
            return;
        }

        if (!warnedAboutMissingAttackState)
        {
            Debug.LogWarning($"PlayerCombat: Attack state not found on any layer: {selectedState}", this);
            warnedAboutMissingAttackState = true;
        }
    }

    private void TryApplyHit()
    {
        if (!isAttacking) return;
        if (hitAlreadyAppliedThisAttack) return;
        if (animator == null) return;

        hitAlreadyAppliedThisAttack = true;
        PerformMeleeHitRaycasts(attackDamage, attackRange);
    }

    private void PerformMeleeHitRaycasts(float damage, float range)
    {
        Vector3 rayDirection = transform.forward;
        LayerMask mask = enemyLayerMask;
        QueryTriggerInteraction q = QueryTriggerInteraction.Collide;

        // Two heights: chest (humanoids) and lower (small enemies / slope differences).
        Vector3[] origins =
        {
            transform.position + Vector3.up * 1.2f,
            transform.position + Vector3.up * 0.55f
        };

        foreach (Vector3 rayOrigin in origins)
        {
            Color dbg = damage > attackDamage ? Color.magenta : Color.yellow;
            Debug.DrawRay(rayOrigin, rayDirection * range, dbg, 0.5f);

            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, rayDirection, range, mask, q);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (EnemyHitUtility.TryApplyDamage(hit.collider, damage))
                {
                    if (debugLogs) Debug.Log(damage > attackDamage ? "Super hit registered" : "Hit Registered");
                    PlayAttackSound(attackHitEnemySound, attackHitEnemyVolume);
                    return;
                }
            }
        }

        Vector3 sphereOrigin = transform.position + Vector3.up * 0.95f;
        float sphereRadius = damage > attackDamage ? 0.48f : 0.38f;
        float castLen = Mathf.Max(0.05f, range - sphereRadius);
        if (Physics.SphereCast(sphereOrigin, sphereRadius, rayDirection, out RaycastHit sphereHit, castLen, mask, q))
        {
            if (EnemyHitUtility.TryApplyDamage(sphereHit.collider, damage))
            {
                if (debugLogs) Debug.Log(damage > attackDamage ? "Super hit (sphere)" : "Hit Registered (sphere melee)");
                PlayAttackSound(attackHitEnemySound, attackHitEnemyVolume);
                return;
            }
        }

        if (debugLogs) Debug.Log(damage > attackDamage ? "Super attack missed" : "Player attack missed");
    }

    private Animator ResolveAnimator()
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

    private void CacheAnimatorParameters()
    {
        hasAttackingTrigger = HasParameter(AttackingTrigger, AnimatorControllerParameterType.Trigger);
        hasIsAttackingBool = HasParameter(IsAttackingBool, AnimatorControllerParameterType.Bool);
        hasAttackVariantInt = HasParameter(AttackVariantIntHash, AnimatorControllerParameterType.Int);

        if (!hasAttackingTrigger && !warnedAboutMissingAttackingTrigger)
        {
            Debug.LogWarning("PlayerCombat: Animator controller missing Trigger 'attacking'. Add it to the Animator Controller on PlayerArmature.");
            warnedAboutMissingAttackingTrigger = true;
        }
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
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

    private int FindLayerForState(int stateHash)
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

    private void PlayAttackSound(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f) return;
        Vector3 pos = transform.position + Vector3.up * 1.1f;
        AudioSource.PlayClipAtPoint(clip, pos, volume);
    }
}
