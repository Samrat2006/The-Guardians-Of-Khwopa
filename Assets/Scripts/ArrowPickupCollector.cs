using UnityEngine;

/// <summary>
/// Place on the player (same GameObject as <see cref="CharacterController"/> / movement).
/// Collects <see cref="ArrowPickup"/> when the player's trigger volume overlaps a pickup.
/// This is more reliable than only handling overlap on the pickup (layer matrix, CC vs triggers, child colliders).
/// </summary>
[DefaultExecutionOrder(-35)]
public class ArrowPickupCollector : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        TryPickup(other);
    }

    private void TryPickup(Collider other)
    {
        if (other == null) return;
        ArrowPickup pickup = other.GetComponent<ArrowPickup>();
        if (pickup == null) pickup = other.GetComponentInParent<ArrowPickup>();
        if (pickup == null) return;

        pickup.TryCollectFromPlayer(gameObject);
    }
}
