# 正式美术资产生产管线

## 目的

把 Blender 等 DCC 软件制作的精细模型稳定地接入 Unity，同时保持玩法碰撞、
Object-ID 评分和镜头构图不受美术替换影响。当前程序化模型继续作为代理资产，
直到对应正式模型逐件通过固定镜头检查。

## 文件边界

- DCC 工作文件放在仓库外或 `ArtSource/`（以后若建立，必须加入 Git LFS），不放进
  Unity 的 `Assets`。
- 交付模型只进入 `Assets/Art/SourceModels/`。
- 文件名为 `ROLE_Name_v###.fbx`：ROLE 只能是 `HERO`、`PROP`、`ENV`。
- Unity 材质放 `Assets/Art/Materials/Production/`，不从 FBX 自动提取材质。
- 玩法碰撞体继续由场景构建器生成，不直接使用高模 MeshCollider。

## Blender 场景合同

1. 单位 Metric，Unit Scale 1.0；1 Blender 米等于 1 Unity 米。
2. 正面朝 `-Z`，上方 `+Y`；导出 FBX 使用 `-Z Forward / Y Up`、Apply Transform。
3. 对象原点必须放在可摆放底面中心；桥和亭子原点不能位于几何中心悬空处。
4. 应用 Rotation 与 Scale；对象 Transform 应为旋转 0、缩放 1。
5. 保留硬边和加权法线；不要依靠 Blender 相机、灯光、世界环境或节点材质。
6. 单个英雄模型控制在 30k 三角形以内；远景和小道具控制在 8k 以内。
7. 一件可操作物体只提供一个主根节点；装饰子件不能改变根节点原点。
8. 可选的简化碰撞网格使用同名后缀 `_COL`，LOD 使用 `_LOD0/_LOD1/_LOD2`。

## Unity 自动行为

`ProductionModelImportPolicy` 只处理 `SourceModels`，自动关闭动画、摄像机、灯光、
可见性和嵌入材质，保留输入法线、计算 Mikk 切线，并锁定单位比例。这样不会意外
影响旧原型资产。

导入后运行：

`Tools > PerspectivePuzzle > Production Art > Validate Source Models`

验证通过不等于视觉验收。每次替换英雄模型后还必须运行视觉回归，并人工检查操作
视角、构图视角、轮廓目标和 Object-ID 目标。

## 第一批正式资产顺序

1. `HERO_ArchBridge_v001`：直接由玩家操作，最能暴露比例和原点问题。
2. `HERO_Pavilion_v001`：验证屋檐剪影、柱体连接和多材质槽。
3. `HERO_MiddleMountain_v001`：验证更复杂轮廓仍能保持目标画面可读。
4. `PROP_CloudPine_v001` 与 `PROP_ForegroundRock_v001`。

每次只替换一件并重新生成目标图，禁止一次性换完整场景后再定位评分漂移。

## 正式美术验收

- 操作镜头下，轮廓、接触面和前后关系清楚。
- 构图镜头下，不出现穿插、悬空、亭盖脱模或桥面断裂。
- 使用简化代理碰撞，不因装饰细节让选择变得困难。
- Object-ID 捕获仍保持一件物体一种纯色。
- 固定镜头视觉回归的变化得到明确审阅和基线更新。

