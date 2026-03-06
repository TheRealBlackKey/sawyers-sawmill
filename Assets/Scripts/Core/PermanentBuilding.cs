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

    [Tooltip("Offset the grid footprint relative to the sprite center")]
    [SerializeField] private Vector2Int footprintOffset = Vector2Int.zero;

    [Tooltip("How many grid cells UP from the bottom edge should the depth-sorting evaluate? Base 1 for typical buildings.")]
    [SerializeField] private float depthSortRowOffset = 1f;

    [Header("Legacy (unused — kept for reference)")]
    [HideInInspector] public Vector2 footprintSize = new Vector2(80f, 50f);

    private bool _registered = false;

    private void Start()
    {
        RegisterWithWorldGrid();
        UpdateDepthSorting();
    }

    private void UpdateDepthSorting()
    {
        if (WorldGrid.Instance == null) return;
        
        Vector2Int origin = GetGridOrigin();
        float cs = WorldGrid.Instance.CellSize;
        
        // Find the exact world center of the occupied grid rect
        Vector3 topLeft = WorldGrid.Instance.GridToWorld(origin.x, origin.y);
        
        Vector3 trueCenter = new Vector3(
            topLeft.x + (gridWidth - 1) * cs * 0.5f,
            topLeft.y - (gridHeight - 1) * cs * 0.5f,
            0f);

        // Shift the sort anchor UP by depthSortRowOffset (default 1 cell) so characters 
        // standing in the bottom row of the footprint render in *front* of the building.
        float bottomWorldY = trueCenter.y - (gridHeight * cs * 0.5f) + (depthSortRowOffset * cs);
        int baseSortingOrder = Mathf.RoundToInt(-bottomWorldY * 10f) + 10000;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            // Accumulate so relative layering (e.g., blades over base) is preserved
            sr.sortingOrder += baseSortingOrder;
        }
    }

    private Vector2Int GetGridOrigin()
    {
        if (WorldGrid.Instance == null) return Vector2Int.zero;
        
        // Find the grid cell nearest to the sprite's transform
        Vector2Int cell = WorldGrid.Instance.WorldToGrid(transform.position);
        
        // Center the footprint over that cell, then apply the manual offset
        int originX = cell.x - gridWidth / 2 + footprintOffset.x;
        int originY = cell.y - gridHeight / 2 + footprintOffset.y;
        
        return new Vector2Int(originX, originY);
    }

    private void RegisterWithWorldGrid()
    {
        if (WorldGrid.Instance == null)
        {
            Debug.LogWarning($"[PermanentBuilding] WorldGrid not found — " +
                             $"{gameObject.name} won't block placement.");
            return;
        }

        Vector2Int origin = GetGridOrigin();
        
        // Ensure bounds validation happens on mathematical cells, not visual
        if (!WorldGrid.Instance.InBounds(origin.x, origin.y) || 
            !WorldGrid.Instance.InBounds(origin.x + gridWidth - 1, origin.y + gridHeight - 1))
        {
            Debug.LogWarning($"[PermanentBuilding] {gameObject.name} footprint extends outside bounds!");
            return;
        }

        int originX = Mathf.Max(0, origin.x);
        int originY = Mathf.Max(0, origin.y);

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
        
        Vector2Int origin = GetGridOrigin();
        float cs = WorldGrid.Instance.CellSize;
        
        // Find the exact world center of the occupied grid rect
        Vector3 topLeft = WorldGrid.Instance.GridToWorld(origin.x, origin.y);
        
        Vector3 trueCenter = new Vector3(
            topLeft.x + (gridWidth - 1) * cs * 0.5f,
            topLeft.y - (gridHeight - 1) * cs * 0.5f,
            0f);

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
        Gizmos.DrawCube(trueCenter, new Vector3(gridWidth * cs, gridHeight * cs, 1f));
        
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 1f);
        Gizmos.DrawWireCube(trueCenter, new Vector3(gridWidth * cs, gridHeight * cs, 1f));
    }
}
