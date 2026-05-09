using System;
using System.Collections;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private Animator animator;

    [Header("Audio")]
    [Tooltip("Played when the player takes damage (e.g. Warrox punch). Skipped if dead.")]
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] [Range(0f, 1f)] private float hurtSoundVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    [Header("Defeat sequence")]
    [Tooltip("Optional; if empty, a fullscreen UI is created at runtime.")]
    [SerializeField] private DefeatSequenceUI defeatSequenceUI;
    [SerializeField] private float defeatedTextFadeInDuration = 0.4f;
    [SerializeField] private float defeatedMessageHoldDuration = 2f;
    [SerializeField] private float fadeToBlackDuration = 1.25f;
    [Tooltip("Safety cap while waiting for the Animator to reach the Dead state.")]
    [SerializeField] private float deathAnimationWaitTimeout = 12f;
    [Tooltip("Defeated text + fade-to-black must start within this many seconds after death.")]
    [SerializeField] private float defeatFadeStartDeadlineSeconds = 3f;

    private int currentHealth;
    private bool isDead;
    private float diedAtUnscaledTime;

    private ThirdPersonController thirdPersonController;
    private StarterAssetsInputs starterAssetsInputs;
    private PlayerCombat playerCombat;
#if ENABLE_INPUT_SYSTEM
    private PlayerInput playerInput;
