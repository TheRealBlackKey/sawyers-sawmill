using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks all world-space obstacles that block building placement.
/// Buildings, natural elements (rivers, boulders) all register here.
/// PlacementManager queries this before confirming any placement.
/// </summary>
public class ObstacleRegistry : MonoBehaviour
{
    public static ObstacleRegistry Instance { get; private set; }

    // All registered occupied rectangles in world space
    private List<ObstacleEntry> _obstacles = new List<ObstacleEntry>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Registration ──────────────────────────────────────────────────

    /// <summary>Register a building or natural element as an obstacle.</summary>
    public void Register(ObstacleEntry entry)
    {
        if (!_obstacles.Contains(entry))
            _obstacles.Add(entry);
    }

    /// <summary>Unregister an obstacle (e.g. building demolished).</summary>
    public void Unregister(ObstacleEntry entry)
    {
        _obstacles.Remove(entry);
    }

    /// <summary>Register a world object directly with its bounds.</summary>
    public ObstacleEntry RegisterBounds(Vector2 center, Vector2 size, ObstacleType type, GameObject source = null)
    {
        var entry = new ObstacleEntry
        {
            center = center,
            size = size,
            type = type,
            sourceObject = source
        };
        _obstacles.Add(entry);
        return entry;
    }

    // ── Queries ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the given rect overlaps any registered obstacle.
    /// Optionally ignore a specific type (e.g. ignore decorations).
    /// </summary>
    public bool IsBlocked(Vector2 center, Vector2 size, ObstacleType ignoreType = ObstacleType.None)
    {
        Rect proposed = new Rect(center - size * 0.5f, size);

        foreach (var obs in _obstacles)
        {
            if (ignoreType != ObstacleType.None && obs.type == ignoreType) continue;

            Rect existing = new Rect(obs.center - obs.size * 0.5f, obs.size);
            if (proposed.Overlaps(existing))
                return true;
        }
        return false;
    }

    /// <summary>Returns all obstacles overlapping a given area.</summary>
    public List<ObstacleEntry> GetOverlapping(Vector2 center, Vector2 size)
    {
        var results = new List<ObstacleEntry>();
        Rect proposed = new Rect(center - size * 0.5f, size);

        foreach (var obs in _obstacles)
        {
            Rect existing = new Rect(obs.center - obs.size * 0.5f, obs.size);
            if (proposed.Overlaps(existing))
                results.Add(obs);
        }
        return results;
    }

    /// <summary>Find the nearest unblocked X position for a given footprint size.</summary>
    public float FindNearestClearX(float desiredX, float y, Vector2 footprint, float searchRange = 500f)
    {
        // Try desired position first
        if (!IsBlocked(new Vector2(desiredX, y), footprint))
            return desiredX;

        // Search outward in both directions
        float step = 10f;
        for (float offset = step; offset <= searchRange; offset += step)
        {
            if (!IsBlocked(new Vector2(desiredX + offset, y), footprint))
                return desiredX + offset;
            if (!IsBlocked(new Vector2(desiredX - offset, y), footprint))
                return desiredX - offset;
        }

        return desiredX; // fallback
    }

    public List<ObstacleEntry> AllObstacles => new List<ObstacleEntry>(_obstacles);
}

// ── Data Types ────────────────────────────────────────────────────────

public class ObstacleEntry
{
    public Vector2 center;
    public Vector2 size;
    public ObstacleType type;
    public GameObject sourceObject;
    public string label = "";
}

public enum ObstacleType
{
    None,
    Building,
    NaturalRock,
    NaturalWater,
    NaturalTree,    // Large immovable trees (not player trees)
    Road,
    Decoration
}
