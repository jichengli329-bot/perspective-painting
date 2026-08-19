using System;
using UnityEngine;
using UnityEngine.UI;
using PerspectivePuzzle.Domain;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Product-facing T-011 guidance rail: keeps a live beauty view from the
    /// Composition Camera beside the target painting, then translates the
    /// machine score into one calm progress line and one worst-piece focus
    /// hint. Object-ID colours, grids and raw percentages are never shown.
    /// </summary>
    public sealed class PaintingGuidancePresenter : MonoBehaviour
    {
        [SerializeField] private PaintingCompositionEvaluator _evaluator;
        [SerializeField] private PaintingManipulationController _manipulation;
        [SerializeField] private PaintingGoalGate _goalGate;
        [SerializeField] private Camera _compositionCamera;
        [SerializeField] private RawImage _targetImage;
        [SerializeField] private RawImage _targetPieceOutline;
        [SerializeField] private RawImage _liveImage;
        [SerializeField] private RectTransform _progressFill;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _focusText;
        [SerializeField] private Text _secondaryStatusText;
        [SerializeField] private CanvasGroup _comparisonGroup;
        [SerializeField] private RawImage _comparisonTarget;
        [SerializeField] private RawImage _comparisonLive;
        [SerializeField] private string[] _pieceNames = Array.Empty<string>();
        [SerializeField] private bool[] _hintEligible = Array.Empty<bool>();
        [SerializeField] private bool _tutorialMode;
        [SerializeField] private PaintingTutorialSequence _tutorialSequence;
        [SerializeField] private PaintingDepthTutorialSequence _depthTutorialSequence;
        [SerializeField, Min(64)] private int _liveWidth = 640;
        [SerializeField, Min(64)] private int _liveHeight = 360;
        [SerializeField, Min(0.01f)] private float _progressSmoothTime = 0.22f;
        [SerializeField, Range(0f, 0.25f)] private float _focusSwitchMargin = 0.04f;
        [SerializeField, Min(0f)] private float _focusMinimumHold = 0.75f;

        private RenderTexture _liveTexture;
        private RenderTexture _previousCameraTarget;
        private bool _previousCameraEnabled;
        private float _targetProgress;
        private float _displayedProgress;
        private float _progressVelocity;
        private int _worstPieceIndex = -1;
        private float _lastFocusSwitchTime = float.NegativeInfinity;
        private bool _configured;
        private Texture2D _outlineTexture;
        private PaintingManipulablePiece _lastOutlinedPiece;
        private string _passiveFocus = "让当前画面接近目标";

        public bool IsConfigured => _configured;
        public RenderTexture LiveTexture => _liveTexture;
        public Texture TargetTexture => _targetImage != null ? _targetImage.texture : null;
        public float DisplayedProgress => _displayedProgress;
        public int WorstPieceIndex => _worstPieceIndex;
        public string Status => _statusText != null ? _statusText.text : string.Empty;
        public string Focus => _focusText != null ? _focusText.text : string.Empty;

        private void Awake()
        {
            if (_evaluator != null && _compositionCamera != null && _targetImage != null
                && _liveImage != null && _progressFill != null && _statusText != null
                && _focusText != null && _pieceNames != null && _pieceNames.Length > 0)
            {
                Configure();
            }
        }

        private void OnEnable()
        {
            if (_configured)
                _evaluator.Evaluated += OnEvaluated;
        }

        private void OnDisable()
        {
            if (_configured && _evaluator != null)
                _evaluator.Evaluated -= OnEvaluated;
            ReleaseLiveTexture();
        }

        private void OnDestroy()
        {
            ReleaseLiveTexture();
        }

        private void Update()
        {
            if (!_configured)
                return;
            _displayedProgress = Mathf.SmoothDamp(
                _displayedProgress, _targetProgress, ref _progressVelocity, _progressSmoothTime);
            ApplyProgress(_displayedProgress);
            UpdateSelectedOutline();
            if (Input.GetKeyDown(KeyCode.H))
            {
                PaintingManipulablePiece focus = WorstPlayablePiece();
                if (focus != null) _manipulation.SelectPiece(focus);
            }
            if (Input.GetKeyDown(KeyCode.G))
            {
                PaintingManipulablePiece assisted = WorstPlayablePiece();
                if (assisted != null && _manipulation.AssistPlace(assisted))
                {
                    _statusText.text = "已帮你摆好：" + _pieceNames[_worstPieceIndex];
                    _evaluator.RequestEvaluationNow();
                }
            }
            string visibleFocus = _passiveFocus;
            if (_tutorialMode && _tutorialSequence != null
                && (_goalGate == null || !_goalGate.IsSatisfied))
                visibleFocus = "入门提示：" + visibleFocus;
            if (_tutorialMode && _tutorialSequence != null)
            {
                if (_tutorialSequence.Assistance == PaintingTutorialSequence.AssistanceLevel.Warm)
                    visibleFocus += "  •  金色圆环附近吸附更强";
                else if (_tutorialSequence.Assistance == PaintingTutorialSequence.AssistanceLevel.Rescue)
                    visibleFocus += "  •  仍卡住可按 G 辅助摆好当前景物";
            }
            if (_depthTutorialSequence != null)
            {
                string lesson = _depthTutorialSequence.CurrentStep switch
                {
                    1 => "第一步·复习构图  ",
                    2 => "第二步·调整山体远近  ",
                    _ => "第三步·处理凉亭遮挡  "
                };
                visibleFocus = lesson + visibleFocus;
            }
            if (_manipulation.SelectedPiece != null && _manipulation.UsesPlacementLattice)
            {
                string band = _manipulation.SelectedDepthBand switch
                {
                    PaintingManipulationController.DepthBand.Far => "远景层",
                    PaintingManipulationController.DepthBand.Near => "前景层",
                    _ => "中景层"
                };
                visibleFocus += "  •  当前：" + band;
            }
            if (_manipulation.SelectedPiece != null)
            {
                string selectedName = DisplayNameForPiece(_manipulation.SelectedPiece);
                string selectedRegion = TargetRegionForPiece(selectedName);
                if (!string.IsNullOrEmpty(selectedRegion))
                    visibleFocus += "  ｜  已选“" + selectedName + "”：目标在" + selectedRegion;
            }
            string rotationHelp = RotationHelpForSelection();
            if (!string.IsNullOrEmpty(rotationHelp))
                visibleFocus += "  ｜  " + rotationHelp;
            _focusText.text = visibleFocus;
            if (_comparisonGroup != null)
            {
                bool comparing = Input.GetKey(KeyCode.Tab);
                _comparisonGroup.alpha = comparing ? 1f : 0f;
                _comparisonGroup.blocksRaycasts = false;
                _comparisonGroup.interactable = false;
            }
            if (_secondaryStatusText != null && _goalGate != null)
                _secondaryStatusText.text = _goalGate.SecondaryProgress >= 0.82f
                    ? "侧面印章  •  已对齐" : "侧面印章  •  调整中";
        }

        /// <summary>Validates serialized wiring, creates the live view and initializes restrained copy.</summary>
        public void Configure()
        {
            if (_evaluator == null) throw new ArgumentNullException(nameof(_evaluator));
            if (_compositionCamera == null) throw new ArgumentNullException(nameof(_compositionCamera));
            if (_targetImage == null) throw new ArgumentNullException(nameof(_targetImage));
            if (_liveImage == null) throw new ArgumentNullException(nameof(_liveImage));
            if (_progressFill == null) throw new ArgumentNullException(nameof(_progressFill));
            if (_statusText == null) throw new ArgumentNullException(nameof(_statusText));
            if (_focusText == null) throw new ArgumentNullException(nameof(_focusText));
            if (_pieceNames == null || _pieceNames.Length == 0)
                throw new ArgumentException("At least one display piece name is required.", nameof(_pieceNames));
            if (_liveWidth < 64 || _liveHeight < 64)
                throw new ArgumentOutOfRangeException(nameof(_liveWidth));

            ReleaseLiveTexture();
            _liveTexture = new RenderTexture(_liveWidth, _liveHeight, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = "Painting Live View (Runtime)",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false
            };
            _liveTexture.Create();
            _previousCameraEnabled = _compositionCamera.enabled;
            _previousCameraTarget = _compositionCamera.targetTexture;
            _compositionCamera.targetTexture = _liveTexture;
            // The composition camera used to be a manual-render-only scoring
            // sensor. A RenderTexture assignment alone does not produce live
            // frames while the Camera component is disabled.
            _compositionCamera.enabled = true;
            _liveImage.texture = _liveTexture;
            _liveImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            if (_comparisonTarget != null) _comparisonTarget.texture = _targetImage.texture;
            if (_comparisonLive != null) _comparisonLive.texture = _liveTexture;
            if (_comparisonGroup != null) _comparisonGroup.alpha = 0f;

            _targetProgress = _displayedProgress = 0f;
            _progressVelocity = 0f;
            _worstPieceIndex = -1;
            _lastFocusSwitchTime = float.NegativeInfinity;
            _statusText.text = "调整景物";
            _focusText.text = "让当前画面接近目标";
            _passiveFocus = _focusText.text;
            if (_targetPieceOutline != null)
            {
                _targetPieceOutline.color = Color.white;
                _targetPieceOutline.texture = null;
            }
            ApplyProgress(0f);
            _configured = true;
        }

        private void OnEvaluated(CompositionScoreResult result)
        {
            if (result == null)
                return;
            _targetProgress = Mathf.Clamp01(result.WeightedScore);

            int count = Mathf.Min(result.Pieces.Count, _pieceNames.Length);
            float worstCoverage = float.PositiveInfinity;
            int candidateIndex = -1;
            for (int i = 0; i < count; i++)
            {
                if (_hintEligible != null && _hintEligible.Length == count && !_hintEligible[i])
                    continue;
                if (result.Pieces[i].TargetCoverage < worstCoverage)
                {
                    worstCoverage = result.Pieces[i].TargetCoverage;
                    candidateIndex = i;
                }
            }

            if (_tutorialMode && _evaluator.LatestDiagnostics != null)
            {
                if (!IsNearAligned(6)) candidateIndex = 6; // Arch Bridge first
                else if (!IsNearAligned(5)) candidateIndex = 5; // then Pavilion
            }
            else if (_depthTutorialSequence != null && _evaluator.LatestDiagnostics != null)
                candidateIndex = _depthTutorialSequence.PreferredEvaluatorIndex(_evaluator.LatestDiagnostics);

            float currentCoverage = _worstPieceIndex >= 0 && _worstPieceIndex < count
                ? result.Pieces[_worstPieceIndex].TargetCoverage
                : float.PositiveInfinity;
            bool holdExpired = Time.unscaledTime - _lastFocusSwitchTime >= _focusMinimumHold;
            bool materiallyWorse = worstCoverage <= currentCoverage - _focusSwitchMargin;
            if (_worstPieceIndex < 0 || candidateIndex == _worstPieceIndex || (holdExpired && materiallyWorse))
            {
                if (candidateIndex != _worstPieceIndex)
                    _lastFocusSwitchTime = Time.unscaledTime;
                _worstPieceIndex = candidateIndex;
            }

            bool productPass = result.PassesPolicy
                && (_tutorialSequence == null || _tutorialSequence.CompletionReady);
            productPass &= _depthTutorialSequence == null || _depthTutorialSequence.CompletionReady;
            if (productPass)
            {
                _statusText.text = "画面已经重合";
                _passiveFocus = "保持这个构图";
            }
            else
            {
                _statusText.text = result.WeightedScore < 0.55f
                    ? "调整景物"
                    : result.WeightedScore < 0.82f ? "构图正在成形" : "已经很接近了";
                bool near = _evaluator.LatestDiagnostics != null && _worstPieceIndex >= 0
                    && _worstPieceIndex < _evaluator.LatestDiagnostics.Count
                    && _evaluator.LatestDiagnostics[_worstPieceIndex].Guidance == VisualGuidanceKind.NearlyAligned;
                _statusText.text = near ? "这一层已经摆对" : _statusText.text;
                _passiveFocus = _worstPieceIndex >= 0 && _evaluator.LatestDiagnostics != null
                    && _worstPieceIndex < _evaluator.LatestDiagnostics.Count
                    ? TutorialGuidanceText(_evaluator.LatestDiagnostics[_worstPieceIndex].Guidance,
                        _pieceNames[_worstPieceIndex])
                    : "让当前画面接近目标";
            }

            if (_evaluator.LatestDiagnostics != null)
            {
                for (int i = 0; i < _evaluator.Pieces.Count && i < _evaluator.LatestDiagnostics.Count; i++)
                {
                    var handle = _evaluator.Pieces[i] != null
                        ? _evaluator.Pieces[i].GetComponent<PaintingManipulablePiece>() : null;
                    if (handle != null)
                        handle.SetNearAligned((_hintEligible == null || _hintEligible.Length != _evaluator.Pieces.Count || _hintEligible[i])
                            && _evaluator.LatestDiagnostics[i].Guidance == VisualGuidanceKind.NearlyAligned);
                }
            }
        }

        private string GuidanceText(VisualGuidanceKind guidance, string piece)
        {
            string action = guidance switch
            {
                VisualGuidanceKind.MoveLeft => "向画面左侧拖动一格，再看实时画面",
                VisualGuidanceKind.MoveRight => "向画面右侧拖动一格，再看实时画面",
                VisualGuidanceKind.MoveUp => "向画面上方拖动一格，再看实时画面",
                VisualGuidanceKind.MoveDown => "向画面下方拖动一格，再看实时画面",
                VisualGuidanceKind.BringForward => "向前景移动一层，让轮廓更大、更靠前",
                VisualGuidanceKind.SendBackward => "向远景移动一层，让轮廓更小、更靠后",
                VisualGuidanceKind.Rotate => RotationInstructionForPiece(piece),
                VisualGuidanceKind.ReconsiderOcclusion => "前后换一层，观察它与相邻景物的遮挡顺序",
                VisualGuidanceKind.NearlyAligned => "位置和轮廓已经成立，可以处理下一件景物",
                _ => "对照金色轮廓，先调整位置，再调整前后和朝向",
            };
            string region = TargetRegionForPiece(piece);
            return "请调整“" + piece + "”" + (string.IsNullOrEmpty(region) ? string.Empty : "（目标在" + region + "）")
                + "：" + action;
        }

        private PaintingManipulablePiece WorstPlayablePiece()
        {
            if (_worstPieceIndex < 0 || _worstPieceIndex >= _evaluator.Pieces.Count)
                return null;
            if (_hintEligible != null && _hintEligible.Length == _evaluator.Pieces.Count
                && !_hintEligible[_worstPieceIndex])
                return null;
            Transform root = _evaluator.Pieces[_worstPieceIndex] != null
                ? _evaluator.Pieces[_worstPieceIndex].transform : null;
            return root != null ? root.GetComponent<PaintingManipulablePiece>() : null;
        }

        private bool IsNearAligned(int index)
        {
            return index >= 0 && index < _evaluator.LatestDiagnostics.Count
                && _evaluator.LatestDiagnostics[index].Guidance == VisualGuidanceKind.NearlyAligned;
        }

        private string TutorialGuidanceText(VisualGuidanceKind guidance, string piece)
        {
            string controls = _tutorialMode
                ? "  ｜  靠近正确格时会自动吸附"
                : "  ｜  H：选中提示物体  ｜  G：辅助摆好";
            return GuidanceText(guidance, piece) + controls;
        }

        private string RotationHelpForSelection()
        {
            PaintingManipulablePiece selected = _manipulation != null ? _manipulation.SelectedPiece : null;
            if (selected == null || !_manipulation.AllowsRotation) return string.Empty;
            return RotationInstruction(selected);
        }

        private string RotationInstructionForPiece(string displayName)
        {
            if (_pieceNames != null)
            {
                for (int i = 0; i < _pieceNames.Length && i < _evaluator.Pieces.Count; i++)
                {
                    if (_pieceNames[i] != displayName || _evaluator.Pieces[i] == null) continue;
                    PaintingManipulablePiece handle = _evaluator.Pieces[i].GetComponent<PaintingManipulablePiece>();
                    if (handle != null) return RotationInstruction(handle);
                }
            }
            return "向左或向右旋转一次，并观察实时画面的轮廓变化";
        }

        private string DisplayNameForPiece(PaintingManipulablePiece piece)
        {
            if (piece == null || _pieceNames == null) return string.Empty;
            for (int i = 0; i < _evaluator.Pieces.Count && i < _pieceNames.Length; i++)
                if (_evaluator.Pieces[i] != null && _evaluator.Pieces[i].transform == piece.transform)
                    return _pieceNames[i];
            return piece.Root != null ? piece.Root.name : string.Empty;
        }

        private string RotationInstruction(PaintingManipulablePiece selected)
        {
            float yaw = selected.AuthoredSignedYawOffset(selected.Root.rotation);
            float step = Mathf.Max(1f, _manipulation.RotationStepDegrees);
            int presses = Mathf.RoundToInt(Mathf.Abs(yaw) / step);
            if (presses <= 0) return "朝向已对齐";
            string key = yaw > 0f ? "Q" : "E";
            string direction = yaw > 0f ? "向左旋转" : "向右旋转";
            return direction + "：按 " + key + " " + presses + " 次";
        }

        private string TargetRegionForPiece(string displayName)
        {
            if (_evaluator == null || _evaluator.Target == null || _pieceNames == null) return string.Empty;
            int pieceIndex = Array.IndexOf(_pieceNames, displayName);
            if (pieceIndex < 0 || pieceIndex >= _evaluator.Pieces.Count || _evaluator.Pieces[pieceIndex] == null)
                return string.Empty;
            uint id = _evaluator.Pieces[pieceIndex].Id;
            CompositionIdBuffer target = _evaluator.Target;
            long sumX = 0, sumY = 0, count = 0;
            for (int y = 0; y < target.Height; y++)
            for (int x = 0; x < target.Width; x++)
            {
                if (target.GetPixel(x, y) != id) continue;
                sumX += x; sumY += y; count++;
            }
            if (count == 0) return string.Empty;
            float nx = (float)sumX / count / Mathf.Max(1, target.Width - 1);
            float ny = (float)sumY / count / Mathf.Max(1, target.Height - 1);
            string horizontal = nx < 0.37f ? "左侧" : nx > 0.63f ? "右侧" : "中央";
            string vertical = ny < 0.38f ? "下方" : ny > 0.68f ? "上方" : "中部";
            return vertical + horizontal;
        }

        private void UpdateSelectedOutline()
        {
            if (_targetPieceOutline == null || _manipulation == null || _evaluator.Target == null) return;
            PaintingManipulablePiece selected = _manipulation.SelectedPiece;
            if (selected == null) selected = WorstPlayablePiece();
            if (selected == _lastOutlinedPiece) return;
            _lastOutlinedPiece = selected;
            if (selected == null)
            {
                _targetPieceOutline.enabled = false;
                return;
            }
            uint id = 0;
            for (int i = 0; i < _evaluator.Pieces.Count; i++)
                if (_evaluator.Pieces[i] != null && _evaluator.Pieces[i].transform == selected.transform)
                    id = _evaluator.Pieces[i].Id;
            if (id == 0) { _targetPieceOutline.enabled = false; return; }

            CompositionIdBuffer target = _evaluator.Target;
            if (_outlineTexture == null || _outlineTexture.width != target.Width || _outlineTexture.height != target.Height)
            {
                if (_outlineTexture != null) Destroy(_outlineTexture);
                _outlineTexture = new Texture2D(target.Width, target.Height, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            }
            var pixels = new Color32[target.PixelCount];
            for (int y = 0; y < target.Height; y++)
            for (int x = 0; x < target.Width; x++)
            {
                int index = y * target.Width + x;
                if (target.GetPixel(x, y) != id) continue;
                bool edge = false;
                const int outlineRadius = 3;
                for (int oy = -outlineRadius; oy <= outlineRadius && !edge; oy++)
                for (int ox = -outlineRadius; ox <= outlineRadius; ox++)
                {
                    int sx = x + ox, sy = y + oy;
                    if (sx < 0 || sy < 0 || sx >= target.Width || sy >= target.Height
                        || target.GetPixel(sx, sy) != id) { edge = true; break; }
                }
                pixels[index] = edge
                    ? new Color32(255, 210, 80, 245)
                    : new Color32(255, 185, 45, 72);
            }
            _outlineTexture.SetPixels32(pixels);
            _outlineTexture.Apply(false, false);
            _targetPieceOutline.texture = _outlineTexture;
            _targetPieceOutline.enabled = true;
        }

        private void ApplyProgress(float value)
        {
            Vector2 anchorMax = _progressFill.anchorMax;
            anchorMax.x = Mathf.Clamp01(value);
            _progressFill.anchorMax = anchorMax;
        }

        private void ReleaseLiveTexture()
        {
            if (_compositionCamera != null && _compositionCamera.targetTexture == _liveTexture)
            {
                _compositionCamera.targetTexture = _previousCameraTarget;
                _compositionCamera.enabled = _previousCameraEnabled;
            }
            if (_liveImage != null && _liveImage.texture == _liveTexture)
                _liveImage.texture = null;
            if (_liveTexture != null)
            {
                _liveTexture.Release();
                Destroy(_liveTexture);
                _liveTexture = null;
            }
            if (_outlineTexture != null)
            {
                Destroy(_outlineTexture);
                _outlineTexture = null;
            }
        }
    }
}
