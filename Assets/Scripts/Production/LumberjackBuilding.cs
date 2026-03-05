using UnityEngine;
using UnityEngine.EventSystems;
using Sawmill.Core;

namespace Sawmill.Production
{
    public class LumberjackBuilding : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Worker Settlement")]
        [Tooltip("Offset from the building center where the lumberjack will spawn.")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -20f, 0f);

        [HideInInspector] public GameObject LumberjackPrefab; // Populated from BuildingData
        [HideInInspector] public int EffectRadius = 5; // Populated from BuildingData

        public LumberjackWorker SpawnedWorker { get; private set; }

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
            GameObject workerObj = Instantiate(LumberjackPrefab, spawnPos, Quaternion.identity);
            workerObj.name = "Lumberjack Worker";

            // If we grabbed Sawyer as a fallback, we need to swap scripts
            var sawyerScript = workerObj.GetComponent<SawyerWorker>();
            if (sawyerScript != null)
            {
                Destroy(sawyerScript); // Remove the player logic
                var lumberjackScript = workerObj.AddComponent<LumberjackWorker>();
                lumberjackScript.HomeBuilding = this;
                SpawnedWorker = lumberjackScript;
            }
            else
            {
                var lumberjackScript = workerObj.GetComponent<LumberjackWorker>();
                if (lumberjackScript != null)
                {
                    lumberjackScript.HomeBuilding = this;
                    SpawnedWorker = lumberjackScript;
                }
            }

            Debug.Log($"[LumberjackBuilding] Spawned lumberjack at {spawnPos} with effect radius {EffectRadius}");
        }

        // ── Interaction ───────────────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // To keep this generic, tell UIManager to open the Lumberjack Menu
                UI.UIManager.Instance?.OpenLumberjackMenu(this);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Hover logic could go here if we had a valid CursorManager
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Exit logic could go here
        }
    }
}
