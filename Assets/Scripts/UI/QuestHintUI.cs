using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manual top-left HUD widget: shows exactly one quest objective line (the next incomplete objective).
/// Binds to <see cref="QuestManager.ProgressChanged"/> so it updates without polling.
/// </summary>
[DisallowMultipleComponent]
public class QuestHintUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private QuestManager questManager;
    [SerializeField] private TextMeshProUGUI hintText;
    [Tooltip("Optional icon to hide when there is no active hint.")]
    [SerializeField] private Image hintIcon;

    [Header("Behaviour")]
    [Tooltip("If true, hides the whole widget when there is no active hint.")]
    [SerializeField] private bool hideRootWhenNoHint = true;

    private void Awake()
    {
        if (questManager == null)
            questManager = QuestManager.Resolve();
        if (hintText == null)
            hintText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void OnEnable()
    {
        if (questManager == null)
            questManager = QuestManager.Resolve();
        if (questManager != null)
            questManager.ProgressChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (questManager != null)
            questManager.ProgressChanged -= Refresh;
    }

    public void Refresh()
    {
        string line = questManager != null ? questManager.GetNextIncompleteObjectiveLine() : string.Empty;
        bool hasHint = !string.IsNullOrWhiteSpace(line);

        if (hintText != null)
            hintText.text = hasHint ? line : "";

        if (hintIcon != null)
            hintIcon.gameObject.SetActive(hasHint);

        if (hideRootWhenNoHint)
            gameObject.SetActive(hasHint);
    }
}

