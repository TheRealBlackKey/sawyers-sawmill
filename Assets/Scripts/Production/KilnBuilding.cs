using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// The kiln building. Accepts rough sawn boards physically delivered by Sawyer,
/// dries them over time, and outputs kiln-dried lumber to KilnOutput inventory.
///
/// FIX (Session 4): Removed KilnAutoLoad() coroutine. The kiln no longer
/// pulls boards from inventory automatically. Sawyer must physically carry
/// boards here and call LoadBoard(). This makes Sawyer visibly walk to the kiln.
///
/// InputPosition  — where Sawyer walks to DROP a board off
/// OutputPosition — where Sawyer walks to PICK UP a dried board
/// </summary>
public class KilnBuilding : MonoBehaviour
{
    public static event Action<LumberItem, bool> OnBoardDried;  // item, wasLost
    public static event Action<LumberItem> OnRareColorFound;

    [Header("Positions")]
    public Vector3 InputPosition;
    public Vector3 OutputPosition;

    [Header("Kiln Settings")]
    [SerializeField] private KilnType kilnType = KilnType.Electric;
    [SerializeField] private int maxSimultaneousBoards = 1;

    // Upgrade-set values
    [HideInInspector] public float dryingSpeedMultiplier = 1f;
    [HideInInspector] public float checkingChance  = 0.05f;
    [HideInInspector] public float rareColorChance = 0.01f;
    [HideInInspector] public float spaltingChance  = 0f;
    [HideInInspector] public int   slotCapacity    = 4;

    public bool IsFull => _dryingSlots.Count >= slotCapacity;

    private List<KilnSlot> _dryingSlots = new List<KilnSlot>();

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer _kilnSprite;
    [SerializeField] private ParticleSystem steamParticles;
    [SerializeField] private ParticleSystem smokeParticles;

    private void Start()
    {
        // Auto-set positions relative to this building's world position
        // so they work regardless of where the building was placed
        float x = transform.position.x;
        float y = transform.position.y;
        InputPosition  = new Vector3(x - 20f, y, 0f);
        OutputPosition = new Vector3(x + 20f, y, 0f);

        // Only the drying tick runs — NO auto-load loop
        StartCoroutine(DryingTick());
    }

    // ── Physical delivery by Sawyer ───────────────────────────────────

    /// <summary>
    /// Called by SawyerWorker when he physically arrives at InputPosition.
    /// This is the ONLY way boards enter the kiln.
    /// </summary>
    public void LoadBoard(LumberItem board)
    {
        if (IsFull)
        {
            Debug.LogWarning("[Kiln] LoadBoard called but kiln is full — Sawyer shouldn't have come here.");
            return;
        }

        float dryTime = board.species != null
            ? board.species.kilnDryTimeSeconds * dryingSpeedMultiplier
            : 120f * dryingSpeedMultiplier;

        if (kilnType == KilnType.AirDrying) dryTime *= 5f;
        if (kilnType == KilnType.Solar)     dryTime *= 2f;
        if (kilnType == KilnType.Steam)     dryTime *= 0.6f;

        _dryingSlots.Add(new KilnSlot(board, dryTime));
        Debug.Log($"[Kiln] Loaded {board.DisplayName} — drying for {dryTime:F0}s ({_dryingSlots.Count}/{slotCapacity} slots)");
        UpdateVisuals();
    }

    // ── Drying Tick ───────────────────────────────────────────────────

    private IEnumerator DryingTick()
    {
        while (true)
        {
            var completed = new List<KilnSlot>();

            foreach (var slot in _dryingSlots)
            {
                slot.elapsed += Time.deltaTime;
                slot.item.moistureContent = Mathf.Lerp(1f, 0.08f, slot.elapsed / slot.duration);

                if (slot.elapsed >= slot.duration)
                    completed.Add(slot);
            }

            foreach (var slot in completed)
            {
                _dryingSlots.Remove(slot);
                FinishDrying(slot.item);
            }

            yield return null;
        }
    }

    private void FinishDrying(LumberItem board)
    {
        board.moistureContent = 0.08f;
        board.stage = ProcessingStage.KilnDried;

        bool boardLost = false;

        if (UnityEngine.Random.value < checkingChance)
            board.quality = QualityGrade.Rustic;

        if (UnityEngine.Random.value < rareColorChance)
        {
            board.quality = QualityGrade.Select;
            OnRareColorFound?.Invoke(board);
        }

        if (spaltingChance > 0f && UnityEngine.Random.value < spaltingChance)
        {
            float spaltRoll = UnityEngine.Random.value;
            if (spaltRoll < 0.1f)
            {
                boardLost = true;
                Debug.Log($"[Kiln] {board.DisplayName} rotted in the humidity chamber.");
            }
            else
            {
                board.isSpalted = true;
                if (spaltRoll < 0.3f)
                    board.quality = QualityGrade.Premium;
            }
        }

        OnBoardDried?.Invoke(board, boardLost);

        if (!boardLost)
        {
            // Board goes to KilnOutput — Sawyer will come pick it up
            InventoryManager.Instance?.AddItem(board, InventoryManager.InventoryZone.KilnOutput);
            GameManager.Instance?.RegisterItemProduced(board);
            Debug.Log($"[Kiln] Finished drying {board.DisplayName} — ready for Sawyer to collect.");
        }

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        float activity = (float)_dryingSlots.Count / Mathf.Max(1, slotCapacity);

        if (steamParticles != null)
        {
            var emission = steamParticles.emission;
            emission.rateOverTime = activity * 15f;
            if (activity > 0 && !steamParticles.isPlaying) steamParticles.Play();
            else if (activity == 0 && steamParticles.isPlaying) steamParticles.Stop();
        }
    }

    // ── Status for UI ─────────────────────────────────────────────────

    public string GetStatusText()
    {
        if (_dryingSlots.Count == 0)
        {
            int waiting = InventoryManager.Instance?.Count(InventoryManager.InventoryZone.KilnOutput) ?? 0;
            return waiting > 0 ? $"Kiln idle — {waiting} board(s) ready for collection" : "Kiln idle";
        }
        return $"Drying {_dryingSlots.Count}/{slotCapacity} board(s)";
    }

    public List<KilnSlotInfo> GetSlotInfo()
    {
        var info = new List<KilnSlotInfo>();
        foreach (var slot in _dryingSlots)
            info.Add(new KilnSlotInfo
            {
                itemName      = slot.item.DisplayName,
                progress      = slot.elapsed / slot.duration,
                timeRemaining = slot.duration - slot.elapsed
            });
        return info;
    }

    // ── Nested Types ──────────────────────────────────────────────────

    private class KilnSlot
    {
        public LumberItem item;
        public float duration;
        public float elapsed;

        public KilnSlot(LumberItem item, float duration)
        {
            this.item     = item;
            this.duration = duration;
            this.elapsed  = 0f;
        }
    }

    public enum KilnType { AirDrying, Electric, Solar, Steam }

    [Serializable]
    public class KilnSlotInfo
    {
        public string itemName;
        public float  progress;
        public float  timeRemaining;
    }
}
