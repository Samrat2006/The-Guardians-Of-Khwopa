using UnityEngine;

/// <summary>
/// Marks a volume in a single-scene game as a "level" or area. Uses a <b>trigger collider</b> (not raycasts):
/// when the player enters, a title/description is shown. Add a Box Collider, enable Is Trigger, size it to the doorway/region.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LevelAreaZone : MonoBehaviour
{
    [Header("Copy")]
    [SerializeField] private string levelTitle = "Level 1";
    [TextArea(2, 5)]
    [SerializeField] private string levelDescription = "";

    [Header("Text look (position, color, size)")]
    [Tooltip("Per-zone: title/body anchored position, colors, font sizes, optional TMP fonts.")]
    [SerializeField] private LevelAreaIntroPresentation introPresentation = new LevelAreaIntroPresentation();

    [Header("Timing")]
    [SerializeField] private float holdSeconds = 2.5f;
    [SerializeField] private float fadeInDuration = 0.45f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("Show rules")]
    [Tooltip("Unique id for this zone (e.g. Level_02_Ruins). Used with Show Once Per Session.")]
    [SerializeField] private string zoneId = "";
    [SerializeField] private bool showOncePerSession = true;

    private static readonly System.Collections.Generic.HashSet<string> s_shownThisSession = new System.Collections.Generic.HashSet<string>();

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null)
            c.isTrigger = true;
    }

    private void Awake()
    {
        Collider c = GetComponent<Collider>();
        if (c != null && !c.isTrigger)
        {
            Debug.LogWarning($"LevelAreaZone on {name}: collider should be a trigger. Enabling Is Trigger.", this);
            c.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (!HierarchyHasPlayerTag(other.transform)) return;

        string key = string.IsNullOrWhiteSpace(zoneId) ? $"{gameObject.scene.name}:{GetInstanceID()}" : zoneId.Trim();
        if (showOncePerSession)
        {
            if (s_shownThisSession.Contains(key))
                return;
            s_shownThisSession.Add(key);
        }

        LevelAreaIntroUI.Instance.Show(levelTitle, levelDescription, holdSeconds, fadeInDuration, fadeOutDuration, introPresentation);
    }

    private static bool HierarchyHasPlayerTag(Transform t)
    {
        for (Transform x = t; x != null; x = x.parent)
        {
            if (x.CompareTag("Player"))
                return true;
        }

        return false;
    }
}
