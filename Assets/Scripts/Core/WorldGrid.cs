using UnityEngine;
using System.Collections.Generic;
using SystemRandom = System.Random;

/// <summary>
/// WorldGrid — spatial authority for the game world.
/// Reads all dimensions from StripConfig so changing stripHeight
/// automatically resizes the grid, camera bounds, and background together.
///
/// CELL COORDINATE SYSTEM:
///   (0,0) = top-left cell. X increases right, Y increases DOWN.
///   World (0, StripConfig.StripCenterY) = center of the strip.
/// </summary>
public class WorldGrid : MonoBehaviour
{
    public static WorldGrid Instance { get; private set; }

    [Header("Configuration")]
    [Tooltip("Assign the StripConfig asset here. " +
             "Create it via: Right-click > Create > SawyersSawmill > Strip Config")]
    [SerializeField] private StripConfig config;

    [Header("Obstacle Generation")]
    [SerializeField] private bool randomizeSeedOnStart = true;
    public int obstacleSeed { get; set; } = 42;

    [Header("Obstacle Counts")]
    [SerializeField] private int boulderCount       = 6;
    [SerializeField] private int oldGrowthTreeCount = 4;
    [SerializeField] [Range(0, 15)] private int riverCount = 1;
    [Tooltip("Chance (0 to 1) per row that a river meanders left or right.")]
    [SerializeField] [Range(0f, 1f)] private float riverMeanderChance = 0.35f;
    [Tooltip("Maximum cells the river can wander away from its starting column.")]
    [SerializeField] private int riverMaxWander = 5;

    [Header("Debug")]
    [SerializeField] private bool showGridGizmos     = true;
    [SerializeField] private bool showObstacleGizmos = true;

    // ── Derived from config ───────────────────────────────────────────
    private int   _columns;
    private int   _rows;
    private float _cellSize;
    private Vector3 _worldOrigin;  // world position of top-left corner of cell (0,0)

    // ── Grid Data ─────────────────────────────────────────────────────
    private GridCell[,] _cells;
    private Dictionary<GameObject, List<Vector2Int>> _occupancyMap
        = new Dictionary<GameObject, List<Vector2Int>>();

    // ── Public Accessors ──────────────────────────────────────────────
    public int   Columns  => _columns;
    public int   Rows     => _rows;
    public float CellSize => _cellSize;
    public float MinWorldX => _worldOrigin.x;
    public float MaxWorldX => _worldOrigin.x + _columns * _cellSize;
    public float TopWorldY => _worldOrigin.y;
    public float BottomWorldY => _worldOrigin.y - _rows * _cellSize;
    public StripConfig Config => config;

    // ── Lifecycle ─────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (config == null)
        {
            Debug.LogError("[WorldGrid] No StripConfig assigned! Create one via " +
                           "Right-click > Create > SawyersSawmill > Strip Config " +
                           "and assign it to the WorldGrid GameObject.");
            return;
        }

        if (randomizeSeedOnStart)
            obstacleSeed = UnityEngine.Random.Range(0, 99999);

        ApplyConfig();
        InitializeGrid();
        GenerateObstacles();

