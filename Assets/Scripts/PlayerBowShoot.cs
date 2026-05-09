using System.Collections;
using StarterAssets;
using UnityEngine;

/// <summary>
/// While <see cref="PlayerBowAim.IsAiming"/>, left click plays the bow <b>Launch</b> animation (trigger <c>attack2</c>),
/// then arrow/raycast. Returns to Aim Overdraw if still aiming. Outside aim mode, <see cref="PlayerCombat"/> handles LMB.
/// </summary>
[DefaultExecutionOrder(-45)]
public class PlayerBowShoot : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerBowAim playerBowAim;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private ThirdPersonController thirdPersonController;

    [Header("Bow attack")]
    [SerializeField] private float shootCooldown = 0.65f;
    [SerializeField] private float crossFadeIn = 0.08f;
    [Tooltip("Optional. If null, uses raycast from screen center.")]
    [SerializeField] private GameObject arrowPrefab;
    [Tooltip("Optional. If set, arrow spawns from this Transform (recommended: an empty child on the bow).")]
    [SerializeField] private Transform arrowSpawnPoint;
    [SerializeField] private float arrowSpawnForwardOffset = 0.6f;
    [SerializeField] private float bowRaycastRange = 80f;

    [Header("Effective range")]
    [Tooltip("If on, shots fail when the aim point is too close or too far (distance from arrow spawn to crosshair hit / max range point).")]
    [SerializeField] private bool limitEffectiveRange = true;
    [Tooltip("Below this distance (spawn → aim point), the bow will not fire. Use ~0 to disable minimum.")]
    [SerializeField] private float minEffectiveDistance = 2f;
    [Tooltip("Beyond this distance, the bow will not fire. Use 0 for no max (only limited by Bow Raycast Range for where you aim).")]
    [SerializeField] private float maxEffectiveDistance = 40f;

    [SerializeField] private float bowDamage = 14f;
    [Tooltip("Matches ArrowProjectile flight speed when using a prefab.")]
    [SerializeField] private float arrowProjectileSpeed = 45f;
    [Tooltip("If false, arrow flies straight (no drop).")]
    [SerializeField] private bool arrowUseGravity = false;
    [SerializeField] private LayerMask bowHitMask = ~0;
    [Tooltip("Seconds from launching the bow animation until the arrow should be released (match your Launch curve time).")]
    [SerializeField] private float recoilReleaseDelay = 0.06f;
    [SerializeField] private float stateWaitTimeout = 3f;
    [Header("Animation (bow shoot)")]
    [Tooltip("Animator Trigger that starts the Launch/throw animation.")]
    [SerializeField] private string shootTriggerName = "attack2";
    [Tooltip("State name on Base Layer that plays when shooting (must exist).")]
    [SerializeField] private string shootStateName = "Launch";

    [Header("VFX")]
    [Tooltip("Optional burst at the bow (Particle System prefab, muzzle flash, etc.). Spawned at arrow spawn, aimed with the shot.")]
    [SerializeField] private GameObject shootVfxPrefab;
    [Tooltip("Seconds before the VFX root is removed. 0 = auto-estimate from all ParticleSystems on the prefab.")]
    [SerializeField] private float shootVfxLifetimeSeconds = 0f;
    [Tooltip("Never keep VFX longer than this (loops / bad curves can otherwise delay Destroy forever). Uses real time, not timeScale.")]
    [SerializeField] private float shootVfxMaxLifetimeSeconds = 5f;

    private static readonly int IsAimingHash = Animator.StringToHash("isAiming");
    private static readonly int PunchKickHash = Animator.StringToHash("punchKick");
    private int shootTriggerHash;
    private int shootStateHash;

    private float nextShootTime;
    private bool shooting;
    private ArrowInventory arrows;
    private bool warnedMissingShootState;

    public bool IsShooting => shooting;

    private void Awake()
    {
        shootTriggerHash = Animator.StringToHash(shootTriggerName);
        shootStateHash = Animator.StringToHash(shootStateName);

        if (animator == null)
            animator = ResolvePlayerAnimator();
        if (playerBowAim == null)
            playerBowAim = GetComponent<PlayerBowAim>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (thirdPersonController == null)
            thirdPersonController = GetComponent<ThirdPersonController>();

        arrows = GetComponent<ArrowInventory>();
        if (arrows == null)
            arrows = GetComponentInChildren<ArrowInventory>(true);
        if (arrows == null)
            arrows = gameObject.AddComponent<ArrowInventory>();
    }

    private Animator ResolvePlayerAnimator()
    {
        // Avoid accidentally grabbing an Animator on bow/arrow props (those often don't have the player controller states).
        Animator own = GetComponent<Animator>();
        if (own != null && own.runtimeAnimatorController != null)
            return own;

        Animator[] candidates = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            Animator a = candidates[i];
            if (a == null || a.runtimeAnimatorController == null) continue;

            // Prefer animators that actually contain the shoot state on Base Layer.
            if (a.HasState(0, shootStateHash))
                return a;
        }

        // Fallback: first valid animator.
        for (int i = 0; i < candidates.Length; i++)
        {
            Animator a = candidates[i];
            if (a != null && a.runtimeAnimatorController != null)
                return a;
        }

        return own;
    }

    private void Update()
    {
        if (DialogueManager.IsBlockingGameplay) return;
        if (playerHealth != null && playerHealth.IsDead) return;
        if (playerBowAim == null || !playerBowAim.IsAiming) return;
        if (shooting) return;
        if (Time.time < nextShootTime) return;
        if (animator == null) return;

        // No arrows → can't shoot.
        if (arrows != null && arrows.Arrows <= 0) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (!IsAimWithinEffectiveRange())
                return;
            StartCoroutine(BowShootRoutine());
        }
    }

    private IEnumerator BowShootRoutine()
    {
        shooting = true;
        nextShootTime = Time.time + shootCooldown;

        if (animator != null)
            animator.ResetTrigger(PunchKickHash);

        bool hadController = thirdPersonController != null && thirdPersonController.enabled;
        if (hadController)
            thirdPersonController.enabled = false;

        if (!HasStateOnLayer0(shootStateHash))
        {
            if (!warnedMissingShootState)
            {
                warnedMissingShootState = true;
                Debug.LogWarning($"PlayerBowShoot: Animator missing state '{shootStateName}' on Base Layer.", this);
            }
            if (IsAimWithinEffectiveRange())
                ApplyBowHit();
            ReturnFromShoot(hadController);
            yield break;
        }

        // Fire trigger to enter Launch (user-made transition), and also crossfade as a reliable fallback.
        if (HasTrigger(shootTriggerHash))
        {
            animator.ResetTrigger(shootTriggerHash);
            animator.SetTrigger(shootTriggerHash);
        }
        animator.CrossFadeInFixedTime(shootStateName, crossFadeIn, 0);

        yield return new WaitForSeconds(recoilReleaseDelay);
        if (!IsAimWithinEffectiveRange())
        {
            ReturnFromShoot(hadController);
            yield break;
        }

        // Spend exactly when the arrow releases (matches animation curve timing).
        if (arrows != null && !arrows.TrySpend(1))
        {
            ReturnFromShoot(hadController);
            yield break;
        }

        ApplyBowHit();
        yield return WaitForStateEndOrCancel(shootStateHash);

        ReturnFromShoot(hadController);
    }

    private void ReturnFromShoot(bool hadController)
    {
        if (StillAiming())
            animator.CrossFadeInFixedTime("Aim Overdraw", crossFadeIn, 0);
        else
        {
            animator.SetBool(IsAimingHash, false);
            animator.CrossFadeInFixedTime("Idle Walk Run Blend", crossFadeIn * 1.5f, 0);
        }

        if (hadController && thirdPersonController != null)
            thirdPersonController.enabled = true;

        shooting = false;
    }

    private bool StillAiming()
    {
        return playerBowAim != null && playerBowAim.IsAiming;
    }

    private bool TryComputeBowAim(out Ray ray, out Vector3 spawn, out Vector3 aimPoint)
    {
        ray = default;
        spawn = default;
        aimPoint = default;
        Camera cam = Camera.main;
        if (cam == null)
            return false;

        ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        aimPoint = cam.transform.position + ray.direction * bowRaycastRange;
        if (Physics.Raycast(ray, out RaycastHit aimHit, bowRaycastRange, bowHitMask, QueryTriggerInteraction.Collide))
            aimPoint = aimHit.point;

        spawn = arrowSpawnPoint != null
            ? arrowSpawnPoint.position
            : (cam.transform.position + cam.transform.forward * arrowSpawnForwardOffset);
        return true;
    }

    private bool IsDistanceWithinEffectiveRange(float distance)
    {
        if (!limitEffectiveRange)
            return true;
        if (minEffectiveDistance > 0f && distance < minEffectiveDistance)
            return false;
        if (maxEffectiveDistance > 0f && distance > maxEffectiveDistance)
            return false;
        return true;
    }

    private bool IsAimWithinEffectiveRange()
    {
        if (!TryComputeBowAim(out Ray _, out Vector3 spawn, out Vector3 aimPoint))
            return false;
        return IsDistanceWithinEffectiveRange(Vector3.Distance(spawn, aimPoint));
    }

    private void ApplyBowHit()
    {
        if (!TryComputeBowAim(out Ray ray, out Vector3 spawn, out Vector3 aimPoint))
            return;

        float aimDistance = Vector3.Distance(spawn, aimPoint);
        if (!IsDistanceWithinEffectiveRange(aimDistance))
            return;

        Vector3 dir = (aimPoint - spawn);
        if (dir.sqrMagnitude < 0.0001f)
            dir = ray.direction;
        dir.Normalize();

        Quaternion shotRot = Quaternion.LookRotation(dir);
        SpawnShootVfx(spawn, shotRot);

        if (arrowPrefab != null)
        {
            GameObject arrow = Instantiate(arrowPrefab, spawn, shotRot);
            // Without ArrowProjectile, nothing applies velocity — the arrow stays at the spawn point.
            if (!arrow.TryGetComponent(out ArrowProjectile proj))
                proj = arrow.AddComponent<ArrowProjectile>();
            // Use bow damage/speed so Inspector values match raycast mode and prefab defaults don't fight tuning.
            proj.SetUseGravity(arrowUseGravity);
            proj.Launch(arrowProjectileSpeed, bowDamage);
        }
        else if (Physics.Raycast(ray, out RaycastHit hit, bowRaycastRange, bowHitMask, QueryTriggerInteraction.Collide))
        {
            EnemyHitUtility.TryApplyDamage(hit.collider, bowDamage);
        }
    }

    private IEnumerator WaitForStateEndOrCancel(int shortNameHash)
    {
        float waited = 0f;
        while (waited < stateWaitTimeout)
        {
            if (!StillAiming())
                yield break;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == shortNameHash && info.normalizedTime >= 0.98f && !animator.IsInTransition(0))
                yield break;

            waited += Time.deltaTime;
            yield return null;
        }
    }

    private bool HasStateOnLayer0(int shortNameHash)
    {
        return animator != null && animator.HasState(0, shortNameHash);
    }

    private bool HasTrigger(int hash)
    {
        if (animator == null) return false;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Trigger)
                return true;
        }
        return false;
    }

    private void SpawnShootVfx(Vector3 position, Quaternion rotation)
    {
        float maxLife = shootVfxMaxLifetimeSeconds > 0f ? shootVfxMaxLifetimeSeconds : 8f;
        VfxLifetimeUtility.SpawnBurstAndDestroy(shootVfxPrefab, position, rotation, shootVfxLifetimeSeconds, maxLife);
    }
}
