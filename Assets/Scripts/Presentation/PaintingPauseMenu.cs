using System;
using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>Small keyboard-first pause/help layer for the gallery build.</summary>
    public sealed class PaintingPauseMenu : MonoBehaviour
    {
        [SerializeField] private PaintingLevelFlow _flow;
        [SerializeField] private PaintingManipulationController _manipulation;
        [SerializeField] private CanvasGroup _panel;

        public bool IsPaused { get; private set; }

        private void Awake()
        {
            if (_flow != null && _manipulation != null && _panel != null)
                Configure(_flow, _manipulation, _panel);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && _flow != null && _flow.CanManipulate
                && !_manipulation.IsCarrying && !_manipulation.IsSettling)
                SetPaused(!IsPaused);
        }

        public void Configure(PaintingLevelFlow flow, PaintingManipulationController manipulation, CanvasGroup panel)
        {
            _flow = flow != null ? flow : throw new ArgumentNullException(nameof(flow));
            _manipulation = manipulation != null ? manipulation : throw new ArgumentNullException(nameof(manipulation));
            _panel = panel != null ? panel : throw new ArgumentNullException(nameof(panel));
            SetPanel(false);
        }

        public void SetPaused(bool paused)
        {
            if (_flow == null || _manipulation == null || _panel == null)
                return;
            if (paused && !_flow.CanManipulate)
                return;
            IsPaused = paused;
            SetPanel(paused);
            _manipulation.SetInputLocked(paused || !_flow.CanManipulate);
        }

        private void SetPanel(bool visible)
        {
            _panel.alpha = visible ? 1f : 0f;
            _panel.interactable = visible;
            _panel.blocksRaycasts = visible;
        }
    }
}
