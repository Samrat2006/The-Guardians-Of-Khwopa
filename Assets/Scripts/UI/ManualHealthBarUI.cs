using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manual HUD health bar binder. Updates a Filled Image based on <see cref="PlayerHealth.OnHealthChanged"/>.
/// No runtime UI creation, no polling.
/// </summary>
[DisallowMultipleComponent]
public class ManualHealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("Your green bar Image. Must be Image Type = Filled (Horizontal).")]
    [SerializeField] private Image fillImage;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth != null)
            playerHealth.OnHealthChanged += HandleHealthChanged;

        RefreshNow();
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int current, int max)
    {
        if (fillImage == null)
            return;
        if (max <= 0)
        {
            fillImage.fillAmount = 0f;
            return;
        }
        fillImage.fillAmount = Mathf.Clamp01((float)current / max);
    }

    private void RefreshNow()
    {
        if (playerHealth == null || fillImage == null)
            return;
        HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }
}

