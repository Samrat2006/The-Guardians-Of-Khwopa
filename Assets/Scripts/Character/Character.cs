using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

public class Character : MonoBehaviour
{
    public PlayerInput playerInput;
    public Animator animator;

    private InputAction attackAction;
    private StarterAssetsInputs inputs;
    private PlayerBowAim bowAim;
    private PlayerBowShoot bowShoot;
    private PlayerHealth playerHealth;

    private bool isAttacking;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>();
        inputs = GetComponent<StarterAssetsInputs>();
        bowAim = GetComponent<PlayerBowAim>();
        bowShoot = GetComponent<PlayerBowShoot>();
        playerHealth = GetComponent<PlayerHealth>();

        attackAction = playerInput.actions["Attack"];
    }

    void Update()
    {
        if (DialogueManager.IsBlockingGameplay) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        // When aiming/shooting bow, don't trigger melee punchKick.
        if (bowAim != null && bowAim.IsAiming) return;
        if (bowShoot != null && bowShoot.IsShooting) return;

        if (attackAction.triggered && !isAttacking)
        {
            Attack();
        }

        // stop movement while attacking
        if (isAttacking)
        {
            inputs.move = Vector2.zero;
        }
    }

    void Attack()
    {
        isAttacking = true;
        animator.SetTrigger("punchKick");

        // automatically unlock after animation time
        Invoke(nameof(EndAttack), 1.9f);
    }

    void EndAttack()
    {
        isAttacking = false;
    }
}