using UnityEngine;
using System.Collections.Generic;

namespace Sawmill.Core 
{
    /// <summary>
    /// Reads the WorldGrid logic and spawns individual visual tiles for the playfield.
    /// Completely independent of the background backdrop.
    /// </summary>
    public class WorldTileManager : MonoBehaviour
    {
        [Header("Grid Layout & Scale")]
        [SerializeField] private StripConfig config;

        [Header("Decor Tiers (1:1 Cell Mapping)")]
        [Tooltip("Scale 1: Default grass (0.00-0.65 Noise)")]
        [SerializeField] private Sprite[] level1Grass;
        
        [Tooltip("Scale 2: Flowers (0.65-0.75 Noise)")]
        [SerializeField] private Sprite[] level2Flowers;
        
        [Tooltip("Scale 3: Sticks/Leaves (0.75-0.85 Noise)")]
        [SerializeField] private Sprite[] level3Sticks;
        
        [Tooltip("Scale 4: Rubble (0.85-0.95 Noise)")]
        [SerializeField] private Sprite[] level4Rubble;
        
        [Tooltip("Scale 5: Boulders")]
        [SerializeField] private Sprite[] level5Boulders;

        [Header("Decor Density Settings (0.0 - 1.0)")]
        [Tooltip("Percentage of the map covered in basic grass. (e.g. 0.6 = 60%)")]
        [Range(0f, 1f)] [SerializeField] private float grassDensity = 0.60f;
        
        [Tooltip("Percentage of the map covered in flowers.")]
        [Range(0f, 1f)] [SerializeField] private float flowerDensity = 0.15f;
        
        [Tooltip("Percentage of the map covered in sticks & leaves.")]
        [Range(0f, 1f)] [SerializeField] private float stickDensity = 0.10f;
        
        [Tooltip("Percentage of the map covered in rubble.")]
        [Range(0f, 1f)] [SerializeField] private float rubbleDensity = 0.10f;
        
        // Note: Boulders simply take up whatever percentage remains up to 1.0.

        [Header("Spawning - Water")]
        [SerializeField] private Sprite[] waterTiles;

        [Header("Spawning - Epic Trees")]
        [Tooltip("Sprites used for Old Growth/Epic Trees that spawn natively on the map.")]
        [SerializeField] private Sprite[] epicTreeSprites;

        [Header("Organic Clustering (Perlin)")]
        [SerializeField] private float noiseScale = 0.15f;
        [SerializeField] private float noiseSeed = 1000f;

        [Header("Tile Spacing & Scale")]
        [Tooltip("How many logic grid cells a single visual tile covers (e.g. 2 = 1 tile covers 2x2 grid cells)")]
        [SerializeField] private int visualScaleInCells = 2;
        [Tooltip("Scale multiplier to force overlap and hide seams (1.25 = 25% overlap factor)")]
        [SerializeField] private float overlapScale = 1.35f;
        [Tooltip("Random position offset intensity to break the grid")]
        [SerializeField] private float positionJitter = 0.3f;
        [Tooltip("Randomize rotation of the tiles in 90-degree increments?")]
        [SerializeField] private bool randomRotation = true;

        [Header("Sorting")]
        [Tooltip("Should render above the base StripBackground (-20), but behind everything else.")]
        [SerializeField] private int sortingOrder = -10;
        [SerializeField] private string sortingLayerName = "Default";

        private List<GameObject> spawnedTiles = new List<GameObject>();
        private int _oldGrowthPlacementCount = 0;

        private void Start()
        {
            if (config == null || WorldGrid.Instance == null) return;
            
            if (visualScaleInCells < 1) visualScaleInCells = 2;
            
            // In Play Mode, randomize the meadow layout so it's fresh each run
            noiseSeed = UnityEngine.Random.Range(-10000f, 10000f);
            GenerateTiles();
        }

