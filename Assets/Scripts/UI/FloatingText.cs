using UnityEngine;
using TMPro;

namespace Sawmill.UI
{
    /// <summary>
    /// Handles the behavior of a single floating text object.
    /// Manages upward movement, alpha fading, and returning to the object pool.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private TextMeshPro _textMesh;

        [Header("Settings")]
        [SerializeField] private float _moveSpeed = 1.5f;
        [SerializeField] private float _lifetime = 2.0f;
        [SerializeField] private float _fadeStartPercentage = 0.5f;
        
        // Optional follow target (like Sawyer)
        private Transform _followTarget;
        private Vector3 _followOffset;

        private float _timeAlive;
        private bool _isActive;
        private Color _originalColor;

        // Delegate to notify the manager when this object is ready to be recycled
        public System.Action<FloatingText> OnLifetimeEnded;

        private void Awake()
        {
            if (_textMesh == null)
                _textMesh = GetComponentInChildren<TextMeshPro>(true);
                
            if (_textMesh != null)
                _originalColor = _textMesh.color;
            else
                Debug.LogWarning("[FloatingText] No TextMeshPro component found on this prefab or its children!");
        }

        public void Setup(string text, Color color, Transform followTarget = null, Vector3 followOffset = default)
        {
            if (_textMesh == null)
                _textMesh = GetComponentInChildren<TextMeshPro>(true);

            if (_textMesh != null)
            {
                _textMesh.text = text;
                _textMesh.color = color;
                _originalColor = color;
            }

            _followTarget = followTarget;
            _followOffset = followOffset;
            _timeAlive = 0f;
            _isActive = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_isActive) return;

            _timeAlive += Time.deltaTime;

            // Handle Movement
            if (_followTarget != null)
            {
                // Follow the target and drift upwards from the offset over time
                transform.position = _followTarget.position + _followOffset + (Vector3.up * _moveSpeed * _timeAlive);
            }
            else
            {
                // Standard upward drift
                transform.position += Vector3.up * _moveSpeed * Time.deltaTime;
            }

            // Handle Fading
            float lifePercent = _timeAlive / _lifetime;
            if (lifePercent > _fadeStartPercentage)
            {
                float fadePercent = (lifePercent - _fadeStartPercentage) / (1f - _fadeStartPercentage);
                Color newColor = _originalColor;
                newColor.a = Mathf.Lerp(_originalColor.a, 0f, fadePercent);
                if (_textMesh != null) _textMesh.color = newColor;
            }

            // Handle Death
            if (_timeAlive >= _lifetime)
            {
                _isActive = false;
                gameObject.SetActive(false);
                OnLifetimeEnded?.Invoke(this);
            }
        }
    }
}
