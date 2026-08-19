# 第一关美术资产清单

## 当前正式基线

T-026 的程序化青瓷套件保留 28 个生成 Mesh、16 个专属材质/Volume 资源和一个
项目自有青瓷 Shader。场景构建器是这些资源的唯一生成来源；`.unity` 场景保存生成
结果，但不得手工维护另一套命名或参数。

按功能分组：

- 山体：Far Mountain 4 件、Middle Mountain V2 4 件、山纹装饰 2 件。
- 亭子：V2 主/上层屋顶、台阶；基础、柱、檐梁仍复用通用 Cycle 2 资产。
- 桥：V2 桥面、6 段 V2 栏杆、桥头、柱帽、踏步。
- 松树：云冠和枝干；树干继续复用通用资产。
- 构图板：三层板、纵横金色镶线。

## T-027 清理记录

以下 11 个 Mesh 已有 V2 或最终替代品，并经构建器精确名称搜索和全 Assets GUID
搜索确认没有引用，因此连同 `.meta` 一并删除：

- `T026_ArchBridgeDeck`
- `T026_MiddleMountain_Left`
- `T026_MiddleMountain_Main`
- `T026_PavilionRoof_Main`
- `T026_PavilionRoof_Upper`
- `T026_BridgeRailSegment_0` 至 `_5`

删除是不可逆的工作区操作，但这些文件仍可从 Git 提交 `767976e` 恢复。V2 资产、
场景引用、目标图片和玩家构建均被保留。

## 后续替换原则

正式 DCC 模型接入时，保留当前程序资产作为代理和回退，不直接覆盖其 GUID。新模型
进入 `Assets/Art/SourceModels`，在构建器中以单件开关替换；通过视觉回归和玩法测试
后再删除对应代理。任何删除都必须重复“源码精确名称 + 全资产 GUID”双重检查。

