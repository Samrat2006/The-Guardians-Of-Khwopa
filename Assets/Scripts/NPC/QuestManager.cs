using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// Tracks optional multi-step objectives for the quest log. Completed objectives are omitted from <see cref="BuildQuestSummaryMultiline"/>.
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Tooltip("Coins progress uses this player's inventory when set (auto-found if empty).")]
    [SerializeField] private CoinCollector coinCollector;

    [Header("Objective — Talk with NPC")]
    [SerializeField] private bool trackTalkNpc = true;
    [SerializeField] private string talkObjectiveDisplay = "Talk with the envoy";
    [Tooltip("Leave empty so the first notifier (e.g. Arissa / NPCInteraction) completes this step.")]
    [SerializeField] private string requiredTalkSourceId = "";

    [Header("Objective — Collect coins")]
    [SerializeField] private bool trackCollectCoins = true;
    [SerializeField] private int coinsTarget = 25;
    [SerializeField] private string coinsObjectiveDisplay = "Collect coins";

    [Header("Objective — Building names")]
    [SerializeField] private bool trackBuildingNames = true;
    [SerializeField] private int buildingsRequiredCount = 4;
    [SerializeField] private string buildingsObjectiveDisplay = "Learn names of four buildings";

    [Header("Objective — Villain")]
    [SerializeField] private bool trackDefeatVillain = true;
    [SerializeField] private string villainObjectiveDisplay = "Defeat Kyha";

    [Header("Objective — Temple")]
    [SerializeField] private bool trackRebuildTemple = true;
    [SerializeField] private string rebuildTempleDisplay = "Rebuild the temple";

    [Tooltip("Optional legacy HUD line; safe to leave empty if you only use the quest journal panel.")]
    public TextMeshProUGUI questText;

    /// <summary>Fired whenever quest progress updates (coins, NPC talk, buildings, villain, temple).</summary>
    public event System.Action ProgressChanged;

    /// <summary>Distinct building IDs discovered (see <see cref="BuildingInfoSource.QuestBuildingId"/>).</summary>
    private readonly HashSet<string> _buildingIdsSeen = new HashSet<string>();

    private bool _talkComplete;
    private bool _villainComplete;
    private bool _templeComplete;

    /// <summary>Prefer <see cref="Instance"/>; falls back when another script fires before Awake.</summary>
    public static QuestManager Resolve()
    {
        if (Instance != null)
            return Instance;
        return UnityEngine.Object.FindObjectOfType<QuestManager>(true);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("QuestManager: duplicate in scene — destroying this instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (coinCollector == null)
            coinCollector = CoinCollector.Instance != null ? CoinCollector.Instance : FindFirstObjectByType<CoinCollector>();
    }

    private void Start()
    {
        UpdateQuestText();
    }

    /// <summary>Call from proximity dialogue / NPC scripts. Use <paramref name="sourceId"/> consistent with Required Talk Source Id, or omit when that field is blank.</summary>
    public void NotifyTalkWithNpc(string sourceId = null)
    {
        if (!trackTalkNpc || _talkComplete)
            return;

        string want = requiredTalkSourceId != null ? requiredTalkSourceId.Trim() : "";
        if (!string.IsNullOrEmpty(want))
        {
            string got = sourceId != null ? sourceId.Trim() : "";
            if (!want.Equals(got, System.StringComparison.OrdinalIgnoreCase))
                return;
        }

        _talkComplete = true;
        RaiseProgressChanged();
    }

    /// <summary>Called when building info opens; <paramref name="buildingId"/> should be stable per landmark.</summary>
    public void NotifyBuildingDiscovered(string buildingId)
    {
        if (!trackBuildingNames)
            return;
        if (string.IsNullOrWhiteSpace(buildingId))
            return;
        _buildingIdsSeen.Add(buildingId.Trim());
        RaiseProgressChanged();
    }

    /// <summary>Coins changed (collect or spend) — refreshes quest HUD and <see cref="ProgressChanged"/> listeners.</summary>
    public void NotifyCoinEconomyChanged()
    {
        RaiseProgressChanged();
    }

    /// <summary>Usually invoked by <see cref="QuestVillainTarget"/> when the villain dies.</summary>
    public void NotifyVillainDefeated()
    {
        if (!trackDefeatVillain || _villainComplete)
            return;
        _villainComplete = true;
        RaiseProgressChanged();
    }

    /// <summary>Invoke from <see cref="QuestTempleRebuildNotifier"/> when restoration finishes.</summary>
    public void NotifyTempleRebuilt()
    {
        if (!trackRebuildTemple || _templeComplete)
            return;
        _templeComplete = true;
        RaiseProgressChanged();
    }

    /// <summary>True when every objective with its Track flag on is satisfied (gates, portals).</summary>
    public bool AreAllActiveObjectivesComplete()
    {
        return HasAnyTrackedObjective() && !HasAnyIncompleteObjective();
    }

    /// <summary>Comma-separated unfinished objectives for barrier hint lines.</summary>
    public string BuildIncompleteObjectivesPlainText(string separator = ", ")
    {
        if (separator == null) separator = ", ";
        var parts = new List<string>();

        if (trackTalkNpc && !_talkComplete) parts.Add(talkObjectiveDisplay);
        if (trackCollectCoins && !IsCoinsDone()) parts.Add($"{coinsObjectiveDisplay} ({CoinsHeld()}/{coinsTarget})");
        if (trackBuildingNames && !IsBuildingsDone())
            parts.Add($"{buildingsObjectiveDisplay} ({_buildingIdsSeen.Count}/{buildingsRequiredCount})");
        if (trackDefeatVillain && !_villainComplete) parts.Add(villainObjectiveDisplay);
        if (trackRebuildTemple && !_templeComplete) parts.Add(rebuildTempleDisplay);

        return parts.Count == 0 ? string.Empty : string.Join(separator, parts);
    }

    /// <summary>Level barrier / narration: prepend + incomplete list.</summary>
    public string BuildBarrierBlockedMessage(string headerLine)
    {
        string tail = BuildIncompleteObjectivesPlainText("\n• ");
        if (string.IsNullOrEmpty(tail))
            return headerLine ?? "";
        if (string.IsNullOrWhiteSpace(headerLine))
            return "Still to do:\n• " + tail;
        return headerLine.TrimEnd() + "\n\nStill to do:\n• " + tail;
    }

    /// <summary>
    /// Single-line hint for the HUD (top-left): returns the next incomplete objective.
    /// Returns empty string if there is no active quest or all tracked objectives are complete.
    /// </summary>
    public string GetNextIncompleteObjectiveLine()
    {
        if (!HasAnyTrackedObjective())
            return string.Empty;

        if (trackTalkNpc && !_talkComplete)
            return talkObjectiveDisplay;

        if (trackCollectCoins && !IsCoinsDone())
            return $"{coinsObjectiveDisplay} ({CoinsHeld()} / {Mathf.Max(1, coinsTarget)})";

        if (trackBuildingNames && !IsBuildingsDone())
            return $"{buildingsObjectiveDisplay} ({_buildingIdsSeen.Count} / {Mathf.Max(1, buildingsRequiredCount)})";

        if (trackDefeatVillain && !_villainComplete)
            return villainObjectiveDisplay;

        if (trackRebuildTemple && !_templeComplete)
            return rebuildTempleDisplay;

        return string.Empty;
    }

    public void UpdateQuestText()
    {
        if (questText != null)
            questText.text = BuildQuestSummarySingleLine();
    }

    private void RaiseProgressChanged()
    {
        UpdateQuestText();
        ProgressChanged?.Invoke();
    }

    /// <summary>Multi-line body for the quest panel UI.</summary>
    public string BuildQuestSummaryMultiline()
    {
        if (!HasAnyTrackedObjective())
            return "<i>No active quest.</i>";

        bool anyPending = HasAnyIncompleteObjective();
        var sb = new StringBuilder();

        if (anyPending)
        {
            sb.AppendLine("<b>Objectives</b>");
            bool firstLine = true;
            AppendObjectiveLine(ref firstLine, sb, trackTalkNpc, IsTalkDone(), $"{talkObjectiveDisplay}");
            AppendObjectiveLine(ref firstLine, sb, trackCollectCoins, IsCoinsDone(), $"{coinsObjectiveDisplay} ({CoinsHeld()} / {Mathf.Max(1, coinsTarget)})");
            AppendObjectiveLine(ref firstLine, sb, trackBuildingNames, IsBuildingsDone(),
                $"{buildingsObjectiveDisplay} ({_buildingIdsSeen.Count} / {Mathf.Max(1, buildingsRequiredCount)} known)");
            AppendObjectiveLine(ref firstLine, sb, trackDefeatVillain, IsVillainDone(), $"{villainObjectiveDisplay}");
            AppendObjectiveLine(ref firstLine, sb, trackRebuildTemple, IsTempleDone(), $"{rebuildTempleDisplay}");

            sb.AppendLine();
            sb.Append("<size=92%><i>Finish every task to restore the sanctuary.</i></size>");
        }
        else
        {
            sb.AppendLine("<b>Status:</b>");
            sb.AppendLine("Story chapter complete!");
            sb.AppendLine();
            sb.Append("<size=92%>You finished every ritual task.<i> Completed goals are cleared from this log.</i></size>");
        }

        return sb.ToString().TrimEnd();
    }

    public string BuildQuestSummarySingleLine()
    {
        if (!HasAnyTrackedObjective())
            return "No quest.";
        int left = CountIncompleteObjectives();
        if (left == 0)
            return "Chapter complete!";
        return left == 1 ? "1 objective left" : $"{left} objectives left";
    }

    private bool HasAnyTrackedObjective()
    {
        return trackTalkNpc || trackCollectCoins || trackBuildingNames || trackDefeatVillain || trackRebuildTemple;
    }

    private bool HasAnyIncompleteObjective()
    {
        if (trackTalkNpc && !_talkComplete) return true;
        if (trackCollectCoins && !IsCoinsDone()) return true;
        if (trackBuildingNames && !IsBuildingsDone()) return true;
        if (trackDefeatVillain && !_villainComplete) return true;
        if (trackRebuildTemple && !_templeComplete) return true;
        return false;
    }

    private int CountIncompleteObjectives()
    {
        int n = 0;
        if (trackTalkNpc && !_talkComplete) n++;
        if (trackCollectCoins && !IsCoinsDone()) n++;
        if (trackBuildingNames && !IsBuildingsDone()) n++;
        if (trackDefeatVillain && !_villainComplete) n++;
        if (trackRebuildTemple && !_templeComplete) n++;
        return n;
    }

    private static void AppendObjectiveLine(ref bool firstLine, StringBuilder sb, bool tracked, bool done, string line)
    {
        if (!tracked || done || string.IsNullOrEmpty(line))
            return;

        sb.Append(firstLine ? "• " : "\n• ");
        firstLine = false;
        sb.Append(line.Trim());
    }

    private bool IsTalkDone() => !trackTalkNpc || _talkComplete;

    private bool IsVillainDone() => !trackDefeatVillain || _villainComplete;

    private bool IsTempleDone() => !trackRebuildTemple || _templeComplete;

    private bool IsCoinsDone()
    {
        if (!trackCollectCoins) return true;
        return CoinsHeld() >= coinsTarget;
    }

    private int CoinsHeld()
    {
        if (coinCollector == null && CoinCollector.Instance != null)
            coinCollector = CoinCollector.Instance;
        return coinCollector != null ? coinCollector.coins : 0;
    }

    private bool IsBuildingsDone()
    {
        if (!trackBuildingNames) return true;
        return _buildingIdsSeen.Count >= Mathf.Max(1, buildingsRequiredCount);
    }

}
