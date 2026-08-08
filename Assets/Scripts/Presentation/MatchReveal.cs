using System.Collections;
using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Simple non-text completion reveal: when the input controller raises its
    /// reveal signal (the session has locked input on an exact match), this
    /// eases the fixed orthographic camera into a closer, board-facing shot so
    /// the matched projection fills the frame. No text, no UI, no packages.
    /// </summary>
    public sealed class MatchReveal : MonoBehaviour
    {
        [SerializeField] private PuzzleInputController controller;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform boardTarget;
        [SerializeField] private Vector3 revealPosition = new Vector3(-6.7f, 2.0f, 0.6f);
        [SerializeField] private float revealOrthographicSize = 2.4f;
        [SerializeField] private float duration = 1.6f;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private float _startOrthographicSize;

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private float _initialOrthographicSize;
        private Coroutine _revealCoroutine;

        private void Start()
        {
            if (controller != null)
                controller.Revealed += OnRevealed;

            // Remember the resting camera pose so the R reset can restore it
            // without reloading the scene.
            if (targetCamera != null)
            {
                _initialPosition = targetCamera.transform.position;
                _initialRotation = targetCamera.transform.rotation;
                _initialOrthographicSize = targetCamera.orthographicSize;
            }
        }

        private void OnDestroy()
        {
            if (controller != null)
                controller.Revealed -= OnRevealed;
        }

        private void OnRevealed()
        {
            if (targetCamera == null || boardTarget == null)
                return;

            _startPosition = targetCamera.transform.position;
            _startRotation = targetCamera.transform.rotation;
            _startOrthographicSize = targetCamera.orthographicSize;
            _revealCoroutine = StartCoroutine(RevealCoroutine());
        }

        /// <summary>
        /// Restores the camera to its resting pose and stops any in-flight
        /// reveal animation. The input controller re-arms its own reveal
        /// signal, so a later exact match starts a fresh reveal.
        /// </summary>
        public void ResetReveal()
        {
            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
                _revealCoroutine = null;
            }
            if (targetCamera == null)
                return;

            targetCamera.transform.position = _initialPosition;
            targetCamera.transform.rotation = _initialRotation;
            targetCamera.orthographicSize = _initialOrthographicSize;
        }

        private IEnumerator RevealCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                targetCamera.transform.position = Vector3.Lerp(_startPosition, revealPosition, t);
                targetCamera.transform.rotation = Quaternion.Slerp(_startRotation, LookAtBoard(), t);
                targetCamera.orthographicSize = Mathf.Lerp(_startOrthographicSize, revealOrthographicSize, t);
                yield return null;
            }

            targetCamera.transform.position = revealPosition;
            targetCamera.transform.rotation = LookAtBoard();
            targetCamera.orthographicSize = revealOrthographicSize;
            _revealCoroutine = null;
        }

        private Quaternion LookAtBoard()
        {
            var forward = (boardTarget.position - targetCamera.transform.position).normalized;
            return Quaternion.LookRotation(forward);
        }
    }
}
