using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Sawmill.UI
{
    /// <summary>
    /// Global manager for spawning floating text events in the world (e.g. "Chopping...", "-$15", "+5 Rep").
    /// Uses object pooling to avoid Instantiation overhead during gameplay.
    /// </summary>
    public class WorldTextManager : MonoBehaviour
    {
        public static WorldTextManager Instance { get; private set; }

        [Header("Prefabs")]
        [Tooltip("Prefab containing the FloatingText component and a TextMeshPro setup.")]
        [SerializeField] private FloatingText _floatingTextPrefab;

        [Header("Pool Settings")]
        [SerializeField] private int _initialPoolSize = 10;
        
        [Header("Default Settings")]
        [SerializeField] private Color _defaultColor = Color.white;
        [SerializeField] private float _defaultYOffset = 2.0f;
        [Tooltip("Adjust this to easily make the floating text bigger or smaller in the game world.")]
        [SerializeField] private float _defaultScale = 1.0f;

        [Header("Worker Actions: Toggles")]
        public bool showChoppingText = true;
        public bool showSawingText = true;
        public bool showSurfacingText = true;
        public bool showClearingStumpText = true;

        [Header("Worker Actions: Custom Text")]
        public string choppingText = "Chopping...";
        public string sawingText = "Sawing...";
        public string surfacingText = "Surfacing...";
        public string clearingStumpText = "Clearing Stump...";

        [Header("Worker Actions: Custom Colors")]
        public Color choppingColor = new Color(0.9f, 0.5f, 0.3f);
        public Color sawingColor = new Color(0.9f, 0.9f, 0.5f);
        public Color surfacingColor = new Color(0.9f, 0.9f, 0.5f);
        public Color clearingStumpColor = new Color(0.7f, 0.4f, 0.2f);

        [Header("Sorting Layer Fix")]
        [Tooltip("If the text renders behind your characters, set this to the Sorting Layer Name your characters use (e.g., 'Player' or 'Default')")]
        public string fallbackSortingLayerName = "Default";

        private Queue<FloatingText> _pool = new Queue<FloatingText>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializePool();
        }

        private void InitializePool()
        {
            if (_floatingTextPrefab == null)
            {
                // Fallback creation for testing if prefab isn't linked yet
                Debug.LogWarning("WorldTextManager: No prefab assigned. Creating a basic fallback prefab.");
                CreateFallbackPrefab();
            }

            for (int i = 0; i < _initialPoolSize; i++)
            {
                CreateAndPoolInstance();
            }
        }

        private void CreateFallbackPrefab()
        {
            var go = new GameObject("Fallback_FloatingText");
            var tmp = go.AddComponent<TextMeshPro>();
            var rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(10f, 2f);
            
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 4;
            tmp.outlineWidth = 0.2f; // Outline for visibility against trees

            // TextMeshPro requires you to access its underlying Renderer to change the sorting layer
            Renderer tmpRenderer = tmp.GetComponent<Renderer>();
            if (tmpRenderer != null)
            {
                tmpRenderer.sortingLayerName = fallbackSortingLayerName;
            }

            tmp.sortingOrder = 32767;  // Max sorting order to render over Sawyer
            _floatingTextPrefab = go.AddComponent<FloatingText>();
            // Disable so it doesn't float away during setup
            go.SetActive(false); 
        }

        private FloatingText CreateAndPoolInstance()
        {
            var instance = Instantiate(_floatingTextPrefab, transform);
            instance.gameObject.SetActive(false);
            instance.OnLifetimeEnded += ReturnToPool;
            _pool.Enqueue(instance);
            return instance;
        }

        private void ReturnToPool(FloatingText instance)
        {
            if (instance != null)
            {
                instance.gameObject.SetActive(false);
                instance.transform.SetParent(transform);
                _pool.Enqueue(instance);
            }
        }

        private FloatingText GetFromPool()
        {
            if (_pool.Count == 0)
            {
                return CreateAndPoolInstance();
            }

            var instance = _pool.Dequeue();
            instance.gameObject.SetActive(true);
            return instance;
        }

        /// <summary>
        /// Spawns text that stays in place where it spawned (e.g. above a building).
        /// </summary>
        public void SpawnStaticText(Vector3 position, string text, Color? color = null)
        {
            var instance = GetFromPool();
            instance.transform.position = position + (Vector3.up * _defaultYOffset);
            instance.transform.localScale = Vector3.one * _defaultScale;
            instance.Setup(text, color ?? _defaultColor);
        }

        /// <summary>
        /// Spawns text that follows a moving target (e.g. above Sawyer).
        /// </summary>
        public void SpawnFollowingText(Transform target, string text, Color? color = null, Vector3 offset = default)
        {
            if (target == null) return;
            var instance = GetFromPool();
            Vector3 finalOffset = offset == default ? (Vector3.up * _defaultYOffset) : offset;
            
            // initial position setup
            instance.transform.position = target.position + finalOffset;
            instance.transform.localScale = Vector3.one * _defaultScale;
            instance.transform.SetParent(null); // Detach so the manager doesn't pull it around
            instance.Setup(text, color ?? _defaultColor, target, finalOffset);
        }
    }
}
