using UnityEngine;
using Sawmill.Core;

namespace Sawmill.Production
{
    public class LumberjackBuilding : MonoBehaviour
    {
        [Header("Worker Settlement")]
        [Tooltip("Offset from the building center where the lumberjack will spawn.")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -20f, 0f);

        [HideInInspector] public GameObject LumberjackPrefab; // Populated from BuildingData
        [HideInInspector] public int EffectRadius = 5; // Populated from BuildingData

        private GameObject _spawnedWorker;

        /// <summary>
        /// Called by PlacementManager right after the component is attached.
        /// We wait one frame to ensure PlacementManager has finished building the PlacedBuilding record.
        /// </summary>
        public void Initialize(Vector3 worldPos)
        {
            StartCoroutine(InitRoutine(worldPos));
        }

        private System.Collections.IEnumerator InitRoutine(Vector3 worldPos)
        {
            yield return new WaitForEndOfFrame();

            // We need to look up our own BuildingData to know our effect radius.
            // A simple way is to find our PlacedBuilding record.
            if (PlacementManager.Instance != null)
            {
                foreach (var placed in PlacementManager.Instance.PlacedBuildings)
                {
                    if (placed.worldObject == this.gameObject)
                    {
                        EffectRadius = placed.data.effectRadius;
                        LumberjackPrefab = placed.data.helperPrefab;
                        break;
                    }
                }
            }

            SpawnWorker(worldPos);
        }

        private void SpawnWorker(Vector3 basePos)
        {
            if (LumberjackPrefab == null)
            {
                Debug.LogError("[LumberjackBuilding] No helper prefab assigned in BuildingData!");
                return;
            }

            Vector3 spawnPos = basePos + spawnOffset;
            _spawnedWorker = Instantiate(LumberjackPrefab, spawnPos, Quaternion.identity);
            _spawnedWorker.name = "Lumberjack Worker";

            // If we grabbed Sawyer as a fallback, we need to swap scripts
            var sawyerScript = _spawnedWorker.GetComponent<SawyerWorker>();
            if (sawyerScript != null)
            {
                Destroy(sawyerScript); // Remove the player logic
                var lumberjackScript = _spawnedWorker.AddComponent<LumberjackWorker>();
                lumberjackScript.HomeBuilding = this;
                
                // Temporary tint just so we know it's the lumberjack if sharing sprites
                var sr = _spawnedWorker.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(0.7f, 1f, 0.7f); // Light green
            }
            else
            {
                var lumberjackScript = _spawnedWorker.GetComponent<LumberjackWorker>();
                if (lumberjackScript != null)
                {
                    lumberjackScript.HomeBuilding = this;
                }
            }

            Debug.Log($"[LumberjackBuilding] Spawned lumberjack at {spawnPos} with effect radius {EffectRadius}");
        }
    }
}
