using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>Small, self-contained exhibition flow shared by every painting scene.</summary>
    public sealed class PaintingLevelFlow : MonoBehaviour
    {
        [SerializeField] private PaintingCompletionReveal _reveal;
        [SerializeField] private PaintingManipulationController _manipulation;
        [SerializeField] private CanvasGroup _introGroup;
        [SerializeField] private CanvasGroup _continueGroup;
        [SerializeField] private Text _chapterText;
        [SerializeField] private Text _continueText;
        [SerializeField] private string _paintingTitle = "雾谷石桥";
        [SerializeField] private int _paintingNumber = 1;
        [SerializeField] private int _paintingCount = 3;
        [SerializeField] private string _nextScene;

        private bool _started;
        private bool _readyToContinue;
        private bool _testMode;
        private bool _testConfirm;

        public bool HasStarted => _started;
        public bool ReadyToContinue => _readyToContinue;
        public bool CanManipulate => _started && !_readyToContinue;

        private void Awake()
        {
            if (_chapterText != null)
                _chapterText.text = $"第 {_paintingNumber:00} / {_paintingCount:00} 幅\n{_paintingTitle}";
            SetGroup(_introGroup, true);
            SetGroup(_continueGroup, false);
            if (_manipulation != null) _manipulation.SetInputLocked(true);
        }

        private void OnEnable()
        {
            if (_reveal != null) _reveal.RevealCompleted += OnRevealCompleted;
        }

        private void OnDisable()
        {
            if (_reveal != null) _reveal.RevealCompleted -= OnRevealCompleted;
        }

        private void Update()
        {
            bool confirm = _testMode
                ? _testConfirm
                : Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)
                    || Input.GetKeyDown(KeyCode.Return);
            if (!_started && confirm)
            {
                _started = true;
                SetGroup(_introGroup, false);
                if (_manipulation != null) _manipulation.SetInputLocked(false);
            }
            else if (_readyToContinue && confirm)
            {
                if (!string.IsNullOrWhiteSpace(_nextScene)) SceneManager.LoadScene(_nextScene);
            }

            if (_started && !_readyToContinue && Input.GetKeyDown(KeyCode.R))
                _manipulation?.ResetToAuthored();
            if (_started && !_readyToContinue && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && Input.GetKeyDown(KeyCode.Z))
                _manipulation?.Undo();
        }

        /// <summary>
        /// Test-only configuration: makes <see cref="Update"/> read the
        /// scripted confirm signal set by <see cref="SetTestConfirm"/> instead
        /// of live legacy Input, so PlayMode tests drive the intro/continue
        /// state machine deterministically. Player builds keep the bound
        /// mouse/Space/Return confirm.
        /// </summary>
        public void ConfigureForTests()
        {
            _testMode = true;
        }

        /// <summary>
        /// Test-only scripted confirm signal read by <see cref="Update"/> once
        /// <see cref="ConfigureForTests"/> has been called. Mirrors the
        /// edge-triggered press the player's click/Space/Return produces: the
        /// test holds the signal true across exactly the frames it wants the
        /// flow to treat as a confirm, then clears it.
        /// </summary>
        public void SetTestConfirm(bool confirmed)
        {
            _testConfirm = confirmed;
        }

        private void OnRevealCompleted()
        {
            _readyToContinue = true;
            if (_continueText != null)
                _continueText.text = string.IsNullOrWhiteSpace(_nextScene)
                    ? "全部作品已完成\n感谢你修复这场展览"
                    : "作品修复完成\n点击或按空格键进入下一展厅";
            SetGroup(_continueGroup, true);
        }

        private static void SetGroup(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
