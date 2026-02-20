using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// The surfacing building. Reveals grain in kiln-dried boards physically
/// delivered by Sawyer. This is the game's "slot machine" moment —
/// the sawdust clears and the figure is revealed.
///
/// FIX (Session 4): Removed SurfacingLoop() auto-pull coroutine.
/// Boards only enter via ProcessBoard(), called when Sawyer arrives.
/// Sawyer watches for SurfacingOutput to fill, then carries to market.
///
/// InputPosition  — where Sawyer walks to DROP off a dried board
/// OutputPosition — where Sawyer walks to PICK UP a surfaced board
/// </summary>
public class SurfacingBuilding : MonoBehaviour
{
    public static event Action<LumberItem, GrainVariant> OnGrainRevealed;
    public static event Action<LumberItem, GrainVariant> OnRareGrainFound;

    [Header("Positions")]
    public Vector3 InputPosition;
    public Vector3 OutputPosition;

    [Header("Base Stats")]
    [SerializeField] private float baseSurfacingTime = 30f;

    // ── Upgrade slots ─────────────────────────────────────────────────
    [HideInInspector] public float speedMultiplier  = 1f;
    [HideInInspector] public float rarityBonus      = 0f;
    [HideInInspector] public bool  hasJointer       = true;
    [HideInInspector] public bool  hasPlaner        = false;
    [HideInInspector] public bool  hasDrumSander    = false;
    [HideInInspector] public bool  hasWideBeltSander = false;
    [HideInInspector] public bool  hasCNCRouter     = false;
    [HideInInspector] public int   slotCapacity     = 1;

    public bool IsFull        => _processingCount >= slotCapacity;
    public bool IsProcessing  => _processingCount > 0;
    public float ProcessingProgress { get; private set; } = 0f;
    public LumberItem CurrentBoard  { get; private set; }

    private int _processingCount = 0;

    [Header("Reveal Effects")]
    [SerializeField] private ParticleSystem sawdustRevealParticles;
    [SerializeField] private GameObject     grainRevealEffectPrefab;
    [SerializeField] private SpriteRenderer boardDisplaySprite;

    private void Start()
    {
        // Auto-set positions relative to world position
        float x = transform.position.x;
        float y = transform.position.y;
        InputPosition  = new Vector3(x - 20f, y, 0f);
        OutputPosition = new Vector3(x + 20f, y, 0f);

        // No auto-loop — boards only enter via ProcessBoard()
    }

    // ── Physical delivery by Sawyer ───────────────────────────────────

    /// <summary>
    /// Called by SawyerWorker when he physically arrives at InputPosition
    /// carrying a kiln-dried board. This is the ONLY way boards enter surfacing.
    /// </summary>
    public void ProcessBoard(LumberItem board)
    {
        if (IsFull)
        {
            Debug.LogWarning("[Surfacing] ProcessBoard called but station is full.");
            return;
        }

        Debug.Log($"[Surfacing] Starting to surface {board.DisplayName}...");
        StartCoroutine(SurfaceBoard(board));
    }

    // ── Processing ────────────────────────────────────────────────────

    private IEnumerator SurfaceBoard(LumberItem board)
    {
        _processingCount++;
        CurrentBoard = board;

        if (boardDisplaySprite != null && board.species != null)
            boardDisplaySprite.sprite = board.species.boardSprite;

        float duration = GetSurfacingDuration(board);
        float elapsed  = 0f;

        if (sawdustRevealParticles != null)
            sawdustRevealParticles.Play();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ProcessingProgress = elapsed / duration;
            yield return null;
        }

        yield return StartCoroutine(RevealGrain(board));

        _processingCount--;
        CurrentBoard       = null;
        ProcessingProgress = 0f;
    }

    private IEnumerator RevealGrain(LumberItem board)
    {
        if (sawdustRevealParticles != null)
        {
            sawdustRevealParticles.Stop();
            yield return new WaitForSeconds(0.8f);
        }

        // ── Unlock new grain variants based on boards processed ───────
        if (board.species != null && board.species.grainVariants.Count > 0)
        {
            int boardsProcessed = GameManager.Instance?.GetBoardsProcessed(board.species) ?? 0;

            foreach (var variant in board.species.grainVariants)
            {
                if (!variant.isUnlocked && boardsProcessed >= variant.boardsToUnlock)
                {
                    variant.isUnlocked = true;
                    Debug.Log($"[Surfacing] Unlocked grain variant: {variant.variantName} for {board.species.speciesName}!");
                }
            }

            // ── Roll for grain variant ────────────────────────────────
            var unlocked = board.species.grainVariants.FindAll(v => v.isUnlocked);
            if (unlocked.Count > 0)
            {
                var rolled = board.species.RollGrainVariant(rarityBonus);
                board.grainVariant    = rolled;
                board.isGrainRevealed = true;

                if (rolled != null)
                {
                    board.quality = rolled.rarityTier switch
                    {
                        0 => QualityGrade.Standard,
                        1 => QualityGrade.Select,
                        2 => QualityGrade.Premium,
                        3 => QualityGrade.Heirloom,
                        _ => QualityGrade.Standard
                    };

                    if (boardDisplaySprite != null && rolled.boardSprite != null)
                    {
                        boardDisplaySprite.sprite = rolled.boardSprite;
                        boardDisplaySprite.color  = rolled.grainTintColor;
                    }

                    if (rolled.rarityTier >= 2)
                    {
                        if (grainRevealEffectPrefab != null)
                            Instantiate(grainRevealEffectPrefab, transform.position, Quaternion.identity);

                        OnRareGrainFound?.Invoke(board, rolled);
                        GameManager.Instance?.NotifyRareGrainRevealed(rolled, board.species);

                        yield return new WaitForSeconds(1.5f);
                    }
                    else
                    {
                        OnGrainRevealed?.Invoke(board, rolled);
                    }
                }
            }
        }

        // ── Finalise board ────────────────────────────────────────────
        board.stage = ProcessingStage.Surfaced;

        if (hasDrumSander && board.quality < QualityGrade.Select)
            board.quality = QualityGrade.Select;
        if (hasWideBeltSander && board.quality < QualityGrade.Premium && board.grainVariant?.rarityTier >= 2)
            board.quality = QualityGrade.Premium;

        // Board goes to SurfacingOutput — Sawyer will come collect it
        InventoryManager.Instance?.AddItem(board, InventoryManager.InventoryZone.SurfacingOutput);
        GameManager.Instance?.RegisterItemProduced(board);
        Debug.Log($"[Surfacing] Finished {board.DisplayName} ({board.quality}) — ready for Sawyer to collect.");

        if (boardDisplaySprite != null)
        {
            boardDisplaySprite.sprite = null;
            boardDisplaySprite.color  = Color.white;
        }
    }

    private float GetSurfacingDuration(LumberItem board)
    {
        float time = board.species != null ? board.species.surfaceTimeSeconds : baseSurfacingTime;
        if (hasPlaner)    time *= 0.85f;
        if (hasDrumSander) time *= 0.9f;
        time *= speedMultiplier;
        return time;
    }

    public string GetStatusText()
    {
        if (IsProcessing && CurrentBoard?.species != null)
            return $"Surfacing {CurrentBoard.species.speciesName}... {ProcessingProgress * 100f:F0}%";

        int waiting = InventoryManager.Instance?.Count(InventoryManager.InventoryZone.SurfacingOutput) ?? 0;
        return waiting > 0 ? $"Ready — {waiting} board(s) awaiting delivery" : "Surfacing station idle";
    }
}
