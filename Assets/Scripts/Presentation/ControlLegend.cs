using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// The restrained in-world control legend of the playable scene: one TextMesh
    /// lying on the floor beside the stage that lists the interactions
    /// (left-click place, right-click remove, 1/2/3 layer, Z undo, R reset)
    /// without covering the puzzle or the projection board. A second, smaller
    /// TextMesh below the main text mentions Space — Next Puzzle, but only
    /// while a later puzzle exists: the input controller hides it on the final
    /// puzzle, so the legend never advertises an action progression cannot
    /// satisfy. Content is written by the deterministic scene builder; the
    /// component exists so scene validation can find and verify the legend in
    /// the built scene.
    /// </summary>
    public sealed class ControlLegend : MonoBehaviour
    {
        [SerializeField] private TextMesh textMesh;
        [SerializeField] private TextMesh nextHintMesh;

        /// <summary>The legend text currently shown, or empty when no TextMesh is wired.</summary>
        public string Text => textMesh != null ? textMesh.text : string.Empty;

        /// <summary>True while the Space-next hint is currently visible.</summary>
        public bool NextHintVisible => nextHintMesh != null && nextHintMesh.gameObject.activeSelf;

        /// <summary>Replaces the legend text.</summary>
        public void SetText(string text)
        {
            if (textMesh != null)
                textMesh.text = text;
        }

        /// <summary>Shows or hides the Space-next hint (hidden on the final puzzle).</summary>
        public void SetNextHintVisible(bool visible)
        {
            if (nextHintMesh != null)
                nextHintMesh.gameObject.SetActive(visible);
        }
    }
}
