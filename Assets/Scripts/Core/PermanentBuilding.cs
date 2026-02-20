using UnityEngine;

/// <summary>
/// Attach to any building that is permanently placed in the world from the start
/// (i.e. Sawyer's Sawmill). Registers itself with ObstacleRegistry so the
/// PlacementManager will never allow building on top of it.
/// </summary>
public class PermanentBuilding : MonoBehaviour
{
    [Header("Footprint")]
    [SerializeField] private Vector2 footprintSize = new Vector2(80f, 50f);

    [Header("Obstacle Type")]
    [SerializeField] private ObstacleType obstacleType = ObstacleType.Building;

    private ObstacleEntry _registeredEntry;

    private void Start()
    {
        RegisterWithObstacleRegistry();
    }

    private void RegisterWithObstacleRegistry()
    {
        if (ObstacleRegistry.Instance == null)
        {
            Debug.LogWarning($"[PermanentBuilding] ObstacleRegistry not found — {gameObject.name} won't block placement.");
            return;
        }

        Vector2 center = new Vector2(transform.position.x, transform.position.y);
        _registeredEntry = ObstacleRegistry.Instance.RegisterBounds(
            center,
            footprintSize,
            obstacleType,
            gameObject
        );

        Debug.Log($"[PermanentBuilding] {gameObject.name} registered as obstacle at {center} with footprint {footprintSize}");
    }

    private void OnDestroy()
    {
        if (ObstacleRegistry.Instance != null && _registeredEntry != null)
            ObstacleRegistry.Instance.Unregister(_registeredEntry);
    }

    // Draw footprint in Scene view for easy sizing
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
        Gizmos.DrawCube(transform.position, new Vector3(footprintSize.x, footprintSize.y, 1f));
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 1f);
        Gizmos.DrawWireCube(transform.position, new Vector3(footprintSize.x, footprintSize.y, 1f));
    }
}