        public void GenerateTiles()
        {
            _oldGrowthPlacementCount = 0;
            
            foreach (var tile in spawnedTiles)
            {
                if (tile != null) Destroy(tile);
            }
            spawnedTiles.Clear();

            int cols = config.columns;
            int rows = config.Rows;
            
            // Calculate absolute target size of the physics cell
            float logicCellSize = config.cellSize;
            float targetVisualSize = logicCellSize * visualScaleInCells;

            // Track placed sprites so we can prevent identical sprites from touching
            int visualCols = Mathf.CeilToInt((float)cols / visualScaleInCells);
            int visualRows = Mathf.CeilToInt((float)rows / visualScaleInCells);
            Sprite[,] placedSprites = new Sprite[visualCols, visualRows];

            for (int x = 0; x < cols; x += visualScaleInCells)
            {
                for (int y = 0; y < rows; y += visualScaleInCells)
                {
                    GridCell cellData = WorldGrid.Instance.GetCell(x, y);
                    
                    // The absolute center of the logical cell we started from
                    Vector3 cellCenter = WorldGrid.Instance.GridToWorld(x, y, 0f);
                    
                    // Adjust to be the exact center of the visual block of cells we are covering
                    float offset = (visualScaleInCells - 1) * config.cellSize * 0.5f;
                    Vector3 finalPos = cellCenter + new Vector3(offset, offset, 0f);

                    // Map Obstacle Handling
                    // --------------------------------------------------------
                    // Check if this cell is purely background/grass, or an actual map obstacle.
                    // If it is Water, paint it blue. If it is Boulder or OldGrowth/EpicTree, skip the procedural visual backdrop
                    // for that 1x1 block, or simply don't overlap the cell.
                    if (cellData.cellType == CellType.Water)
                    {
                        // Default square sprite fallback for rivers (just use the renderer and set it to blue)
                        GameObject tileObj = new GameObject($"RiverTile_{x}_{y}");
                        tileObj.transform.SetParent(this.transform);
                        tileObj.transform.position = finalPos;

                        SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
                        
                        // Create a flat 1x1 white texture/sprite to tint blue
                        Texture2D tex = new Texture2D(1, 1);
                        tex.SetPixel(0, 0, Color.white);
                        tex.Apply();
                        Sprite flatSprite = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
                        
                        sr.sprite = flatSprite;
                        sr.color = new Color(0.2f, 0.5f, 0.95f, 1f); // Blue water tint
                        sr.sortingOrder = sortingOrder + 1; // Draw slightly above grass base
                        sr.sortingLayerName = sortingLayerName;

                        float riverScaleX = logicCellSize * visualScaleInCells;
                        float riverScaleY = logicCellSize * visualScaleInCells;
                        tileObj.transform.localScale = new Vector3(riverScaleX, riverScaleY, 1f);

                        spawnedTiles.Add(tileObj);
                        continue;
                    }
                    else if (cellData.cellType == CellType.Boulder)
                    {
                        // Spawn a random rock tile on Boulder squares
                        if (level5Boulders != null && level5Boulders.Length > 0)
                        {
                            Sprite rockSprite = level5Boulders[UnityEngine.Random.Range(0, level5Boulders.Length)];
                            if (rockSprite != null)
                            {
                                GameObject rockTileObj = new GameObject($"RockTile_{x}_{y}");
                                rockTileObj.transform.SetParent(this.transform);
                                rockTileObj.transform.position = finalPos;

                                SpriteRenderer rockSr = rockTileObj.AddComponent<SpriteRenderer>();
                                rockSr.sprite = rockSprite;
                                rockSr.sortingOrder = sortingOrder + 1; // Slightly above grass base
                                rockSr.sortingLayerName = sortingLayerName;

                                float rockWidthUnits = rockSprite.bounds.size.x;
                                float rockHeightUnits = rockSprite.bounds.size.y;
                                
                                float rockScaleX = rockWidthUnits > 0 ? targetVisualSize / rockWidthUnits : 1f;
                                float rockScaleY = rockHeightUnits > 0 ? targetVisualSize / rockHeightUnits : 1f;
                                
                                rockTileObj.transform.localScale = new Vector3(rockScaleX, rockScaleY, 1f);

                                spawnedTiles.Add(rockTileObj);
                            }
                        }
                        continue;
                    }
                    else if (cellData.cellType == CellType.OldGrowthTree)
                    {
                        // Because WorldGrid generates 2x2 clusters of OldGrowthTree cells, 
                        // we only want to spawn ONE visual tree per 2x2 cluster.
                        // We do this by checking if we are the top-left cell of the block.
                        // In our grid, X increases to the right, Y increases DOWN.
                        // Top-left means the cell to our Left (x-1) is not OldGrowth, and the cell Above (y-1) is not OldGrowth.
                        bool isTopLeftOfBlock = true;
                        
                        GridCell leftCell = WorldGrid.Instance.GetCell(x - 1, y);
                        GridCell topCell = WorldGrid.Instance.GetCell(x, y - 1);
                        
                        if (leftCell.cellType == CellType.OldGrowthTree || topCell.cellType == CellType.OldGrowthTree)
                        {
                            isTopLeftOfBlock = false;
                        }
                        
                        if (isTopLeftOfBlock && epicTreeSprites != null && epicTreeSprites.Length > 0)
                        {
                            int treeIndex = _oldGrowthPlacementCount % epicTreeSprites.Length;
                            Sprite epicSprite = epicTreeSprites[treeIndex];
                            _oldGrowthPlacementCount++;

                            if (epicSprite != null)
                            {
                                GameObject epicTileObj = new GameObject($"EpicTree_{x}_{y}");
                                epicTileObj.transform.SetParent(this.transform);
                                
                                // `finalPos` is currently the center of cell (x,y).
                                // We want to center it in the middle of the 2x2 block, so we shift it right and down by half a cell size.
                                Vector3 centerOfClusterPos = finalPos + new Vector3(config.cellSize * 0.5f, -config.cellSize * 0.5f, 0f);
                                epicTileObj.transform.position = centerOfClusterPos;

                                SpriteRenderer epicSr = epicTileObj.AddComponent<SpriteRenderer>();
                                epicSr.sprite = epicSprite;
                                epicSr.sortingLayerName = sortingLayerName;
                                
                                // Use the same +100 logic as TreeComponent to ensure it renders above the -10 background
                                epicSr.sortingOrder = Mathf.RoundToInt(-centerOfClusterPos.y * 10f) + 100;

                                // Scale it up substantially.
                                float baseScale = 5f; // Match the TreeComponent base scale
                                epicTileObj.transform.localScale = Vector3.one * baseScale * 1.5f;

                                spawnedTiles.Add(epicTileObj);
                            }
                        }
                        
                        // Allow procedural grass to render beneath the Old Growth Tree
                        // so it doesn't leave a brown spot on the map.
                    }

                    // Procedural Grass / Perlin Layers
                    // --------------------------------------------------------
                    Sprite[] palette = GetPaletteForPosition(cellData, finalPos.x, finalPos.y);
                    if (palette == null || palette.Length == 0) continue;

                    int gridX = x / visualScaleInCells;
                    int gridY = y / visualScaleInCells;

                    // Find a sprite that doesn't match its left or bottom neighbors
                    Sprite leftNeighbor = gridX > 0 ? placedSprites[gridX - 1, gridY] : null;
                    Sprite bottomNeighbor = gridY > 0 ? placedSprites[gridX, gridY - 1] : null;
                    
                    Sprite chosenSprite = GetUniqueSprite(palette, leftNeighbor, bottomNeighbor);
                    if (chosenSprite == null) continue;
                    
                    placedSprites[gridX, gridY] = chosenSprite;

                    GameObject grassTileObj = new GameObject($"GrassTile_{x}_{y}");
                    grassTileObj.transform.SetParent(this.transform);
                    grassTileObj.transform.position = finalPos;

                    SpriteRenderer grassSr = grassTileObj.AddComponent<SpriteRenderer>();
                    grassSr.sprite = chosenSprite;
                    
                    grassSr.sortingOrder = sortingOrder;
                    grassSr.sortingLayerName = sortingLayerName;
                    grassSr.drawMode = SpriteDrawMode.Simple;

                    // Force whatever image the user dropped in to perfectly fit the sub-tile
                    float spriteWidthUnits = chosenSprite.bounds.size.x;
                    float spriteHeightUnits = chosenSprite.bounds.size.y;
                    
                    float scaleX = spriteWidthUnits > 0 ? targetVisualSize / spriteWidthUnits : 1f;
                    float scaleY = spriteHeightUnits > 0 ? targetVisualSize / spriteHeightUnits : 1f;

                    // Apply Overlap Scale directly to fix visual seams
                    scaleX *= overlapScale;
                    scaleY *= overlapScale;
                    
                    grassTileObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);

                    // Position Jitter
                    if (positionJitter > 0f)
                    {
                        float jx = UnityEngine.Random.Range(-positionJitter, positionJitter);
                        float jy = UnityEngine.Random.Range(-positionJitter, positionJitter);
                        grassTileObj.transform.position += new Vector3(jx, jy, 0f);
                    }

                    // Rotation
                    if (randomRotation)
                    {
                        int rot = UnityEngine.Random.Range(0, 4) * 90;
                        grassTileObj.transform.rotation = Quaternion.Euler(0, 0, rot);
                    }

                    // Add the object so it can be cleared.
                    spawnedTiles.Add(grassTileObj);
                }
            }
        }

