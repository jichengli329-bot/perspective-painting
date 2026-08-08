using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Translucent snapped placement preview shown on hover. The scene builder
    /// assigns the preview renderer (a rounded teal piece mesh) and colors;
    /// <see cref="SetValid"/> switches between the accepted teal/coral palette.
    /// The preview never mutates puzzle state.
    /// </summary>
    public sealed class PlacementPreview : MonoBehaviour
    {
        [SerializeField] private MeshRenderer previewRenderer;
        [SerializeField] private Color validColor = new Color(0.298f, 0.604f, 0.573f, 0.55f); // accepted MutedTeal, translucent
        [SerializeField] private Color invalidColor = new Color(0.898f, 0.541f, 0.396f, 0.55f); // accepted SoftCoral, translucent

        /// <summary>Shows or hides the preview.</summary>
        public void SetVisible(bool visible)
        {
            if (previewRenderer != null)
                previewRenderer.enabled = visible;
        }

        /// <summary>Moves the preview to a world position (usually a snapped cell center).</summary>
        public void SetWorldPosition(Vector3 world)
        {
            transform.position = world;
        }

        /// <summary>True when the hovered cell may be placed on; false colors it as a warning.</summary>
        public void SetValid(bool valid)
        {
            if (previewRenderer != null)
                previewRenderer.material.SetColor("_BaseColor", valid ? validColor : invalidColor);
        }
    }
}
