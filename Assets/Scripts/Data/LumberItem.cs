using UnityEngine;

/// <summary>
/// Represents a single piece of wood (log, board, slab, etc.) 
/// as it moves through the production pipeline.
/// This is a runtime data object — not a MonoBehaviour.
/// </summary>
[System.Serializable]
public class LumberItem
{
    // ── Identity ─────────────────────────────────────────────────────
    public string itemId;                   // Unique runtime ID
    public WoodSpeciesData species;
    public ProcessingStage stage;
    public GrainVariant grainVariant;       // Null until surfacing reveals it
    public LumberItemType itemType;

    // ── Quality & Value ───────────────────────────────────────────────
    public QualityGrade quality = QualityGrade.Standard;
    public bool isGrainRevealed = false;
    public bool isSpalted = false;
    public bool isLiveEdge = false;         // Whole slab cut — bonus value, slower to process
    public float moistureContent = 1f;     // 1 = green wood, 0 = fully dried (target ~0.08)

    // ── Finishing ─────────────────────────────────────────────────────
    public FinishType appliedFinish = FinishType.None;
    public bool hasResinFill = false;

    // ── AI Tracking ───────────────────────────────────────────────────
    public bool IsClaimed { get; set; } = false;

    // ── Computed Properties ───────────────────────────────────────────
    public float CurrentMarketValue
    {
        get
        {
            if (species == null) return 0f;

            float value = species.GetBoardValue(stage, isGrainRevealed ? grainVariant : null);

            // Quality modifiers
            value *= quality switch
            {
                QualityGrade.Rustic  => 0.6f,
                QualityGrade.Standard => 1f,
                QualityGrade.Select  => 1.4f,
                QualityGrade.Premium => 2f,
                QualityGrade.Heirloom => 3.5f,
                _ => 1f
            };

            // Bonus modifiers
            if (isSpalted)   value *= 1.5f;
            if (isLiveEdge)  value *= 2f;
            if (hasResinFill) value *= 1.8f;

            value *= appliedFinish switch
            {
                FinishType.OilFinish    => 1.2f,
                FinishType.DanishOil    => 1.35f,
                FinishType.Lacquer      => 1.5f,
                FinishType.HandRubWax   => 1.6f,
                FinishType.Specialty    => 2f,
                _ => 1f
            };

            return Mathf.Round(value * 100f) / 100f;
        }
    }

    public string DisplayName
    {
        get
        {
            string prefix = isGrainRevealed && grainVariant != null ? grainVariant.variantName + " " : "";
            string spalt = isSpalted ? "Spalted " : "";
            string liveEdge = isLiveEdge ? "Live Edge " : "";
            string stageName = stage switch
            {
                ProcessingStage.Log       => "Log",
                ProcessingStage.RoughSawn => "Rough Sawn Board",
                ProcessingStage.KilnDried => "Kiln Dried Board",
                ProcessingStage.Surfaced  => "Surfaced Board",
                ProcessingStage.Finished  => "Finished Board",
                _ => "Board"
            };
            return $"{prefix}{spalt}{liveEdge}{species?.speciesName} {stageName}";
        }
    }

    // ── Constructor ───────────────────────────────────────────────────
    public LumberItem(WoodSpeciesData species, ProcessingStage stage, LumberItemType type = LumberItemType.Board)
    {
        this.itemId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        this.species = species;
        this.stage = stage;
        this.itemType = type;

        // Green logs start at full moisture
        moistureContent = (stage == ProcessingStage.Log) ? 1f : 0.5f;
    }

    public LumberItem() { }
}

public enum LumberItemType
{
    Log,
    Board,
    Slab,           // Live edge, wider
    TurningBlank,   // For the lathe
    VeneerSheet,
    Stave           // For cooperage
}

public enum QualityGrade
{
    Rustic,         // Checking, defects — still sellable at discount
    Standard,
    Select,
    Premium,
    Heirloom        // Rare figured pieces at peak processing
}

public enum FinishType
{
    None,
    OilFinish,
    DanishOil,
    Lacquer,
    HandRubWax,
    Specialty       // Milk paint, ebonizing, etc.
}
