using System;
using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>Hold-to-inspect high angle for the physical composition board.</summary>
    public sealed class PaintingInspectionCamera : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private PaintingManipulationController _manipulation;
        [SerializeField] private Vector3 _homePosition;
        [SerializeField] private Quaternion _homeRotation = Quaternion.identity;
        [SerializeField] private Vector3 _inspectionPosition;
        [SerializeField] private Quaternion _inspectionRotation = Quaternion.identity;
        [SerializeField, Min(0.05f)] private float _blendDuration = 0.24f;

        private float _blend;
        public bool IsConfigured => _camera != null && _manipulation != null && _blendDuration > 0f;

        public void Configure(Camera camera, PaintingManipulationController manipulation,
            Vector3 inspectionPosition, Vector3 inspectionTarget, float blendDuration = 0.24f)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _manipulation = manipulation != null ? manipulation : throw new ArgumentNullException(nameof(manipulation));
            if (blendDuration <= 0f) throw new ArgumentOutOfRangeException(nameof(blendDuration));
            _homePosition = camera.transform.position;
            _homeRotation = camera.transform.rotation;
            _inspectionPosition = inspectionPosition;
            _inspectionRotation = Quaternion.LookRotation((inspectionTarget - inspectionPosition).normalized, Vector3.up);
            _blendDuration = blendDuration;
        }

        private void Update()
        {
            if (!IsConfigured || _manipulation.InputLocked)
                return;
            float target = Input.GetKey(KeyCode.Space) ? 1f : 0f;
            _blend = Mathf.MoveTowards(_blend, target, Time.unscaledDeltaTime / _blendDuration);
            float eased = _blend * _blend * (3f - 2f * _blend);
            _camera.transform.position = Vector3.Lerp(_homePosition, _inspectionPosition, eased);
            _camera.transform.rotation = Quaternion.Slerp(_homeRotation, _inspectionRotation, eased);
        }
    }
}
