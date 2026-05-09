using UnityEngine;

/// <summary>
/// Simple arrow inventory for the player.
/// - Add arrows via pickups
/// - Spend 1 arrow per bow shot
/// </summary>
public class ArrowInventory : MonoBehaviour
{
    [SerializeField] private int arrows = 20;

    public int Arrows => arrows;

    private void Awake()
    {
        // OnTriggerEnter only runs on the GameObject that owns the trigger Collider.
        Transform searchRoot = transform.root;

        CharacterController cc = searchRoot.GetComponentInChildren<CharacterController>(true);
        if (cc != null)
        {
            foreach (Collider c in cc.gameObject.GetComponents<Collider>())
            {
                if (c == null || !c.isTrigger) continue;
                if (c.gameObject.GetComponent<ArrowPickupCollector>() != null) return;
                c.gameObject.AddComponent<ArrowPickupCollector>();
                return;
            }
        }

        foreach (Collider c in searchRoot.GetComponentsInChildren<Collider>(true))
        {
            if (c == null || !c.isTrigger) continue;
            if (c.gameObject.GetComponent<ArrowPickupCollector>() != null) return;
            c.gameObject.AddComponent<ArrowPickupCollector>();
            return;
        }

        if (GetComponent<ArrowPickupCollector>() == null)
            gameObject.AddComponent<ArrowPickupCollector>();
    }

    public void AddArrows(int amount)
    {
        if (amount <= 0) return;
        arrows += amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (arrows < amount) return false;
        arrows -= amount;
        return true;
    }
}

