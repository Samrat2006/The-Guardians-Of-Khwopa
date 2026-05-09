using UnityEngine;

public class CoinCollector : MonoBehaviour
{
    public static CoinCollector Instance;

    public int coins = 0;
    [SerializeField] private bool debugLogs = false;

    void Awake()
    {
        Instance = this;
    }

    public void AddCoin(int amount)
    {
        if (amount <= 0) return;
        coins += amount;
        if (debugLogs) Debug.Log("Coins Collected: " + coins);
        QuestManager.Resolve()?.NotifyCoinEconomyChanged();
    }

    /// <summary>
    /// Deduct coins for crafting / rebuilding. Returns false if balance is insufficient.
    /// </summary>
    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0)
            return true;
        if (coins < amount)
            return false;
        coins -= amount;
        if (debugLogs) Debug.Log("Coins spent: " + amount + "; balance: " + coins);
        QuestManager.Resolve()?.NotifyCoinEconomyChanged();
        return true;
    }
}