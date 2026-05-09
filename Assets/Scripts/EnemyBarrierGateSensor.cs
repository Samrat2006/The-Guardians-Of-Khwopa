using UnityEngine;

/// <summary>
/// Put this on a trigger collider GameObject near the barrier. When player enters, it tells <see cref="EnemyBarrierGate"/> to show the message.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyBarrierGateSensor : MonoBehaviour
{
    [SerializeField] private EnemyBarrierGate gate;

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

    public void Bind(EnemyBarrierGate owner)
    {
        gate = owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (!HierarchyHasPlayerTag(other.transform)) return;
        if (gate == null) return;
        gate.NotifyPlayerHitBarrier();
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