        Debug.Log($"[WorldGrid] Ready — {_columns}×{_rows} cells @ {_cellSize}u. " +
                  $"Strip: Y[{BottomWorldY:F0}..{TopWorldY:F0}], " +
                  $"X[{MinWorldX:F0}..{MaxWorldX:F0}]. Seed: {obstacleSeed}");
    }

    private void ApplyConfig()
    {
        _columns  = config.columns;
        _rows     = config.Rows;
        _cellSize = config.cellSize;

        // Top-left corner of cell (0,0) in world space
        // Grid top = StripTopY, horizontally centered
        _worldOrigin = new Vector3(
            -(_columns * _cellSize) / 2f,
             config.StripTopY,
            0f);
    }

    // ── Grid Initialization ───────────────────────────────────────────
    private void InitializeGrid()
    {
        _cells = new GridCell[_columns, _rows];
        for (int x = 0; x < _columns; x++)
        for (int y = 0; y < _rows;    y++)
        {
            _cells[x, y] = new GridCell
            {
                gridX = x, gridY = y,
                cellType = CellType.Empty, isWalkable = true, occupant = null
            };
        }
    }

    private bool IsInStartingSafeZone(int x, int y)
    {
        int centerX = _columns / 2;
        int centerY = _rows / 2;
        // Safe zone of ~12x10 cells around center (Sawmill is usually ~6x4)
        return Mathf.Abs(x - centerX) <= 6 && Mathf.Abs(y - centerY) <= 5;
    }

    // ── Obstacle Generation ───────────────────────────────────────────
    private void GenerateObstacles()
    {
        var rng = new SystemRandom(obstacleSeed);
        int centerX = _columns / 2;

        // Rivers — 1-cell-wide vertical columns, full height
        // Placed in middle 40% of map width, but avoid the center 12 columns
        // Track the starting columns of rivers to enforce spacing
        List<int> riverStartCols = new List<int>();

        for (int r = 0; r < riverCount; r++)
        {
            int col = 0;
            int attempts = 0;
            bool validPosition = false;

            do
            {
                // Spawn anywhere on the map (with a small 2-cell padding from the absolute edges)
                col = rng.Next(2, _columns - 2);
                attempts++;

                // Check 1: Must be outside the Sawmill Safe Zone
                if (Mathf.Abs(col - centerX) <= 6) continue;

                // Check 2: Enforce distance from other rivers
                bool tooClose = false;
                foreach (int existingCol in riverStartCols)
                {
                    // Minimum 4 spaces between river starts (plus max wander consideration if possible, but simplest is start spacing)
                    if (Mathf.Abs(col - existingCol) < 5) 
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    validPosition = true;
                }

            } while (!validPosition && attempts < 100);

            // Fallback: If we couldn't find a perfectly spaced spot after 100 tries, we just accept the last rolled column 
            // as long as it isn't in the safe zone. This prevents infinite loops on small/crowded maps.
            if (!validPosition && Mathf.Abs(col - centerX) <= 6)
            {
                // Force it outside the safe zone manually if the fallback failed entirely
                col = rng.NextDouble() > 0.5 ? centerX + 7 : centerX - 7; 
            }

            riverStartCols.Add(col);

            int startCol = col;

            for (int y = 0; y < _rows; y++)
            {
                // We are at (col, y). Draw water here.
                SetObstacle(col, y, CellType.Water);
                
                // Track FlowDirection here (Top-down logic)
                _cells[col, y].flowDirection |= FlowDirection.South; 
                if (y > 0)
                {
                    _cells[col, y].flowDirection |= FlowDirection.North;
                }

                // Do we meander for the NEXT row?
                if (y < _rows - 1 && rng.NextDouble() < riverMeanderChance)
                {
                    int meanderDir = rng.NextDouble() > 0.5 ? 1 : -1;
                    int nextCol = col + meanderDir;

                    // Clamp Meander to max wander radius and keep it away from map edges (let's say 4 padding)
                    if (Mathf.Abs(nextCol - startCol) <= riverMaxWander && nextCol >= 4 && nextCol < _columns - 4)
                    {
                        // Check that it's not piercing the safe zone (center X +/- 6)
                        if (!IsInStartingSafeZone(nextCol, y))
                        {
                            // It's safe to turn. The current cell turns East or West.
                            if (meanderDir > 0)
                                _cells[col, y].flowDirection |= FlowDirection.East;
                            else
                                _cells[col, y].flowDirection |= FlowDirection.West;

                            // Move to new col on the SAME row to build the orthogonal bridge
                            col = nextCol;
                            SetObstacle(col, y, CellType.Water);
                            
                            // The new cell receives the flow from the bridge we just made
                            if (meanderDir > 0)
                                _cells[col, y].flowDirection |= FlowDirection.West; // Flow came from West
                            else
                                _cells[col, y].flowDirection |= FlowDirection.East; // Flow came from East
                                
                            // Ensure the bridge also knows it goes down to the next row
                            _cells[col, y].flowDirection |= FlowDirection.South;
                        }
                    }
                }
            }
            Debug.Log($"[WorldGrid] River generated starting at column {startCol}");
        }

        // Boulders — single cells, avoid leftmost 8 and rightmost 4 cols, and safe zone
        int placed = 0;
        int attemptsBoulder = 0;
        while (placed < boulderCount && attemptsBoulder < 300)
        {
            attemptsBoulder++;
            int bx = rng.Next(8, _columns - 4);
            int by = rng.Next(0, _rows);
            if (!IsInStartingSafeZone(bx, by) && _cells[bx, by].cellType == CellType.Empty)
            { SetObstacle(bx, by, CellType.Boulder); placed++; }
        }

        // Old Growth Trees — 2×2 clusters, same exclusion zones
        int ogPlaced = 0;
        int attemptsOG = 0;
        while (ogPlaced < oldGrowthTreeCount && attemptsOG < 300)
        {
            attemptsOG++;
            int ox = rng.Next(8, _columns - 5);
            int oy = rng.Next(0, _rows - 1);
            
            bool safe = !IsInStartingSafeZone(ox, oy) && !IsInStartingSafeZone(ox + 1, oy) &&
                        !IsInStartingSafeZone(ox, oy + 1) && !IsInStartingSafeZone(ox + 1, oy + 1);

            if (safe && AllEmpty(ox, oy, 2, 2))
            {
                for (int dx = 0; dx < 2; dx++)
                for (int dy = 0; dy < 2; dy++)
                    SetObstacle(ox + dx, oy + dy, CellType.OldGrowthTree);
                ogPlaced++;
            }
        }

        Debug.Log($"[WorldGrid] Obstacles: {riverCount} river(s), {placed} boulder(s), {ogPlaced} old growth.");
    }

    private void SetObstacle(int x, int y, CellType type)
    {
        if (!InBounds(x, y)) return;
        _cells[x, y].cellType   = type;
        _cells[x, y].isWalkable = false;
    }

    private bool AllEmpty(int sx, int sy, int w, int h)
    {
        for (int dx = 0; dx < w; dx++)
        for (int dy = 0; dy < h; dy++)
        {
            int cx = sx + dx, cy = sy + dy;
            if (!InBounds(cx, cy) || _cells[cx, cy].cellType != CellType.Empty) return false;
        }
        return true;
    }

    // ── Coordinate Conversion ─────────────────────────────────────────

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int gx = Mathf.FloorToInt((worldPos.x - _worldOrigin.x) / _cellSize);
        int gy = Mathf.FloorToInt((_worldOrigin.y - worldPos.y) / _cellSize);
        if (!InBounds(gx, gy)) return new Vector2Int(-1, -1);
        return new Vector2Int(gx, gy);
    }

    public Vector3 GridToWorld(int gridX, int gridY, float z = 0f)
    {
        float wx = _worldOrigin.x + (gridX + 0.5f) * _cellSize;
        float wy = _worldOrigin.y - (gridY + 0.5f) * _cellSize;
        return new Vector3(wx, wy, z);
    }

    public Vector3 GridToWorld(Vector2Int g, float z = 0f) => GridToWorld(g.x, g.y, z);

    public Vector3 SnapToGrid(Vector3 worldPos, float z = 0f)
    {
        var g = WorldToGrid(worldPos);
        return InBounds(g.x, g.y) ? GridToWorld(g.x, g.y, z) : worldPos;
    }

    // ── Bounds & Queries ──────────────────────────────────────────────

    public bool InBounds(int x, int y) => x >= 0 && x < _columns && y >= 0 && y < _rows;
    public bool InBounds(Vector2Int g)  => InBounds(g.x, g.y);

    public GridCell GetCell(int x, int y)      => InBounds(x, y) ? _cells[x, y] : GridCell.Invalid;
    public GridCell GetCell(Vector2Int g)       => GetCell(g.x, g.y);
    public GridCell GetCellAtWorld(Vector3 pos) => GetCell(WorldToGrid(pos));

    /// <summary>
    /// Returns true only if all cells in the footprint are completely empty
    /// (no trees, stumps, buildings, or obstacles). Stumps explicitly block building.
    /// </summary>
    public bool IsBuildable(int gridX, int gridY, int w, int h)
    {
        for (int dx = 0; dx < w; dx++)
        for (int dy = 0; dy < h; dy++)
        {
            int cx = gridX + dx, cy = gridY + dy;
            if (!InBounds(cx, cy)) 
            {
                // Debug.Log($"[WorldGrid] IsBuildable: ({cx},{cy}) is Out of Bounds.");
                return false;
            }
            if (_cells[cx, cy].cellType != CellType.Empty)
            {
                // Debug.Log($"[WorldGrid] IsBuildable: ({cx},{cy}) cellType is {_cells[cx, cy].cellType}, not Empty.");
                return false;
            }
            if (_cells[cx, cy].occupant != null)
            {
                // Debug.Log($"[WorldGrid] IsBuildable: ({cx},{cy}) has occupant {_cells[cx, cy].occupant.name}.");
                return false;
            }
        }
        return true;
    }

    public bool IsBuildable(Vector2Int origin, int w, int h)
        => IsBuildable(origin.x, origin.y, w, h);

    /// <summary>
    /// Returns true if all cells in the region are either Empty or Stump.
    /// Used by the zone painter to validate forest zone placement — stumps
    /// can be overwritten by a new zone (Sawyer will clear them first).
    /// </summary>
    public bool IsZonePaintable(int gridX, int gridY, int w, int h)
    {
        for (int dx = 0; dx < w; dx++)
        for (int dy = 0; dy < h; dy++)
        {
            int cx = gridX + dx, cy = gridY + dy;
            if (!InBounds(cx, cy)) return false;
            var cell = _cells[cx, cy];
            // Allow Empty and Stump cells — stumps will be cleared when zone is planted
            if (cell.cellType != CellType.Empty && cell.cellType != CellType.Stump)
                return false;
        }
        return true;
    }

    public bool IsWalkable(Vector3 worldPos)
    {
        var g = WorldToGrid(worldPos);
        return InBounds(g) && _cells[g.x, g.y].isWalkable;
    }

    // ── Stump Management ──────────────────────────────────────────────

    /// <summary>
    /// Marks a single grid cell as a Stump. Stumps block building placement
    /// but are walkable — Sawyer will clear them when idle.
    /// </summary>
    public void SetStump(int gridX, int gridY)
    {
        if (!InBounds(gridX, gridY)) return;
        _cells[gridX, gridY].cellType   = CellType.Stump;
        _cells[gridX, gridY].isWalkable = true;
        _cells[gridX, gridY].occupant   = null;
    }

    public void SetStump(Vector2Int g) => SetStump(g.x, g.y);

    /// <summary>
    /// Clears a stump, returning the cell to Empty. Called by SawyerWorker
    /// after the stump-clearing animation completes.
    /// </summary>
    public void ClearStump(int gridX, int gridY)
    {
        if (!InBounds(gridX, gridY)) return;
        if (_cells[gridX, gridY].cellType != CellType.Stump)
        {
            Debug.LogWarning($"[WorldGrid] ClearStump called on non-stump cell ({gridX},{gridY}).");
            return;
        }
        _cells[gridX, gridY] = new GridCell
        {
            gridX = gridX, gridY = gridY,
            cellType = CellType.Empty, isWalkable = true, occupant = null
        };
    }

    public void ClearStump(Vector2Int g) => ClearStump(g.x, g.y);

    /// <summary>
    /// Returns all stump cells in the grid, optionally limited to a region.
    /// Used by SawyerWorker to find the nearest stump to clear.
    /// </summary>
    public List<Vector2Int> GetAllStumps(int startX = 0, int startY = 0, int width = -1, int height = -1)
    {
        int endX = width  < 0 ? _columns : Mathf.Min(startX + width,  _columns);
        int endY = height < 0 ? _rows    : Mathf.Min(startY + height, _rows);

        var result = new List<Vector2Int>();
        for (int x = startX; x < endX; x++)
        for (int y = startY; y < endY; y++)
            if (InBounds(x, y) && _cells[x, y].cellType == CellType.Stump)
                result.Add(new Vector2Int(x, y));
        return result;
    }

    // ── Occupancy ─────────────────────────────────────────────────────

    public bool Occupy(int gridX, int gridY, int w, int h,
                       CellType type, GameObject occupant, bool walkable = false)
    {
        if (!IsBuildable(gridX, gridY, w, h))
        {
            Debug.LogWarning($"[WorldGrid] Occupy failed at ({gridX},{gridY}) {w}×{h}.");
            return false;
        }
        var cellList = new List<Vector2Int>();
        for (int dx = 0; dx < w; dx++)
        for (int dy = 0; dy < h; dy++)
        {
            int cx = gridX + dx, cy = gridY + dy;
            _cells[cx, cy].cellType   = type;

            bool isWalkableCell = walkable;
            if (type == CellType.Building)
            {
                // Top rows are walkable so characters can walk behind the building
                // Bottom row is obstacle (dy == h - 1 because Y increases DOWNwards)
                if (dy < h - 1)
                {
                    isWalkableCell = true;
                }
                else
                {
                    isWalkableCell = false;
                }
            }

            _cells[cx, cy].isWalkable = isWalkableCell;
            _cells[cx, cy].occupant   = occupant;
            cellList.Add(new Vector2Int(cx, cy));
        }
        if (occupant != null) _occupancyMap[occupant] = cellList;
        return true;
    }

    public bool Occupy(Vector2Int origin, int w, int h,
                       CellType type, GameObject occupant, bool walkable = false)
        => Occupy(origin.x, origin.y, w, h, type, occupant, walkable);

    public void Free(GameObject occupant)
    {
        if (occupant == null || !_occupancyMap.ContainsKey(occupant)) return;
        foreach (var c in _occupancyMap[occupant])
        {
            if (!InBounds(c)) continue;
            _cells[c.x, c.y] = new GridCell
            { gridX = c.x, gridY = c.y, cellType = CellType.Empty, isWalkable = true };
        }
        _occupancyMap.Remove(occupant);
    }

    // ── Search & Pathfinding Utilities ───────────────────────────────

    /// <summary>
    /// Converts a world-space position into a GridCell.
    /// Returns GridCell.Invalid if the position is out of bounds.
    /// </summary>
    public GridCell GetCellAtWorldPosition(Vector3 worldPos)
    {
        Vector2Int coords = WorldToCell(worldPos);
        if (InBounds(coords)) return _cells[coords.x, coords.y];
        return GridCell.Invalid;
    }

    /// <summary>
    /// Converts a world-space position into a Vector2Int representing the grid coordinates (x,y).
    /// Note: Does not check bounds.
    /// </summary>
    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos.x - _worldOrigin.x) / _cellSize);
        int y = Mathf.FloorToInt((_worldOrigin.y - worldPos.y) / _cellSize);
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// Retrieves adjacent cells for pathfinding algorithms.
    /// </summary>
    public List<GridCell> GetNeighbors(GridCell cell, bool allowDiagonals = false)
    {
        List<GridCell> neighbors = new List<GridCell>();

        int[] dx = allowDiagonals ? new int[] { 0, 1, 0, -1, 1, 1, -1, -1 } : new int[] { 0, 1, 0, -1 };
        int[] dy = allowDiagonals ? new int[] { -1, 0, 1, 0, -1, 1, -1, 1 } : new int[] { -1, 0, 1, 0 };

        for (int i = 0; i < dx.Length; i++)
        {
            int checkX = cell.gridX + dx[i];
            int checkY = cell.gridY + dy[i];

            if (InBounds(checkX, checkY))
            {
                // If checking diagonals, ensure we don't cut corners through solid blocks
                if (allowDiagonals && i >= 4)
                {
                    bool adjacent1Walkable = IsCellWalkableForPathfinding(_cells[cell.gridX + dx[i], cell.gridY]);
                    bool adjacent2Walkable = IsCellWalkableForPathfinding(_cells[cell.gridX, cell.gridY + dy[i]]);
                    
                    if (!adjacent1Walkable || !adjacent2Walkable) continue;
                }

                neighbors.Add(_cells[checkX, checkY]);
            }
        }
        return neighbors;
    }

    /// <summary>
    /// Helper to define what constitutes a solid obstacle for Sawyer's pathfinding.
    /// Currently, Water, Boulders, and OldGrowthTrees block movement. Standard Trees and Buildings are ignored.
    /// </summary>
    public bool IsCellWalkableForPathfinding(GridCell cell)
    {
        return cell.isWalkable;
    }

    public List<Vector2Int> GetEmptyCellsInRegion(int startX, int startY, int w, int h)
    {
        var result = new List<Vector2Int>();
        for (int x = startX; x < Mathf.Min(startX + w, _columns); x++)
        for (int y = startY; y < Mathf.Min(startY + h, _rows);    y++)
            if (InBounds(x, y) && _cells[x, y].IsEmpty)
                result.Add(new Vector2Int(x, y));
        return result;
    }

    public Vector2Int FindNearestEmpty(Vector3 worldPos, int maxRadius = 5)
    {
        var origin = WorldToGrid(worldPos);
        for (int r = 0; r <= maxRadius; r++)
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        {
            if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
            int cx = origin.x + dx, cy = origin.y + dy;
            if (InBounds(cx, cy) && _cells[cx, cy].IsEmpty)
                return new Vector2Int(cx, cy);
        }
        return new Vector2Int(-1, -1);
    }

    public void RegenerateObstacles()
    {
        // First wipe the grid clean of any current obstacles
        InitializeGrid();
        
        // Then regenerate them using the newly set seed
        GenerateObstacles();
        Debug.Log($"[WorldGrid] Regenerated obstacles using seed: {obstacleSeed}");
    }

    // ── Gizmos ────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Show strip outline even before Play
        if (config == null) return;

        float cs = config.cellSize;
        int cols = config.columns;
        int rows = config.Rows;
        Vector3 origin = new Vector3(
            -(cols * cs) / 2f,
             config.StripTopY, 0f);

        for (int x = 0; x < cols; x++)
        for (int y = 0; y < rows; y++)
        {
            float wx = origin.x + (x + 0.5f) * cs;
            float wy = origin.y - (y + 0.5f) * cs;
            Vector3 center = new Vector3(wx, wy, 0f);

            if (showObstacleGizmos && _cells != null && InBounds(x, y)
                && _cells[x, y].cellType != CellType.Empty)
            {
                Gizmos.color = GizmoColor(_cells[x, y].cellType);
                Gizmos.DrawCube(center, new Vector3(cs * 0.88f, cs * 0.88f, 1f));
            }

            if (showGridGizmos)
            {
                Gizmos.color = new Color(0.5f, 0.8f, 0.5f, 0.15f);
                Gizmos.DrawWireCube(center, new Vector3(cs, cs, 0f));
            }
        }

        // Draw strip boundary in yellow
        Vector3 stripCenter = new Vector3(0f, config.StripTopY - (config.GridHeight / 2f), 0f);
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(stripCenter,
            new Vector3(cols * cs, config.GridHeight, 0f));
    }

    private static Color GizmoColor(CellType t) => t switch
    {
        CellType.Water         => new Color(0.2f, 0.5f, 0.95f, 0.65f),
        CellType.Boulder       => new Color(0.55f, 0.55f, 0.55f, 0.65f),
        CellType.OldGrowthTree => new Color(0.1f, 0.45f, 0.1f, 0.65f),
        CellType.Tree          => new Color(0.35f, 0.7f, 0.35f, 0.45f),
        CellType.Building      => new Color(0.85f, 0.6f, 0.15f, 0.55f),
        CellType.Stump         => new Color(0.55f, 0.38f, 0.18f, 0.65f),
        _                      => new Color(0.8f, 0.2f, 0.2f, 0.45f),
    };
#endif
}

// ── GridCell ──────────────────────────────────────────────────────────

[System.Serializable]
public struct GridCell
{
    public int        gridX;
    public int        gridY;
    public CellType   cellType;
    public bool       isWalkable;
    public GameObject occupant;
    public FlowDirection flowDirection;

    public bool IsEmpty => cellType == CellType.Empty && occupant == null;

    /// <summary>True if this cell is a tree stump awaiting clearing.</summary>
    public bool IsStump => cellType == CellType.Stump;

    public static readonly GridCell Invalid = new GridCell
    {
        gridX = -1, gridY = -1,
        cellType = CellType.Occupied, isWalkable = false, flowDirection = FlowDirection.None
    };
}

// ── Enums ──────────────────────────────────────────────────────────

[System.Flags]
public enum FlowDirection
{
    None  = 0,
    North = 1 << 0,
    East  = 1 << 1,
    South = 1 << 2,
    West  = 1 << 3
}

public enum CellType
{
    Empty,
    Tree,
    Building,
    Occupied,
    Water,
    Boulder,
    OldGrowthTree,
    /// <summary>
    /// Left behind when a ForestZone depletes. Blocks building placement
    /// but is walkable — Sawyer clears stumps when idle.
    /// </summary>
    Stump,
}
