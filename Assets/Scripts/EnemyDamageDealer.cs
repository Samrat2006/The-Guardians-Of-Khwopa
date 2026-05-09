using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDamageDealer : MonoBehaviour
{
    bool canDealDamage;
    bool hasDealtDamage;

    [SerializeField] float weaponLength = 2f;
    [SerializeField] float weaponDamage = 1f;

    void Start()
    {
        canDealDamage = false;
        hasDealtDamage = false;
    }

    void Update()
    {
        // debug ray to see attack direction
        Debug.DrawRay(transform.position, transform.forward * weaponLength, Color.red);

        if (canDealDamage && !hasDealtDamage)
        {
            RaycastHit hit;

            int layerMask = 1 << 8; // player layer

            if (Physics.Raycast(transform.position, transform.forward, out hit, weaponLength, layerMask))
            {
                PlayerHealth playerHealth = hit.transform.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(Mathf.RoundToInt(weaponDamage));
                    playerHealth.HitVFX(hit.point);
                    hasDealtDamage = true;
                }
            }
        }
    }

    public void StartDealDamage()
    {
        canDealDamage = true;
        hasDealtDamage = false;
    }

    public void EndDealDamage()
    {
        canDealDamage = false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * weaponLength);
    }
}