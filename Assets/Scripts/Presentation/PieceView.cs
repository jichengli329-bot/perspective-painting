using System;
using System.Collections;
using UnityEngine;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// A placed piece's visual in the playable scene. The scene builder
    /// instantiates one per placed cell under the piece root using the rounded
    /// teal mesh and material; <see cref="PuzzleInputController"/> manages their
    /// lifecycle from the domain occupancy grid. Placement and removal are
    /// animated with direct coroutines while the domain grid stays the source
    /// of truth: <see cref="PlaceAt"/> re-enters the spawn animation at any
    /// time (reinstating a view that is still animating out), and
    /// <see cref="BeginRemove"/> animates the view away before destroying it,
    /// so rapid valid input never creates duplicate or orphaned views.
    /// </summary>
    public sealed class PieceView : MonoBehaviour
    {
        /// <summary>Raises after the removal animation completes, right before the view is destroyed.</summary>
        public event Action RemovalFinished;

        /// <summary>The grid cell this piece occupies.</summary>
        public GridCoordinate Cell { get; private set; }

        /// <summary>True while the removal animation is running and the view is scheduled for destruction.</summary>
        public bool IsRemoving { get; private set; }

        private const float SpawnDuration = 0.18f;
        private const float RemoveDuration = 0.16f;
        private Coroutine _animation;

        /// <summary>
        /// Positions this piece at the cell center and plays the spawn pop.
        /// Re-invoking it on a view that is still removing cancels the removal
        /// and re-enters the spawn animation, so rapid place/remove/place input
        /// on one cell always keeps exactly one live view for that cell.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="mapper"/> is null.</exception>
        public void PlaceAt(GridCoordinate cell, GridCoordinateMapper mapper)
        {
            if (mapper == null)
                throw new ArgumentNullException(nameof(mapper));

            Cell = cell;
            IsRemoving = false;
            transform.position = mapper.WorldFromCell(cell);
            if (_animation != null)
                StopCoroutine(_animation);
            _animation = StartCoroutine(SpawnRoutine());
        }

        /// <summary>
        /// Plays the removal animation and destroys this view when it finishes.
        /// A repeated call while already removing does nothing.
        /// </summary>
        public void BeginRemove()
        {
            if (IsRemoving)
                return;
            IsRemoving = true;
            if (_animation != null)
                StopCoroutine(_animation);
            _animation = StartCoroutine(RemoveRoutine());
        }

        /// <summary>
        /// Stops any running animation and destroys this view immediately.
        /// <see cref="RemovalFinished"/> is not raised; callers that tear down
        /// whole sets of views (the R reset) clear their own bookkeeping.
        /// </summary>
        public void TeardownImmediate()
        {
            if (_animation != null)
                StopCoroutine(_animation);
            _animation = null;
            if (this != null)
                Destroy(gameObject);
        }

        private IEnumerator SpawnRoutine()
        {
            float elapsed = 0f;
            while (elapsed < SpawnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / SpawnDuration);
                float rise = Mathf.SmoothStep(0f, 1f, t);
                float bounce = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * 0.08f; // one gentle pop, decaying
                transform.localScale = Vector3.one * Mathf.Max(0f, rise + bounce);
                yield return null;
            }
            transform.localScale = Vector3.one;
            _animation = null;
        }

        private IEnumerator RemoveRoutine()
        {
            float elapsed = 0f;
            while (elapsed < RemoveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / RemoveDuration);
                transform.localScale = Vector3.one * (1f - Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            transform.localScale = Vector3.zero;
            _animation = null;
            RemovalFinished?.Invoke();
            Destroy(gameObject);
        }
    }
}
