using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Project-owned content definition for the three-puzzle vertical slice:
    /// exactly three distinct, non-empty 5x5 target patterns with a deliberate
    /// difficulty curve (7, 8 and 9 cells). Content lives here, separate from
    /// the domain projection rules and from any controller branch; the
    /// <see cref="PuzzleProgression"/> coordinator and the deterministic scene
    /// builder both read it.
    /// </summary>
    public static class PuzzleContent
    {
        /// <summary>
        /// Puzzle 1 — the warm-up smiley (7 cells): two eyes, a nose, a smile
        /// and one cheek. The T-005 target, kept as the entry pattern.
        /// </summary>
        public static readonly Vector2Int[] Smile =
        {
            new Vector2Int(1, 3), new Vector2Int(3, 3), // eyes
            new Vector2Int(2, 2),                       // nose
            new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(3, 1), // smile
            new Vector2Int(4, 2),                       // right cheek end
        };

        /// <summary>
        /// Puzzle 2 — the right-pointing feathered arrow (8 cells): a two-cell
        /// fletching, a three-cell shaft and a tip.
        /// </summary>
        public static readonly Vector2Int[] Arrow =
        {
            new Vector2Int(4, 2),                       // tip
            new Vector2Int(3, 2), new Vector2Int(2, 2), new Vector2Int(1, 2), // shaft
            new Vector2Int(1, 1), new Vector2Int(2, 1), // lower fletching
            new Vector2Int(1, 3), new Vector2Int(2, 3), // upper fletching
        };

        /// <summary>
        /// Puzzle 3 — the diagonal cross (9 cells): both full diagonals of the
        /// board, sharing the center. The hardest pattern of the slice.
        /// </summary>
        public static readonly Vector2Int[] Cross =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 2),
            new Vector2Int(3, 3), new Vector2Int(4, 4),
            new Vector2Int(4, 0), new Vector2Int(3, 1),
            new Vector2Int(1, 3), new Vector2Int(0, 4),
        };

        /// <summary>The ordered 1 → 2 → 3 content of the whole slice.</summary>
        public static readonly Vector2Int[][] Puzzles = { Smile, Arrow, Cross };
    }
}
