# 公开发布细则

## 发布目标

本次发布的目标是建立一个可持续更新、可供试玩者下载、也能展示工程质量的项目主页，而不是宣布正式商业发售。

## 推荐发布结构

### 1. GitHub 仓库：源码与开发记录

推荐仓库名：`perspective-painting`。

项目所有者已确认首次发布设为 **Public**。源码可见，但采用保留全部权利的专有许可。

仓库包含：

- Unity 源码、场景和项目设置；
- 目标图片与项目自制美术资源；
- README、产品说明、架构说明、测试和第三方声明；
- Git 历史。

仓库不包含：

- `Library/`、`Logs/`、`UserSettings/`、测试输出和本机缓存；
- API Key、账号凭据或 Unity 许可证数据；
- 解压后的 Windows 构建目录。

### 2. GitHub Releases：Windows 试玩包

将整个 `Builds/WindowsPainting` 目录压缩为一个 ZIP，再上传为版本附件。建议首个标签：

```text
v0.1.0-playtest
```

建议附件名：

```text
PerspectivePainting-v0.1.0-playtest-windows-x64.zip
```

Release 必须写明：开发中、仅 Windows、键鼠操作、无存档、已知难度和反馈入口。

GitHub 官方当前允许单个 Release 附件小于 2 GiB；本项目 Windows 构建约 0.31 GiB，适合放在 Release，而不应提交进源码历史。

### 3. itch.io：玩家展示页（第二阶段）

itch.io 更适合面向普通玩家展示封面、3–5 张截图、介绍和 Windows ZIP，也能查看下载数据。但当前页面还缺少正式封面、宣传视频和稳定的玩家反馈入口，因此建议在 GitHub 首次发布并完成一轮外部试玩后再创建 itch.io 页面。

## 首次发布检查清单

- [x] 项目所有者确认仓库为 Public；
- [x] 项目所有者确认许可证为保留全部权利；
- [ ] README 中的截图、操作和四关说明与当前构建一致；
- [ ] `git status` 干净；
- [ ] 凭据扫描无结果；
- [ ] 所有跟踪文件小于 GitHub 单文件限制；
- [ ] PlayMode 与 EditMode 测试通过；
- [ ] Windows 构建成功并完成独立进程冒烟；
- [ ] ZIP 在新目录解压后可运行；
- [ ] GitHub 远程仓库上传完成；
- [ ] Release 下载链接在未登录窗口中可访问；
- [ ] 远程验证完成后，才删除本地可重新生成的缓存。

## 建议仓库简介

```text
A stylized 3D perspective puzzle: arrange miniature scenery to reconstruct a 2D painting from one exact viewpoint. / 在微缩展台中摆放三维景物，从指定视角重构一幅画。
```

建议 Topics：

```text
unity, puzzle-game, perspective, optical-illusion, diorama, forced-perspective, csharp, urp
```

## 建议 v0.1.0 Release 说明

### 内容

- 四个山水构图关卡；
- 实时目标视角对照；
- 移动、前后层和定量旋转提示；
- 宽容的视觉等价判定；
- 第四关双视角实验；
- Windows x64 开发构建。

### 已知限制

- 仅键鼠；
- 没有存档与设置；
- 美术仍为程序化垂直切片；
- 部分复杂构图仍需要继续降低理解成本；
- Windows SmartScreen 可能提示未知发布者，因为构建尚未进行代码签名。

## 本地清理规则

远程仓库和 Release 验证成功前，不删除任何本地文件。

验证后可删除并由 Unity 自动重建：

- `Library/`（当前约 1.72 GiB）；
- `Logs/`（当前约 0.13 GiB）；
- `TestResults/`、`outputs/`；
- 已确认上传的临时 ZIP。

是否保留 `Builds/WindowsPainting` 由项目所有者决定。它约 0.31 GiB，保留可立即试玩，删除可节省空间但下次需要重新构建。

## 本次候选包

```text
文件：PerspectivePainting-v0.1.0-playtest-windows-x64.zip
大小：62.3 MiB
SHA-256：B8B70F8F0A4CE3845D78631D0A65C196429E082DDE0AAB06960807DEE33713CC
```

候选包已在独立目录解压，数据目录完整，不含 Burst `DoNotShip` 调试数据；隐藏启动 15 秒保持响应，运行日志中的目标异常匹配数为 0。

禁止删除：

- `Assets/`、`Packages/`、`ProjectSettings/`；
- `.git/`；
- `docs/` 和测试；
- 任何尚未推送的提交。
