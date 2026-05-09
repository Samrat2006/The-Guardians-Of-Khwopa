using TMPro;
using UnityEngine;

/// <summary>
/// Manual HUD binder: shows the current arrow count from <see cref="ArrowInventory"/>.
/// Uses a small refresh interval (no per-frame spam) and requires no changes to ArrowInventory.
/// </summary>
[DisallowMultipleComponent]
public class ArrowCountUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ArrowInventory arrowInventory;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Performance")]
    [Tooltip("How often to refresh the count text. 0 = every frame (not recommended).")]
    [SerializeField] private float refreshIntervalSeconds = 0.15f;

    private float _nextRefreshAt;
    private int _lastValue = int.MinValue;

    private void Awake()
    {
        if (countText == null)
            countText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (arrowInventory == null)
            arrowInventory = FindObjectOfType<ArrowInventory>(true);
    }

    private void OnEnable()
    {
        _nextRefreshAt = 0f;
        _lastValue = int.MinValue;
        Refresh(force: true);
    }

    private void Update()
    {
        if (refreshIntervalSeconds <= 0f || Time.unscaledTime >= _nextRefreshAt)
        {
            _nextRefreshAt = Time.unscaledTime + Mathf.Max(0.02f, refreshIntervalSeconds);
            Refresh(force: false);
        }
    }

    private void Refresh(bool force)
    {
        if (countText == null)
            return;

        if (arrowInventory == null)
            arrowInventory = FindObjectOfType<ArrowInventory>(true);

        int value = arrowInventory != null ? arrowInventory.Arrows : 0;
        if (!force && value == _lastValue)
            return;

        _lastValue = value;
        countText.text = value.ToString();
    }
}

