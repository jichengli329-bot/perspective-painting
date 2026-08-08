using System;
using UnityEngine;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Builds the single 5x5x3 <see cref="PuzzleSession"/> and its
    /// <see cref="GridCoordinateMapper"/> at runtime and binds them to the
    /// <see cref="PuzzleInputController"/>. Both are plain C# objects, so they
    /// cannot be serialized into the scene; the deterministic scene builder only
    /// writes the target cells and grid layout into this component.
    /// </summary>
    public sealed class PuzzleSessionSource : MonoBehaviour
    {
        [SerializeField] private PuzzleInputController controller;
        [SerializeField] private Vector2Int[] targetCells = Array.Empty<Vector2Int>();
        [SerializeField] private Vector3 origin = new Vector3(-1.32f, 1.43f, 1.32f);
        [SerializeField] private float spacingX = 0.66f;
        [SerializeField] private float spacingY = 0.66f;
        [SerializeField] private float layerHeight = 0.62f;

        /// <summary>The session created by Awake, or null before it runs.</summary>
        public PuzzleSession Session { get; private set; }

        /// <summary>The mapper created by Awake, or null before it runs.</summary>
        public GridCoordinateMapper Mapper { get; private set; }

        private void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// Builds the single <see cref="PuzzleSession"/> and mapper from the
        /// serialized puzzle-one layout and binds them to the controller.
        /// Called by Awake; the scene builder writes the first
        /// <see cref="PuzzleContent"/> pattern into <see cref="targetCells"/>,
        /// so the serialized scene and the runtime progression agree.
        /// </summary>
        public void Initialize()
        {
            bool[] cells = new bool[PuzzleSession.GridWidth * PuzzleSession.GridHeight];
            foreach (var cell in targetCells)
            {
                if (cell.x < 0 || cell.x >= PuzzleSession.GridWidth
                    || cell.y < 0 || cell.y >= PuzzleSession.GridHeight)
                    continue;
                cells[cell.y * PuzzleSession.GridWidth + cell.x] = true;
            }

            var target = ProjectionMap2D.FromCells(
                PuzzleSession.GridWidth, PuzzleSession.GridHeight, cells);
            RebuildWith(target);
        }

        /// <summary>
        /// Rebuilds the session, the mapper and the controller binding around
        /// <paramref name="target"/> — the R reset path keeps the current
        /// puzzle's target, the progression transition passes the next one. A
        /// fresh session means a fresh grid, target and history with no
        /// application or scene reload.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException">The target is not 5x5.</exception>
        public void RebuildWith(ProjectionMap2D target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (target.Width != PuzzleSession.GridWidth || target.Height != PuzzleSession.GridHeight)
                throw new ArgumentException(
                    "The target projection must be " + PuzzleSession.GridWidth + "x" + PuzzleSession.GridHeight
                    + " but was " + target.Width + "x" + target.Height + ".", nameof(target));

            var grid = new OccupancyGrid3D(
                PuzzleSession.GridWidth, PuzzleSession.GridHeight, PuzzleSession.GridDepth);
            Mapper = new GridCoordinateMapper(
                PuzzleSession.GridWidth, PuzzleSession.GridHeight, PuzzleSession.GridDepth,
                origin, spacingX, spacingY, layerHeight);
            Session = new PuzzleSession(grid, target);

            if (controller != null)
                controller.Bind(Mapper, Session);
        }
    }
}
