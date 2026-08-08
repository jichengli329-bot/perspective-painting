using System.Collections;
using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// In-world marker for the active depth layer: a coral plate that rises to
    /// the layer height, a numeric label that always shows the current 1/2/3
    /// layer, and a small scale pop when the layer changes. The scene builder
    /// assigns <see cref="marker"/>, <see cref="label"/> and a layer height
    /// matching the grid mapper; grid Z = 0 sits at the base and grid Z = 2 at
    /// the top. No UI panel: the indicator stays a physical object on the stage.
    /// </summary>
    public sealed class LayerIndicator : MonoBehaviour
    {
        [SerializeField] private Transform marker;
        [SerializeField] private TextMesh label;
        [SerializeField] private float layerHeight = 0.5f;

        private const float PopDuration = 0.14f;
        private Vector3 _baseScale = Vector3.one;
        private bool _baseScaleCaptured;
        private Coroutine _pop;

        /// <summary>The currently indicated 0-based depth layer, clamped to the 3 puzzle layers.</summary>
        public int ActiveLayer { get; private set; }

        private void Start()
        {
            CaptureBaseScale();
        }

        /// <summary>
        /// Moves the marker to the given depth layer, updates the numeric label,
        /// and pops the marker so the change reads at a glance.
        /// </summary>
        public void SetActiveLayer(int gridZ)
        {
            ActiveLayer = Mathf.Clamp(gridZ, 0, 2);
            CaptureBaseScale();

            if (marker != null)
            {
                var position = marker.localPosition;
                marker.localPosition = new Vector3(position.x, ActiveLayer * layerHeight, position.z);
            }

            if (label != null)
                label.text = (ActiveLayer + 1).ToString();

            if (_pop != null)
                StopCoroutine(_pop);
            _pop = StartCoroutine(PopRoutine());
        }

        /// <summary>Returns the indicator to layer 0 and clears any running pop animation.</summary>
        public void ResetIndicator()
        {
            if (_pop != null)
                StopCoroutine(_pop);
            _pop = null;
            if (marker != null && _baseScaleCaptured)
                marker.localScale = _baseScale;
            SetActiveLayer(0);
        }

        private void CaptureBaseScale()
        {
            if (_baseScaleCaptured || marker == null)
                return;
            _baseScale = marker.localScale;
            _baseScaleCaptured = true;
        }

        private IEnumerator PopRoutine()
        {
            if (marker == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < PopDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / PopDuration);
                marker.localScale = _baseScale * (1f + 0.18f * Mathf.Sin(t * Mathf.PI)); // swell and settle
                yield return null;
            }
            marker.localScale = _baseScale;
            _pop = null;
        }
    }
}
