using System.Collections.Generic;
using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    bool canDealDamage = false;
    List<GameObject> damagedTargets = new List<GameObject>();

    [SerializeField] float weaponLength = 2f;
    [SerializeField] float weaponDamage = 1f;
    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    void Update()
    {
        if (!canDealDamage) return;

        RaycastHit hit;

        Debug.DrawRay(transform.position, transform.forward * weaponLength, Color.red);

        if (Physics.Raycast(transform.position, transform.forward, out hit, weaponLength))
        {
            if (debugLogs) Debug.Log("Ray hit: " + hit.transform.name);

            GameObject root = hit.collider.transform.root.gameObject;
            if (damagedTargets.Contains(root))
                return;

            if (EnemyHitUtility.TryApplyDamage(hit.collider, weaponDamage))
            {
                damagedTargets.Add(root);
                Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
                if (enemy != null)
                    enemy.HitVFX(hit.point);
                if (debugLogs) Debug.Log("Enemy detected -> Damage applied");
            }
        }
    }

    public void StartDealDamage()
    {
        if (debugLogs) Debug.Log("Damage window OPEN");

        canDealDamage = true;
        damagedTargets.Clear();
    }

    public void EndDealDamage()
    {
        if (debugLogs) Debug.Log("Damage window CLOSED");

        canDealDamage = false;
    }
}