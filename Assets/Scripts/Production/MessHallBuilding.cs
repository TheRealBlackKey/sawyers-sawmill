using UnityEngine;
using System.Collections;
using Sawmill.Core;

namespace Sawmill.Production
{
    /// <summary>
    /// The Mess Hall building acts as a refueling station for workers.
    /// It consumes Sawdust from the GameManager to restore worker energy.
    /// </summary>
    public class MessHallBuilding : MonoBehaviour
    {
        [Header("Fuel Configurations")]
        [Tooltip("How much Sawdust is consumed per refueling tick.")]
        [SerializeField] private float sawdustConsumePerTick = 1f;
        
        [Tooltip("How much Energy is restored per tick.")]
        [SerializeField] private float energyRestoredPerTick = 15f;
        
        [Tooltip("Time in seconds between each refueling tick.")]
        [SerializeField] private float refuelTickRate = 1f;

        [Header("Juice")]
        [SerializeField] private ParticleSystem cookingSmokeParticles;

        /// <summary>
        /// Registers the building. No complex setup needed for now as workers scan for it by type.
        /// </summary>
        public void Initialize(Vector3 spawnCenter)
        {
            Debug.Log($"[MessHall] Mess Hall constructed at {spawnCenter}.");
        }

        private void Start()
        {
            // Activate passive particle effects if present
            if (cookingSmokeParticles != null && !cookingSmokeParticles.isPlaying)
            {
                cookingSmokeParticles.Play();
            }
        }

        // ── Interaction ───────────────────────────────────────────────────

        /// <summary>
        /// Coroutine designed to be passed to a WorkerTask's asyncCompletionAction.
        /// The worker will yield this coroutine, staying in the "Working" state 
        /// while they consume sawdust and regain energy.
        /// </summary>
        public IEnumerator HandleRefuel(WorkerBase worker)
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning("[MessHall] No GameManager found to consume Sawdust from.");
                yield break;
            }

            // Hide the worker while eating inside
            worker.SetVisible(false);

            // Loop until the worker is full or we run out of sawdust
            while (worker.CurrentEnergy < worker.maxEnergy)
            {
                if (gm.Sawdust >= sawdustConsumePerTick)
                {
                    // Burn sawdust, restore energy
                    gm.RemoveSawdust(sawdustConsumePerTick);
                    worker.Refuel(energyRestoredPerTick);
                    
                    // TODO: Could spawn a "munching" particle or sound effect here
                }
                else
                {
                    // Out of fuel! Worker is forced to stop refueling early.
                    HUDManager.Instance?.ShowNotification(
                        "Out of Fuel!",
                        "Mess Hall is out of Sawdust! Workers cannot refuel.",
                        new Color(0.8f, 0.2f, 0.2f));
                    break;
                }

                yield return new WaitForSeconds(refuelTickRate);
            }

            // Show the worker again before they leave
            worker.SetVisible(true);
        }

        private void Update()
        {
            // Manual click detection if we want to open a UI panel for the Mess Hall later
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (Camera.main == null) return;
                
                if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                Vector2 screenPosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                Vector3 worldPosition = Camera.main.ScreenToWorldPoint(screenPosition);
                RaycastHit2D hit = Physics2D.Raycast(worldPosition, Vector2.zero);

                if (hit.collider != null && hit.collider.gameObject == this.gameObject)
                {
                    // Placeholder: Could open a Mess Hall stats UI here
                    Debug.Log("[MessHall] Clicked the Mess Hall.");
                }
            }
        }
    }
}
