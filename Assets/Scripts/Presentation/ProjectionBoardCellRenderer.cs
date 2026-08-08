using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Repaints the physical projection board's 5x5 cell tiles from the per-cell
    /// display states computed by <see cref="ProjectionBoardView"/>. The four
    /// states use the accepted T-004 palette so they stay distinguishable at a
    /// glance: Missing is the warm accent, Matched the muted teal, Extra the
    /// blue-gray, Empty the near-white.
    /// </summary>
    public sealed class ProjectionBoardCellRenderer : MonoBehaviour
    {
        private static readonly Color EmptyColor = new Color(0.969f, 0.957f, 0.933f);    // accepted BoardWhite
        private static readonly Color MissingColor = new Color(0.898f, 0.541f, 0.396f);  // accepted SoftCoral
        private static readonly Color ExtraColor = new Color(0.243f, 0.290f, 0.322f);    // accepted FrameBlueGray
        private static readonly Color MatchedColor = new Color(0.298f, 0.604f, 0.573f);  // accepted MutedTeal

        [SerializeField] private ProjectionBoardView board;
        [SerializeField] private MeshRenderer[] cells; // row-major: index = y * width + x

        private void Awake()
        {
            if (board != null)
                board.StatesChanged += Repaint;
        }

        private void OnDestroy()
        {
            if (board != null)
                board.StatesChanged -= Repaint;
        }

        private void Repaint()
        {
            if (board == null || cells == null)
                return;

            for (int index = 0; index < cells.Length; index++)
            {
                var renderer = cells[index];
                if (renderer == null)
                    continue;

                int x = index % board.Width;
                int y = index / board.Width;
                Color color = ColorFor(board.StateAt(x, y));
                renderer.material.color = color;
                renderer.material.SetColor("_BaseColor", color);
            }
        }

        private static Color ColorFor(ProjectionCellState state)
        {
            switch (state)
            {
                case ProjectionCellState.Missing:
                    return MissingColor;
                case ProjectionCellState.Extra:
                    return ExtraColor;
                case ProjectionCellState.Matched:
                    return MatchedColor;
                default:
                    return EmptyColor;
            }
        }
    }
}
