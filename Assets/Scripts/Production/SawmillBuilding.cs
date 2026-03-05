using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// The sawmill building. Takes logs from MillInput inventory zone,
/// processes them into rough sawn boards, outputs to MillOutput zone.
/// </summary>
public class SawmillBuilding : MonoBehaviour
{
    public static event Action<LumberItem> OnBoardMilled;

    [Header("Positions (world space — auto-set from transform if zero)")]
    public Vector3 InputPosition;
    public Vector3 OutputPosition;

    [Header("Base Stats")]
    [SerializeField] private float baseMillingTime = 20f;

    [Header("Input/Output Offsets from building center")]
    [SerializeField] private float inputOffsetX = -40f;
    [SerializeField] private float outputOffsetX = 40f;

    [Header("Passive Sales (Early Game Income)")]
    [Tooltip("If true, the sawmill will periodically sell an item from its output (or input) queue.")]
    [SerializeField] private bool enablePassiveSales = true;
    [Tooltip("How often (in seconds) the sawmill sells a single item.")]
    [SerializeField] private float passiveSaleInterval = 120f;
    [Tooltip("Multiplier applied to the item's true value when sold passively. (0.5 = 50% value)")]
    [SerializeField] private float passiveSaleValuePenalty = 0.5f;

    // ── Upgrade Slots ─────────────────────────────────────────────────
    [HideInInspector] public float millingSpeedMultiplier = 1f;
    [HideInInspector] public int boardsPerLogBonus = 0;
    [HideInInspector] public bool canProduceLiveEdge = false;
    [HideInInspector] public bool canProduceTurningBlanks = false;
    [HideInInspector] public int logQueueCapacity = 3;

    // ── State ─────────────────────────────────────────────────────────
    public bool IsProcessing { get; private set; } = false;
    public float ProcessingProgress { get; private set; } = 0f;
    public LumberItem CurrentLog { get; private set; }

    [Header("Visual")]
    [SerializeField] private ParticleSystem sawdustParticles;
    [SerializeField] private SpriteRenderer sawBladeSprite;

    private void Start()
    {
        // Auto-set input/output positions
        // Always use ground Y of -60 regardless of which object this script is on
        float groundY = -60f;
        float worldX = transform.position.x;

        if (InputPosition == Vector3.zero)
            InputPosition = new Vector3(worldX + inputOffsetX, groundY, 0f);

        if (OutputPosition == Vector3.zero)
            OutputPosition = new Vector3(worldX + outputOffsetX, groundY, 0f);

        Debug.Log($"[Sawmill] InputPosition: {InputPosition}, OutputPosition: {OutputPosition}");

        StartCoroutine(MillingLoop());
        StartCoroutine(PassiveSalesLoop());
    }

    private IEnumerator PassiveSalesLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(passiveSaleInterval);

            if (!enablePassiveSales) continue;

            var inv = InventoryManager.Instance;
            var gm = GameManager.Instance;
            if (inv == null || gm == null) continue;

            // Prioritize selling from Output (Boards), fallback to Input (Logs)
            LumberItem itemToSell = null;
            Vector3 popUpTextPos = OutputPosition;

            if (inv.HasItems(InventoryManager.InventoryZone.MillOutput))
            {
                itemToSell = inv.Dequeue(InventoryManager.InventoryZone.MillOutput);
                popUpTextPos = OutputPosition;
            }
            else if (inv.HasItems(InventoryManager.InventoryZone.MillInput))
            {
                itemToSell = inv.Dequeue(InventoryManager.InventoryZone.MillInput);
                popUpTextPos = InputPosition;
            }

