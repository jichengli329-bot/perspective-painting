using UnityEngine;
using UnityEngine.UI;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>
    /// Replaces Unity's Latin-only built-in UI font at runtime with an
    /// installed CJK font. The scene stays portable and Windows builds render
    /// Chinese guidance without embedding a platform font file.
    /// </summary>
    public sealed class PaintingChineseFontInstaller : MonoBehaviour
    {
        private static readonly string[] PreferredFonts =
        {
            "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Noto Sans CJK SC"
        };

        private Font _runtimeFont;

        private void Awake()
        {
            _runtimeFont = Font.CreateDynamicFontFromOSFont(PreferredFonts, 32);
            if (_runtimeFont == null)
            {
                Debug.LogWarning("未找到可用的中文系统字体，将继续使用场景默认字体。", this);
                return;
            }

            _runtimeFont.name = "Painting Chinese UI (Runtime)";
            foreach (Text text in GetComponentsInChildren<Text>(true))
                text.font = _runtimeFont;
        }

        private void OnDestroy()
        {
            if (_runtimeFont != null)
                Destroy(_runtimeFont);
        }
    }
}
