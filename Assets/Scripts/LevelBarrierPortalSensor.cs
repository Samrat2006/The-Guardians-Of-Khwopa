using UnityEngine;

/// <summary>
/// Put this on a trigger collider GameObject to notify <see cref="LevelBarrierPortal"/>.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LevelBarrierPortalSensor : MonoBehaviour
{
    public enum SensorMode
    {
        MessageBarrier,
        Portal
    }

    [SerializeField] private SensorMode mode = SensorMode.MessageBarrier;
    [SerializeField] private LevelBarrierPortal barrier;
    
    public SensorMode Mode => mode;

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
            c.isTrigger = true;
    }

    public void Bind(LevelBarrierPortal owner)
    {
        barrier = owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (!HierarchyHasPlayerTag(other.transform)) return;
        if (barrier == null) return;

        if (mode == SensorMode.Portal)
            barrier.NotifyPlayerCrossedPortal();
        else
            barrier.NotifyPlayerHitBarrier();
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

