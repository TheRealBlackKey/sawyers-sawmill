using UnityEngine;
using Sawmill.Core;

namespace Sawmill.Production
{
    public class MillworkerBuilding : MonoBehaviour
    {
        [Header("Millworker Settings")]
        public GameObject millworkerPrefab;
        public float spawnDelay = 1f;

        [HideInInspector] public BuildingData Data; // Populated during init
        [HideInInspector] public int EffectRadius = 5; // Populated from BuildingData

        private GameObject _spawnedWorkerObj;
        public MillworkerWorker SpawnedWorker { get; private set; }

        public void Initialize(Vector3 worldPos)
        {
            StartCoroutine(InitRoutine(worldPos));
        }

        private System.Collections.IEnumerator InitRoutine(Vector3 worldPos)
        {
            yield return new WaitForEndOfFrame(); // Wait for PlacementManager to finish listing

            if (PlacementManager.Instance != null)
            {
                foreach (var placed in PlacementManager.Instance.PlacedBuildings)
                {
                    if (placed.worldObject == this.gameObject)
                    {
                        Data = placed.data;
                        EffectRadius = placed.data.effectRadius;
                        millworkerPrefab = placed.data.helperPrefab; // Get the prefab from the Data asset
                        break;
                    }
                }
            }

            Invoke(nameof(SpawnWorker), spawnDelay);
        }

        private void SpawnWorker()
        {
            if (SpawnedWorker != null) return; // Already spawned (e.g. Relocating)
            if (millworkerPrefab == null)
            {
                Debug.LogError("[MillworkerBuilding] No helper prefab assigned in BuildingData!");
                return;
            }

            // Spawn at the bottom center of the building footprint
            Vector3 spawnPos = transform.position;
            if (WorldGrid.Instance != null && Data != null)
            {
                Vector2Int gridPos = WorldGrid.Instance.WorldToGrid(transform.position);
                int centerX = gridPos.x + (Data.gridWidth / 2);
                spawnPos = WorldGrid.Instance.GridToWorld(centerX, gridPos.y, transform.position.z);
            }

            _spawnedWorkerObj = Instantiate(millworkerPrefab, spawnPos, Quaternion.identity);
            _spawnedWorkerObj.name = "Millworker Worker";
            SpawnedWorker = _spawnedWorkerObj.GetComponent<MillworkerWorker>();

            if (SpawnedWorker != null)
            {
                SpawnedWorker.HomeBuilding = this;
            }
        }

        private void OnDestroy()
        {
            if (_spawnedWorkerObj != null)
            {
                Destroy(_spawnedWorkerObj);
            }
        }
    }
}
