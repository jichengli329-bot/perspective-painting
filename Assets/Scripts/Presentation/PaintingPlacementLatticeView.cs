using System;
using System.Collections.Generic;
using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>Shows the selected piece's physical composition lattice while it is being considered.</summary>
    public sealed class PaintingPlacementLatticeView : MonoBehaviour
    {
        [SerializeField] private PaintingManipulationController _controller;
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private Color _lineColor = new Color(0.78f, 0.88f, 0.82f, 0.20f);
        [SerializeField, Min(0.001f)] private float _lineWidth = 0.010f;

        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private PaintingManipulablePiece _shownFor;
        private PaintingTutorialSequence _tutorial;
        private PaintingTutorialSequence.AssistanceLevel _shownAssistance;

        private void Awake()
        {
            _tutorial = GetComponent<PaintingTutorialSequence>();
            if (_controller != null && _lineMaterial != null) Configure(_controller, _lineMaterial);
        }

        private void Update()
        {
            // Selection can remain active for guidance and keyboard actions.
            // The floor grid is a manipulation aid, so show it only during
            // the physical pickup transaction and let the lake stay clean at
            // rest.
            PaintingManipulablePiece selected = _controller != null && _controller.IsCarrying
                ? _controller.SelectedPiece
                : null;
            PaintingTutorialSequence.AssistanceLevel assistance = _tutorial != null
                ? _tutorial.Assistance : PaintingTutorialSequence.AssistanceLevel.Normal;
            if (selected == _shownFor && assistance == _shownAssistance) return;
            _shownFor = selected;
            _shownAssistance = assistance;
            Rebuild(selected);
        }

        public void Configure(PaintingManipulationController controller, Material lineMaterial)
        {
            _controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            _lineMaterial = lineMaterial != null ? lineMaterial : throw new ArgumentNullException(nameof(lineMaterial));
            Rebuild(_controller.IsCarrying ? _controller.SelectedPiece : null);
        }

        private void Rebuild(PaintingManipulablePiece piece)
        {
            ClearLines();
            if (piece == null || !_controller.UsesPlacementLattice) return;
            Rect rect = _controller.PlacementRectangle;
            float y = _controller.SurfaceY + 0.025f;
            float stepX = _controller.LatticeColumnSpacing;
            float stepZ = _controller.LatticeDepthSpacing;

            for (float x = piece.AuthoredPosition.x; x >= rect.xMin - 0.001f; x -= stepX)
                AddLine(new Vector3(Mathf.Max(x, rect.xMin), y, rect.yMin), new Vector3(Mathf.Max(x, rect.xMin), y, rect.yMax));
            for (float x = piece.AuthoredPosition.x + stepX; x <= rect.xMax + 0.001f; x += stepX)
                AddLine(new Vector3(Mathf.Min(x, rect.xMax), y, rect.yMin), new Vector3(Mathf.Min(x, rect.xMax), y, rect.yMax));
            for (float z = piece.AuthoredPosition.z; z >= rect.yMin - 0.001f; z -= stepZ)
                AddLine(new Vector3(rect.xMin, y, Mathf.Max(z, rect.yMin)), new Vector3(rect.xMax, y, Mathf.Max(z, rect.yMin)));
            for (float z = piece.AuthoredPosition.z + stepZ; z <= rect.yMax + 0.001f; z += stepZ)
                AddLine(new Vector3(rect.xMin, y, Mathf.Min(z, rect.yMax)), new Vector3(rect.xMax, y, Mathf.Min(z, rect.yMax)));

            if (_tutorial != null && _tutorial.ActivePiece == piece
                && _tutorial.Assistance != PaintingTutorialSequence.AssistanceLevel.Normal)
                AddTargetRing(piece.AuthoredPosition, y);
        }

        private void AddTargetRing(Vector3 center, float y)
        {
            const int segments = 40;
            float radius = Mathf.Max(0.18f, Mathf.Min(_controller.LatticeColumnSpacing,
                _controller.LatticeDepthSpacing) * 0.28f);
            var go = new GameObject("目标磁性环");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _lineMaterial;
            Color gold = new Color(0.95f, 0.69f, 0.25f, 0.72f);
            line.startColor = line.endColor = gold;
            line.startWidth = line.endWidth = _lineWidth * 2.2f;
            line.positionCount = segments + 1;
            line.useWorldSpace = true;
            line.loop = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * radius, y + 0.004f,
                    center.z + Mathf.Sin(angle) * radius));
            }
            _lines.Add(line);
        }

        private void AddLine(Vector3 from, Vector3 to)
        {
            var go = new GameObject("构图刻度");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _lineMaterial;
            line.startColor = line.endColor = _lineColor;
            line.startWidth = line.endWidth = _lineWidth;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            _lines.Add(line);
        }

        private void ClearLines()
        {
            foreach (LineRenderer line in _lines)
                if (line != null) Destroy(line.gameObject);
            _lines.Clear();
        }

        private void OnDestroy() => ClearLines();
    }
}
