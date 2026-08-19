# 固定镜头视觉回归

## 为什么需要

玩法依赖单一构图视角，普通单元测试无法发现镜头偏移、材质失效、遮挡变化和画风
漂移。视觉回归比较九张已审阅画面：四关各自的操作与构图视角，以及 Twin Seal
侧面印章视角。

## 使用

- 完整运行：`Tools > PerspectivePuzzle > Visual Regression > Capture And Compare All`
- 只比较已有截图：`Tools > PerspectivePuzzle > Visual Regression > Compare Existing Captures`
- 批处理入口：`PerspectivePuzzle.EditorTools.PaintingVisualRegression.RunAll`

输出位于 `Logs/VisualRegression/`：

- `report.md`：每个镜头的平均 RGB 误差、变化像素比例和通过状态。
- `*_Diff.png`：红色热力差异图。

当前阈值允许微小 GPU/抗锯齿波动，但会拦截明显构图漂移：平均绝对 RGB 误差
不超过 0.012，变化像素不超过 6%。它是“需要审阅”的信号，不代替人的美术判断。

## 更新基线规则

只有在以下条件全部成立时才能替换 `docs/visual-regression/baselines/`：

1. 修改是有意的，不是摄像机或场景构建器意外漂移。
2. 操作镜头和构图镜头均人工检查。
3. Object-ID、轮廓、EditMode、PlayMode 和 Windows 构建通过。
4. 基线图片与产生变化的代码在同一个提交中。

禁止为了让失败消失而盲目复制新截图覆盖基线。

