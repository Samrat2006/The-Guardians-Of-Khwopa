using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Call <see cref="MarkTempleRebuilt"/> from animation events, timeline signals, UnityEvents, or other scripts when the temple restoration completes.
/// </summary>
public class QuestTempleRebuildNotifier : MonoBehaviour
{
    [SerializeField] private UnityEvent onTempleMarkedRebuilt;

    /// <summary>Completes the "rebuild temple" quest objective.</summary>
    public void MarkTempleRebuilt()
    {
        var qm = QuestManager.Resolve();
        qm?.NotifyTempleRebuilt();
        onTempleMarkedRebuilt?.Invoke();
    }
}