#endif

    private static readonly int IsDeadHash = Animator.StringToHash("isdead");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
    private static readonly int IsAttackingHash = Animator.StringToHash("isattacking");
    private static readonly int AttackingTriggerHash = Animator.StringToHash("attacking");
    private static readonly int DeadStateHash = Animator.StringToHash("Dead");
    /// <summary>StarterAssets controller names the death state <c>Dead 0</c>, not <c>Dead</c>.</summary>
    private static readonly int DeadStateAltHash = Animator.StringToHash("Dead 0");
    private static readonly int IsAimingHash = Animator.StringToHash("isAiming");
    private static readonly int DamageTriggerHash = Animator.StringToHash("damage");
    private static readonly int PlayerDamageStateHash = Animator.StringToHash("Player_damage");

    public bool IsDead => isDead;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;

    /// <summary>Fired after health changes (damage or init).</summary>
    public event Action<int, int> OnHealthChanged;

    private void Awake()
    {
        // Gameplay: lock/hide cursor (main menu will unlock it).
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (animator == null)
            animator = ResolveAnimator();
        thirdPersonController = GetComponent<ThirdPersonController>();
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();
        playerCombat = GetComponent<PlayerCombat>();
#if ENABLE_INPUT_SYSTEM
        playerInput = GetComponent<PlayerInput>();
#endif

        if (!TryGetComponent<PlayerHealthUI>(out _))
            gameObject.AddComponent<PlayerHealthUI>();
        if (!TryGetComponent<PlayerSuperMeter>(out _))
            gameObject.AddComponent<PlayerSuperMeter>();
        if (!TryGetComponent<PlayerSuperMeterUI>(out _))
            gameObject.AddComponent<PlayerSuperMeterUI>();
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0) return;

        if (debugLogs) Debug.Log("Player Took Damage");
        currentHealth -= damage;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (hurtSound != null && hurtSoundVolume > 0f)
        {
            Vector3 pos = transform.position + Vector3.up * 1.1f;
            AudioSource.PlayClipAtPoint(hurtSound, pos, hurtSoundVolume);
        }

        // Lethal hit: skip damage flinch so "damage" and "isdead" triggers don't fight in the Animator.
        if (animator != null && currentHealth > 0)
            ApplyDamageAnimation();

        if (currentHealth <= 0)
            Die();
    }

    /// <summary>Used by enemies that deal fractional damage.</summary>
    public void TakeDamage(float damage)
    {
        TakeDamage(Mathf.RoundToInt(damage));
    }

    /// <summary>Health packs / pickups. Does not exceed <see cref="MaxHealth"/>; no-op if dead.</summary>
    public void Heal(int amount)
    {
        if (isDead || amount <= 0) return;
        if (currentHealth >= maxHealth) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void HitVFX(Vector3 hitPosition)
    {
        // Kept for compatibility with EnemyDamageDealer / prior HealthSystem API.
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        diedAtUnscaledTime = Time.unscaledTime;

        if (debugLogs) Debug.Log("Player Died");

        if (starterAssetsInputs != null)
        {
            starterAssetsInputs.move = Vector2.zero;
            starterAssetsInputs.look = Vector2.zero;
            starterAssetsInputs.jump = false;
            starterAssetsInputs.sprint = false;
        }

        SnapPlayerToGroundForDeath();

        // Ensure transform isn't held up by the capsule while the death clip plays.
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;

        // Stop Starter Assets controller from writing Speed / blend-tree values this frame or later.
        if (thirdPersonController != null)
            thirdPersonController.enabled = false;

        if (playerCombat != null)
            playerCombat.enabled = false;

        if (TryGetComponent<PlayerBowShoot>(out PlayerBowShoot bowShoot))
            bowShoot.enabled = false;
        if (TryGetComponent<PlayerBowAim>(out PlayerBowAim bowAim))
            bowAim.enabled = false;

#if ENABLE_INPUT_SYSTEM
        if (playerInput != null)
            playerInput.enabled = false;
#endif

        Enemy.NotifyPlayerDefeated();

        if (animator != null)
            ApplyDeathAnimation();

        StartCoroutine(DeathFlowCoroutine());
        StartCoroutine(EnsurePlayerDeadStatePlays());
    }

    /// <summary>
    /// Places the capsule on the ground so the death animation plays while grounded, not mid-air.
    /// </summary>
    private void SnapPlayerToGroundForDeath()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null) return;

        LayerMask mask = thirdPersonController != null && thirdPersonController.GroundLayers.value != 0
            ? thirdPersonController.GroundLayers
            : Physics.DefaultRaycastLayers;

        Vector3 origin = transform.position + Vector3.up * 4f;
        const float maxDist = 80f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDist, mask, QueryTriggerInteraction.Ignore))
        {
            origin = transform.position + Vector3.up * 0.5f;
            if (!Physics.Raycast(origin, Vector3.down, out hit, maxDist, mask, QueryTriggerInteraction.Ignore))
                return;
        }

        float bottomToCenter = cc.height * 0.5f - cc.center.y;
        float targetY = hit.point.y + cc.skinWidth + bottomToCenter;
        Vector3 p = transform.position;
        p.y = targetY;
        transform.position = p;
    }

    /// <summary>
    /// Upper-body layers (Attack, Damage) override the base layer at weight 1 — mute them so the Dead state is visible.
    /// Fires <c>isdead</c> as a <b>Trigger</b> (Any State → Dead in StarterAssetsThirdPerson controller).
    /// </summary>
    private void ApplyDeathAnimation()
    {
        for (int layer = 1; layer < animator.layerCount; layer++)
            animator.SetLayerWeight(layer, 0f);

        // Root motion in death clips can lift/offset the character and look like "floating".
        animator.applyRootMotion = false;

        if (HasAnimatorParameter(IsAttackingHash, AnimatorControllerParameterType.Bool))
            animator.SetBool(IsAttackingHash, false);
        if (HasAnimatorParameter(AttackingTriggerHash, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(AttackingTriggerHash);
        if (HasAnimatorParameter(IsAimingHash, AnimatorControllerParameterType.Bool))
            animator.SetBool(IsAimingHash, false);

        animator.SetFloat(SpeedHash, 0f);
        animator.SetFloat(MotionSpeedHash, 0f);
        animator.SetBool(GroundedHash, true);
        animator.SetBool(JumpHash, false);
        animator.SetBool(FreeFallHash, false);

        if (HasAnimatorParameter(IsDeadHash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(IsDeadHash);
            animator.SetTrigger(IsDeadHash);
        }
        else if (HasAnimatorParameter(IsDeadHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(IsDeadHash, true);
        }

        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.Update(0f);
    }

    private void ApplyDamageAnimation()
    {
        // Don't play damage reaction once we're dead.
        if (isDead) return;

        // Prefer the Trigger -> DamageLayer path if the controller has it.
        if (HasAnimatorParameter(DamageTriggerHash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(DamageTriggerHash);
            animator.SetTrigger(DamageTriggerHash);
            return;
        }

        // Fallback: directly crossfade to the base-layer state name if it exists.
        if (animator.HasState(0, PlayerDamageStateHash))
        {
            animator.CrossFadeInFixedTime("Player_damage", 0.08f, 0);
            animator.Update(0f);
        }
    }

    /// <summary>
    /// If the controller does not reach Dead (parameter mismatch, transition order), force the state on the base layer.
    /// </summary>
    private IEnumerator EnsurePlayerDeadStatePlays()
    {
        if (animator == null) yield break;

        yield return null;
        yield return null;
        yield return null;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (IsAnimatorInDeadState(info))
            yield break;

        if (HasAnimatorParameter(IsDeadHash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(IsDeadHash);
            animator.SetTrigger(IsDeadHash);
        }
        else if (HasAnimatorParameter(IsDeadHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(IsDeadHash, true);
        }

        if (animator.HasState(0, DeadStateHash))
        {
            animator.Play(DeadStateHash, 0, 0f);
            animator.Update(0f);
        }
        else if (animator.HasState(0, DeadStateAltHash))
        {
            animator.Play(DeadStateAltHash, 0, 0f);
            animator.Update(0f);
        }
    }

    private static bool IsAnimatorInDeadState(AnimatorStateInfo info)
    {
        if (info.shortNameHash == DeadStateHash || info.shortNameHash == DeadStateAltHash)
            return true;
        return info.IsName("Dead") || info.IsName("Dead 0");
    }

    private bool HasAnimatorParameter(int hash, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == hash && p.type == type)
                return true;
        }

        return false;
    }

    private IEnumerator DeathFlowCoroutine()
    {
        // Do not let the defeat UI feel delayed by long death animations.
        yield return WaitForDeathAnimationEndOrDeadline();

        DefeatSequenceUI ui = defeatSequenceUI != null ? defeatSequenceUI : DefeatSequenceUI.GetOrCreate();

        // Enforce: defeated + fade begin within N seconds of death.
        float deadline = Mathf.Max(0f, defeatFadeStartDeadlineSeconds);
        float elapsed = Time.unscaledTime - diedAtUnscaledTime;
        float remainingBeforeFade = Mathf.Max(0f, deadline - elapsed);

        float textFade = defeatedTextFadeInDuration;
        float hold = defeatedMessageHoldDuration;

        // If we have a small remaining budget, clamp so the fade-to-black starts in time.
        if (textFade + hold > remainingBeforeFade)
        {
            float maxTextFade = remainingBeforeFade * 0.5f;
            textFade = Mathf.Clamp(textFade, 0f, maxTextFade);
            hold = Mathf.Clamp(hold, 0f, Mathf.Max(0f, remainingBeforeFade - textFade));
        }

        yield return ui.RunPresentation(textFade, hold, fadeToBlackDuration);

        if (debugLogs) Debug.Log("Restarting Scene");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private IEnumerator WaitForDeathAnimationEndOrDeadline()
    {
        if (animator == null)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            yield break;
        }

        // Allow Any State → Dead transition to start.
        yield return null;
        yield return null;

        float waited = 0f;
        float deadline = Mathf.Max(0f, defeatFadeStartDeadlineSeconds);
        while (waited < deathAnimationWaitTimeout)
        {
            // Hard cap: do not delay UI past the deadline.
            if (Time.unscaledTime - diedAtUnscaledTime >= deadline)
                yield break;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (IsAnimatorInDeadState(info) && info.normalizedTime >= 0.95f && !animator.IsInTransition(0))
                yield break;

            waited += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private Animator ResolveAnimator()
    {
        Animator own = GetComponent<Animator>();
        if (own != null && own.runtimeAnimatorController != null)
            return own;

        Animator[] children = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Animator a = children[i];
            if (a != null && a.runtimeAnimatorController != null)
                return a;
        }

        return own;
    }
}
