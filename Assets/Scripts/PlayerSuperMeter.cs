using System;
using UnityEngine;

/// <summary>
/// Fills while alive (over time) and from <see cref="BoosterPickup"/>. When full, <see cref="PlayerCombat"/> can spend it for a super attack (default key R).
/// </summary>
[DefaultExecutionOrder(-80)]
public class PlayerSuperMeter : MonoBehaviour
{
    [Header("Meter")]
    [SerializeField] private float maxMeter = 100f;
    [Tooltip("Points added per second while alive (not dead, gameplay not blocked).")]
    [SerializeField] private float fillPerSecondWhileAlive = 8f;

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;

    private float current;

    /// <summary>Current / max (0–1).</summary>
    public float Normalized => maxMeter > 0.01f ? Mathf.Clamp01(current / maxMeter) : 0f;

    public float Current => current;
    public float Max => maxMeter;

    public bool IsFull => current >= maxMeter - 0.001f;

    /// <summary>Fired when current or max changes (UI).</summary>
    public event Action<float, float> OnMeterChanged;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();

        current = 0f;
        Notify();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        if (currentHealth <= 0)
            ResetMeter();
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead) return;
        if (DialogueManager.IsBlockingGameplay) return;
        if (IsFull) return;

        current += Mathf.Max(0f, fillPerSecondWhileAlive) * Time.deltaTime;
        current = Mathf.Min(current, maxMeter);
        Notify();
    }

    /// <summary>Called by booster pickups.</summary>
    public void AddMeter(float amount)
    {
        if (amount <= 0f) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        current += amount;
        current = Mathf.Min(current, maxMeter);
        Notify();
    }

    /// <summary>Returns true if the bar was full and is now emptied.</summary>
    public bool TryConsumeFullMeter()
    {
        if (!IsFull) return false;
        current = 0f;
        Notify();
        return true;
    }

    public void ResetMeter()
    {
        current = 0f;
        Notify();
    }

    private void Notify()
    {
        OnMeterChanged?.Invoke(current, maxMeter);
    }
}
