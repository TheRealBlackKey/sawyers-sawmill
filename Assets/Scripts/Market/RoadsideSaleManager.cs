using UnityEngine;
using System;

/// <summary>
/// Early-game escape valve. Lets the player sell rough sawn boards directly
/// from the mill output at a steep discount — no kiln or market required.
///
/// The button hides itself once a MarketBuilding exists in the scene,
/// since the player now has proper selling infrastructure.
///
/// SETUP:
///   Add to any persistent GameObject (GameManager works fine).
///   Wire the HUD button via HUDManager.
/// </summary>
public class RoadsideSaleManager : MonoBehaviour
{
    public static RoadsideSaleManager Instance { get; private set; }

    public static event Action<int, float> OnRoadsideSale; // boardsSold, goldEarned

    [Header("Sale Settings")]
    [Tooltip("Fraction of rough sawn value paid out. Low — this is desperation money.")]
    [SerializeField] private float discountRate = 0.35f;

    [Tooltip("Hide the roadside sale button once a market building is placed.")]
    [SerializeField] private bool hideWhenMarketExists = true;

    // Cached reference — refreshed each sale attempt
    private MarketBuilding _market;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Returns true if the roadside sale button should be visible.
    /// False once a proper market building exists.
    /// </summary>
    public bool IsAvailable()
    {
        if (!hideWhenMarketExists) return true;
        if (_market == null) _market = FindFirstObjectByType<MarketBuilding>();
        return _market == null;
    }

    /// <summary>
    /// Returns how many rough sawn boards are currently sitting in MillOutput.
    /// Used to update the button label.
    /// </summary>
    public int AvailableBoardCount()
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return 0;
        return inv.Count(InventoryManager.InventoryZone.MillOutput);
    }

    /// <summary>
    /// Sells all rough sawn boards currently in MillOutput at the discount rate.
    /// Called by HUDManager when the player clicks the roadside sale button.
    /// </summary>
    public void SellAllRoughSawn()
    {
        var inv = InventoryManager.Instance;
        var gm  = GameManager.Instance;
        if (inv == null || gm == null) return;

        var boards = inv.GetItems(InventoryManager.InventoryZone.MillOutput);
        if (boards.Count == 0)
        {
            Debug.Log("[Roadside] No rough sawn boards to sell.");
            return;
        }

        float totalGold = 0f;
        int   count     = 0;

        foreach (var board in boards)
        {
            if (board.species == null) continue;

            float value = board.species.valuePerBoardRoughSawn * discountRate;
            inv.RemoveItem(board, InventoryManager.InventoryZone.MillOutput);
            gm.EarnGold(value);
            gm.RegisterItemSold(board, value);
            totalGold += value;
            count++;
        }

        if (count > 0)
        {
            OnRoadsideSale?.Invoke(count, totalGold);
            Debug.Log($"[Roadside] Sold {count} rough sawn board(s) for {totalGold:F1}g total.");
        }
    }
}
