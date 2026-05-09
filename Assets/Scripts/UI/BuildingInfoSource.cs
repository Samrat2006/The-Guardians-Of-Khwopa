using UnityEngine;

/// <summary>
/// Attach to a building trigger (BoxCollider isTrigger) to provide info shown in <see cref="BuildingInfoPanel"/>.
/// </summary>
[DisallowMultipleComponent]
public class BuildingInfoSource : MonoBehaviour
{
    [Header("Content")]
    [SerializeField] private string title;
    [TextArea(3, 10)]
    [SerializeField] private string description;
    [SerializeField] private Sprite image;

    [Header("Quest tracking")]
    [Tooltip("Stable ID counted toward Learn X building objectives. Defaults to Title, then GameObject name.")]
    [SerializeField] private string questBuildingId;

    [Header("Optional")]
    [Tooltip("If empty, the panel will use its own default hint text.")]
    [SerializeField] private string hintOverride;

    public string QuestBuildingId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(questBuildingId))
                return questBuildingId.Trim();
            if (!string.IsNullOrWhiteSpace(title))
                return title.Trim();
            return gameObject.name;
        }
    }

    public string Title => title;
    public string Description => description;
    public Sprite Image => image;
    public string HintOverride => hintOverride;
}

