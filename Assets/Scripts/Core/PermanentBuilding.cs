using UnityEngine;

/// <summary>
/// Attach to any building that is permanently placed in the world from the start
/// (i.e. Sawyer's Sawmill). Registers itself with WorldGrid so PlacementManager
/// will never allow building on top of it.
///
/// SETUP: Set gridWidth/gridHeight to match how many grid cells this building
/// occupies. The building's Transform position is used as the world anchor —
/// WorldGrid will find the nearest grid cell automatically.
/// </summary>
public class PermanentBuilding : MonoBehaviour
{
    [Header("Grid Footprint (in cells)")]
    [SerializeField] private int gridWidth  = 3;
    [SerializeField] private int gridHeight = 2;

    [Header("Legacy (unused — kept for reference)")]
    [HideInInspector] public Vector2 footprintSize = new Vector2(80f, 50f);

    private bool _registered = false;

    private void Start()
    {
        RegisterWithWorldGrid();
    }

    private void RegisterWithWorldGrid()
    {
        if (WorldGrid.Instance == null)
        {
            Debug.LogWarning($"[PermanentBuilding] WorldGrid not found — " +
                             $"{gameObject.name} won't block placement.");
            return;
        }

        // Find the grid cell nearest to this building's world position
        Vector2Int cell = WorldGrid.Instance.WorldToGrid(transform.position);

        if (!WorldGrid.Instance.InBounds(cell.x, cell.y))
        {
            Debug.LogWarning($"[PermanentBuilding] {gameObject.name} at {transform.position} " +
                             $"is outside the WorldGrid bounds — cannot register.");
            return;
        }

        // Offset so the building is centered on its footprint rather than
        // anchored to the top-left cell
        int originX = Mathf.Max(0, cell.x - gridWidth  / 2);
        int originY = Mathf.Max(0, cell.y - gridHeight / 2);

        bool success = WorldGrid.Instance.Occupy(
            originX, originY,
            gridWidth, gridHeight,
            CellType.Building,
            gameObject,
            walkable: false);

        if (success)
        {
            _registered = true;
            Debug.Log($"[PermanentBuilding] {gameObject.name} occupies grid cells " +
                      $"({originX},{originY}) to ({originX+gridWidth-1},{originY+gridHeight-1}).");
        }
        else
        {
            Debug.LogWarning($"[PermanentBuilding] {gameObject.name} could not register — " +
                             $"cells already occupied. Try adjusting its position.");
        }
    }

    private void OnDestroy()
    {
        if (_registered && WorldGrid.Instance != null)
            WorldGrid.Instance.Free(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (WorldGrid.Instance == null) return;
        float cs = WorldGrid.Instance.CellSize;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
        Gizmos.DrawCube(transform.position,
            new Vector3(gridWidth * cs, gridHeight * cs, 1f));
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 1f);
        Gizmos.DrawWireCube(transform.position,
            new Vector3(gridWidth * cs, gridHeight * cs, 1f));
    }
}
