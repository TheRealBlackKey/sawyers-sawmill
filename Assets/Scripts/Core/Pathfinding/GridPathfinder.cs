using UnityEngine;
using System.Collections.Generic;

namespace Sawmill.Core.Pathfinding
{
    /// <summary>
    /// Utility class for A* pathfinding on the WorldGrid.
    /// </summary>
    public static class GridPathfinder
    {
        public static List<Vector3> FindPath(Vector3 startWorldPos, Vector3 targetWorldPos, bool allowDiagonals = false)
        {
            var grid = WorldGrid.Instance;
            if (grid == null) return new List<Vector3> { targetWorldPos }; // Fallback to straight line

            GridCell startCell = grid.GetCellAtWorldPosition(startWorldPos);
            GridCell targetCell = grid.GetCellAtWorldPosition(targetWorldPos);

            // If start or target is out of bounds, fallback
            if (startCell.gridX == -1 || targetCell.gridX == -1)
            {
                return new List<Vector3> { targetWorldPos };
            }

            // If we are already in the target cell, just return the exact target position
            if (startCell.gridX == targetCell.gridX && startCell.gridY == targetCell.gridY)
            {
                return new List<Vector3> { targetWorldPos };
            }

            // If the target cell is explicitly blocked, we just walk as close as we can (straight line fallback for now)
            if (!grid.IsCellWalkableForPathfinding(targetCell))
            {
                return new List<Vector3> { targetWorldPos };
            }

            // --- A* Algorithm ---
            List<GridCell> openSet = new List<GridCell>();
            HashSet<GridCell> closedSet = new HashSet<GridCell>();
            
            // Map to store parents to trace the path back
            Dictionary<GridCell, GridCell> cameFrom = new Dictionary<GridCell, GridCell>();
            
            // Costs
            Dictionary<GridCell, float> gCost = new Dictionary<GridCell, float>();
            Dictionary<GridCell, float> fCost = new Dictionary<GridCell, float>();

            openSet.Add(startCell);
            gCost[startCell] = 0;
            fCost[startCell] = GetHeuristicDistance(startCell, targetCell, allowDiagonals);

            while (openSet.Count > 0)
            {
                // Find node in openSet with lowest fCost
                GridCell current = openSet[0];
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (fCost.ContainsKey(openSet[i]) && fCost.ContainsKey(current))
                    {
                        if (fCost[openSet[i]] < fCost[current] || 
                            (Mathf.Approximately(fCost[openSet[i]], fCost[current]) && 
                             GetHeuristicDistance(openSet[i], targetCell, allowDiagonals) < GetHeuristicDistance(current, targetCell, allowDiagonals)))
                        {
                            current = openSet[i];
                        }
                    }
                }

                openSet.Remove(current);
                closedSet.Add(current);

                // Reached target
                if (current.gridX == targetCell.gridX && current.gridY == targetCell.gridY)
                {
                    return RetracePath(startCell, current, cameFrom, targetWorldPos, grid.CellSize);
                }

                // Check neighbors
                foreach (GridCell neighbor in grid.GetNeighbors(current, allowDiagonals))
                {
                    // Ignore unwalkable or closed cells
                    if (!grid.IsCellWalkableForPathfinding(neighbor) || closedSet.Contains(neighbor))
                    {
                        continue;
                    }

                    // Cost to move into the neighbor
                    float moveCost = (current.gridX != neighbor.gridX && current.gridY != neighbor.gridY) ? 1.4f : 1f;
                    float tentativeGCost = (gCost.ContainsKey(current) ? gCost[current] : 0) + moveCost;

                    bool betterPath = false;

                    // If not in open set, add it and it's definitely a better path (since it's new)
                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                        betterPath = true;
                    }
                    // If it is in the open set, is this new path faster to reach it?
                    else if (tentativeGCost < (gCost.ContainsKey(neighbor) ? gCost[neighbor] : float.MaxValue))
                    {
                        betterPath = true;
                    }

                    // If this is the best path to the neighbor found so far, record it
                    if (betterPath)
                    {
                        cameFrom[neighbor] = current;
                        gCost[neighbor] = tentativeGCost;
                        fCost[neighbor] = tentativeGCost + GetHeuristicDistance(neighbor, targetCell, allowDiagonals);
                    }
                }
            }

            // No path found (blocked off) - Fallback to straight line
            Debug.LogWarning("[Pathfinder] No valid path found. Falling back to straight line.");
            return new List<Vector3> { targetWorldPos };
        }

        private static float GetHeuristicDistance(GridCell a, GridCell b, bool allowDiagonals)
        {
            int dstX = Mathf.Abs(a.gridX - b.gridX);
            int dstY = Mathf.Abs(a.gridY - b.gridY);

            if (allowDiagonals)
            {
                // Chebyshev/Octile distance for diagonals
                if (dstX > dstY)
                    return 1.4f * dstY + 1f * (dstX - dstY);
                return 1.4f * dstX + 1f * (dstY - dstX);
            }
            else
            {
                // Manhattan distance for 4-way movement
                return dstX + dstY;
            }
        }

        private static List<Vector3> RetracePath(GridCell startCell, GridCell endCell, Dictionary<GridCell, GridCell> cameFrom, Vector3 exactTargetPos, float cellSize)
        {
            List<Vector3> path = new List<Vector3>();
            GridCell currentCell = endCell;

            while (currentCell.gridX != startCell.gridX || currentCell.gridY != startCell.gridY)
            {
                // Convert grid center back to world pos
                var grid = WorldGrid.Instance;
                float worldX = grid.MinWorldX + (currentCell.gridX + 0.5f) * cellSize;
                float worldY = grid.TopWorldY - (currentCell.gridY + 0.5f) * cellSize;
                
                path.Add(new Vector3(worldX, worldY, 0f));
                
                if (cameFrom.ContainsKey(currentCell))
                {
                    currentCell = cameFrom[currentCell];
                }
                else
                {
                    break;
                }
            }

            path.Reverse();

            // Override the very last waypoint to be the EXACT target position requested 
            // (so Sawyer aligns perfectly with the tree/log rather than the center of the cell)
            if (path.Count > 0)
            {
                path[path.Count - 1] = exactTargetPos;
            }
            else
            {
                path.Add(exactTargetPos);
            }

            return path;
        }
    }
}