            if (itemToSell != null && itemToSell.species != null)
            {
                float baseValue = itemToSell.CurrentMarketValue;
                float multiplier = gm.GetSaleMultiplier(itemToSell.species);
                float finalSalePrice = Mathf.Round(baseValue * multiplier * passiveSaleValuePenalty);

                // Ensure at least 1 gold is granted
                finalSalePrice = Mathf.Max(1f, finalSalePrice);

                gm.EarnGold(finalSalePrice);
                gm.RegisterItemSold(itemToSell, finalSalePrice);

                Debug.Log($"[Sawmill] Passively sold {itemToSell.DisplayName} for {finalSalePrice}g.");

                if (Sawmill.UI.WorldTextManager.Instance != null)
                {
                    Sawmill.UI.WorldTextManager.Instance.SpawnStaticText(
                        popUpTextPos, 
                        $"+{finalSalePrice:0}g", 
                        new Color(1f, 0.84f, 0f) // Goldish yellow
                    );
                }
            }
        }
    }

    private IEnumerator MillingLoop()
    {
        while (true)
        {
            var inv = InventoryManager.Instance;

            if (inv != null && inv.HasItems(InventoryManager.InventoryZone.MillInput) && !IsProcessing)
            {
                CurrentLog = inv.Dequeue(InventoryManager.InventoryZone.MillInput);
                if (CurrentLog != null)
                    yield return StartCoroutine(ProcessLog(CurrentLog));
            }
            else
            {
                SetRunning(false);
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    private IEnumerator ProcessLog(LumberItem log)
    {
        IsProcessing = true;
        ProcessingProgress = 0f;
        SetRunning(true);

        float duration = baseMillingTime * millingSpeedMultiplier;
        if (log.species != null)
            duration = log.species.millingTimePerLog * millingSpeedMultiplier;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ProcessingProgress = elapsed / duration;
            yield return null;
        }

        ProduceBoards(log);
        CurrentLog = null;
        IsProcessing = false;
        ProcessingProgress = 0f;
    }

    private void ProduceBoards(LumberItem log)
    {
        var inv = InventoryManager.Instance;
        if (inv == null || log.species == null) return;

        int boardCount = log.species.boardsPerLog + boardsPerLogBonus;

        if (canProduceLiveEdge && UnityEngine.Random.value < 0.15f)
        {
            var slab = new LumberItem(log.species, ProcessingStage.RoughSawn, LumberItemType.Slab)
            {
                isLiveEdge = true
            };
            inv.AddItem(slab, InventoryManager.InventoryZone.MillOutput);
            GameManager.Instance?.RegisterItemProduced(slab);
            OnBoardMilled?.Invoke(slab);
            boardCount -= 2;
        }

        if (canProduceTurningBlanks && UnityEngine.Random.value < 0.2f && boardCount > 1)
        {
            var blank = new LumberItem(log.species, ProcessingStage.RoughSawn, LumberItemType.TurningBlank);
            inv.AddItem(blank, InventoryManager.InventoryZone.MillOutput);
            GameManager.Instance?.RegisterItemProduced(blank);
            OnBoardMilled?.Invoke(blank);
            boardCount--;
        }

        boardCount = Mathf.Max(1, boardCount);
        for (int i = 0; i < boardCount; i++)
        {
            var board = new LumberItem(log.species, ProcessingStage.RoughSawn, LumberItemType.Board);
            inv.AddItem(board, InventoryManager.InventoryZone.MillOutput);
            GameManager.Instance?.RegisterItemProduced(board);
            OnBoardMilled?.Invoke(board);
        }

        Debug.Log($"[Sawmill] Milled {log.species.speciesName} log into {boardCount} boards.");

        if (sawdustParticles != null && !sawdustParticles.isPlaying)
            sawdustParticles.Play();
    }

    private void SetRunning(bool running)
    {
        if (sawdustParticles != null)
        {
            if (running && !sawdustParticles.isPlaying) sawdustParticles.Play();
            else if (!running && sawdustParticles.isPlaying) sawdustParticles.Stop();
        }
    }

    public string GetStatusText()
    {
        if (IsProcessing && CurrentLog?.species != null)
            return $"Milling {CurrentLog.species.speciesName}... {ProcessingProgress * 100f:F0}%";
        return "Awaiting logs";
    }
}
