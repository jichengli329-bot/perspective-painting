using System;
using System.Collections.Generic;
using UnityEngine;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Ordered runtime progression coordinator for the three-puzzle slice.
    /// Owns the current index into the project-owned <see cref="PuzzleContent"/>
    /// and advances puzzle 1 → 2 → 3; the final puzzle is the end of the line
    /// and <see cref="TryAdvance"/> never wraps. The constructor validates the
    /// content: exactly <see cref="PuzzleCount"/> targets, every target
    /// non-empty, every cell inside the 5x5 board, and every pair of targets
    /// distinct, so a broken content edit fails fast at scene start instead of
    /// surfacing as a controller bug.
    /// </summary>
    public sealed class PuzzleProgression
    {
        /// <summary>How many puzzles the slice ships.</summary>
        public const int PuzzleCount = 3;

        private readonly ProjectionMap2D[] _targets;

        /// <summary>0-based index of the puzzle currently being played.</summary>
        public int CurrentIndex { get; private set; }

        /// <summary>The target of the puzzle currently being played.</summary>
        public ProjectionMap2D Current => _targets[CurrentIndex];

        /// <summary>True while a later puzzle exists (Space can advance).</summary>
        public bool HasNext => CurrentIndex < _targets.Length - 1;

        /// <summary>True once the final puzzle is being played (final hold).</summary>
        public bool IsOnFinalPuzzle => !HasNext;

        /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The content is not exactly <see cref="PuzzleCount"/> distinct, non-empty
        /// 5x5 targets, or a cell lies outside the board.
        /// </exception>
        public PuzzleProgression(IReadOnlyList<Vector2Int[]> content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (content.Count != PuzzleCount)
                throw new ArgumentException(
                    "The slice must define exactly " + PuzzleCount + " puzzles but defined " + content.Count + ".",
                    nameof(content));

            _targets = new ProjectionMap2D[content.Count];
            for (int i = 0; i < content.Count; i++)
                _targets[i] = ToTarget(content[i], i);

            for (int i = 0; i < _targets.Length; i++)
                for (int j = i + 1; j < _targets.Length; j++)
                    if (Bitmask(_targets[i]) == Bitmask(_targets[j]))
                        throw new ArgumentException(
                            "Puzzle " + (i + 1) + " and puzzle " + (j + 1) + " are identical targets.", nameof(content));
        }

        /// <summary>
        /// Advances to the next puzzle. Returns false on the final puzzle — the
        /// coordinator stays put and never wraps back to puzzle one.
        /// </summary>
        public bool TryAdvance(out ProjectionMap2D next)
        {
            if (!HasNext)
            {
                next = null;
                return false;
            }

            CurrentIndex++;
            next = _targets[CurrentIndex];
            return true;
        }

        private static ProjectionMap2D ToTarget(Vector2Int[] cells, int puzzleIndex)
        {
            if (cells == null)
                throw new ArgumentException("Puzzle " + (puzzleIndex + 1) + " defines no cells.");

            var map = new bool[PuzzleSession.GridWidth * PuzzleSession.GridHeight];
            foreach (var cell in cells)
            {
                if (cell.x < 0 || cell.x >= PuzzleSession.GridWidth
                    || cell.y < 0 || cell.y >= PuzzleSession.GridHeight)
                    throw new ArgumentException(
                        "Puzzle " + (puzzleIndex + 1) + " cell (" + cell.x + ", " + cell.y
                        + ") lies outside the " + PuzzleSession.GridWidth + "x" + PuzzleSession.GridHeight + " board.");
                map[cell.y * PuzzleSession.GridWidth + cell.x] = true;
            }

            var target = ProjectionMap2D.FromCells(PuzzleSession.GridWidth, PuzzleSession.GridHeight, map);
            if (target.OccupiedCount == 0)
                throw new ArgumentException("Puzzle " + (puzzleIndex + 1) + " target is empty.");
            return target;
        }

        private static ulong Bitmask(ProjectionMap2D target)
        {
            ulong mask = 0;
            for (int y = 0; y < target.Height; y++)
                for (int x = 0; x < target.Width; x++)
                    if (target.IsOccupied(x, y))
                        mask |= 1UL << (y * target.Width + x);
            return mask;
        }
    }
}