        private Sprite[] GetPaletteForPosition(GridCell cellData, float worldX, float worldY)
        {
            // Calculate organic terrain noise
            float noiseX = (worldX * noiseScale) + noiseSeed;
            float noiseY = (worldY * noiseScale) + noiseSeed;
            float perlinValue = Mathf.PerlinNoise(noiseX, noiseY);

            // Tier mapping based on accumulated user density sliders
            float t1 = grassDensity;
            float t2 = t1 + flowerDensity;
            float t3 = t2 + stickDensity;
            float t4 = t3 + rubbleDensity;

            if (perlinValue <= t1) return level1Grass;
            if (perlinValue <= t2) return level2Flowers != null && level2Flowers.Length > 0 ? level2Flowers : level1Grass;
            if (perlinValue <= t3) return level3Sticks != null && level3Sticks.Length > 0 ? level3Sticks : level1Grass;
            if (perlinValue <= t4) return level4Rubble != null && level4Rubble.Length > 0 ? level4Rubble : level1Grass;
            
            // Whatever remains (e.g. the last 5% if everything else added up to 0.95)
            return level5Boulders != null && level5Boulders.Length > 0 ? level5Boulders : level1Grass;
        }

        private Sprite GetUniqueSprite(Sprite[] palette, Sprite leftNeighbor, Sprite bottomNeighbor)
        {
            if (palette == null || palette.Length == 0) return null;
            if (palette.Length == 1) return palette[0]; // Can't prevent touching if only 1 option

            // Build list of valid candidates
            List<Sprite> candidates = new List<Sprite>();
            foreach (var s in palette)
            {
                if (s != leftNeighbor && s != bottomNeighbor)
                {
                    candidates.Add(s);
                }
            }

            // If we ran out of options (e.g. only 2 sprites and both neighbors used them differently)
            // Just drop the bottom neighbor constraint and try again
            if (candidates.Count == 0)
            {
                foreach (var s in palette)
                {
                    if (s != leftNeighbor)
                    {
                        candidates.Add(s);
                    }
                }
            }
            
            // If still 0 (e.g. they were all somehow the left neighbor), fallback to random
            if (candidates.Count == 0)
            {
                candidates.AddRange(palette);
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }
    }
}
