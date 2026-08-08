using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// The restrained physical three-step progress indicator of the playable
    /// room: three rounded pips on a small plaque in front of the puzzle slab.
    /// Completed steps read muted teal, the current step warm coral, upcoming
    /// steps near-white, matching the accepted palette. The scene builder
    /// paints step one; the input controller repaints after every puzzle
    /// transition. No UI panel: the indicator stays a physical object on the
    /// stage.
    /// </summary>
    public sealed class ProgressIndicator : MonoBehaviour
    {
        private static readonly Color DoneColor = new Color(0.298f, 0.604f, 0.573f);      // accepted MutedTeal
        private static readonly Color CurrentColor = new Color(0.898f, 0.541f, 0.396f);   // accepted SoftCoral
        private static readonly Color UpcomingColor = new Color(0.969f, 0.957f, 0.933f);  // accepted BoardWhite

        [SerializeField] private MeshRenderer[] pips = new MeshRenderer[0]; // left → right

        /// <summary>0-based step currently highlighted (clamped into the pip range).</summary>
        public int CurrentStep { get; private set; }

        /// <summary>
        /// Repaints the pips for the given zero-based step: earlier steps read
        /// done (teal), the current step reads coral, later steps stay white.
        /// </summary>
        public void SetCurrentStep(int zeroBasedStep)
        {
            CurrentStep = Mathf.Clamp(zeroBasedStep, 0, Mathf.Max(0, pips.Length - 1));

            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null)
                    continue;
                Color color = i < CurrentStep ? DoneColor : i == CurrentStep ? CurrentColor : UpcomingColor;
                pips[i].material.color = color;
                pips[i].material.SetColor("_BaseColor", color);
            }
        }

        /// <summary>Returns the indicator to step one.</summary>
        public void ResetIndicator()
        {
            SetCurrentStep(0);
        }
    }
}
