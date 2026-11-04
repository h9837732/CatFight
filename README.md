# CatFight

类《杀戮尖塔》(Slay the Spire) 的 2D 卡牌构筑战斗游戏。

- **引擎**：Unity 6000.6.3f1 (Unity 6)，Built-in 渲染管线，Linear 色彩空间
- **平台目标**：Windows x64 / WebGL
- **设计基准分辨率**：1920×1080

## 目录结构

```
CatFight/
├── Assets/
│   ├── Scenes/      场景
│   ├── Scripts/     游戏逻辑脚本
│   ├── Art/         美术资源（立绘、卡面、UI）
│   ├── Audio/       音频
│   ├── Prefabs/     预制体
│   ├── Data/        数据配置（卡牌、敌人、遗物 ScriptableObject）
│   └── Settings/    项目设置资源（布局配置等）
├── Docs/
│   └── Design/      设计文档与草图
├── Packages/        UPM 依赖清单
└── ProjectSettings/ 项目设置
```

## 设计文档

| 文档 | 说明 |
|---|---|
| [战斗系统需求文档 v1.0](Docs/Design/战斗系统需求文档_v1.0.md) | 战斗场景完整需求：布局坐标、手牌遮挡公式、血条/能量条规则、回合状态机、伤害结算链、意图与状态系统、交互规范、28 条验收用例 |
| [战斗草图 v0.0.0](Docs/Design/战斗草图_v0.0.0.png) | 手绘战斗界面草图（原始设计起点） |

## 核心玩法速览

- **回合制**：玩家先手 → 出牌 → 结束回合 → 敌人依次行动
- **能量系统**：每回合 3 点能量（体力条），按上限均分切块显示
- **手牌**：每回合抽 5 张，上限 10 张，超出手牌区按公式叠压排列
- **防御**：格挡 (Block) 抵消伤害，回合开始清零
- **敌人意图**：头顶预告下一步行动（攻击 N / 攻击 N×M / 防御 / 增益 / 未知）
- **构筑**：遗物（被动）+ 道具（主动消耗）+ 卡牌升级

## 开发环境

工程数据目录已从 C 盘迁移到 F 盘（详见 `Docs/Setup/环境迁移记录.md`），保持 C 盘空间可用。

## 版本控制

Git + Git LFS（美术/音频/模型等二进制资源）。克隆后请先执行：

```bash
git lfs install
```

## 当前实现进度（原型 v0.1）

| 模块 | 状态 | 说明 |
|---|---|---|
| 开始界面 | ✅ 可运行 | 新游戏 / 继续 / 设置 / 退出；存档覆盖确认；设置可持久化 |
| 地图 | ✅ 可运行 | 按种子生成 14 行带状图；节点类型可见；点击推进；未选分支淡化 |
| 存档 | ✅ 可运行 | JSON，存种子 + 已走路径 + 血量 / 金币 / 卡组 / 遗物 |
| 战斗 | ✅ 可运行 | 能量 / 格挡 / 伤害结算链 / 状态 / 意图 / 抽牌弃牌 / 胜负结算 |
| 卡牌奖励 | ✅ 可运行 | 胜利后三选一或跳过，写入卡组 |
| 节点事件 | ⚠️ 占位 | 休息 / 商店 / 宝箱 / 事件均为可玩的最小实现，待正式内容 |
| 美术 | ❌ 占位 | 全部程序生成图形 + 系统字体；接入素材后替换 `UiKit` 内部实现 |

### 场景

`Assets/Scenes/MainMenu.unity`、`Map.unity`、`Battle.unity`，已写入 Build Settings。
每个场景只放一个 bootstrap 物体，界面全部由运行时代码构建——接入正式 UI 素材时不用重做场景。
菜单 `CatFight → Scenes → Rebuild All` 可一键重建。

### 开发环境注意

- 项目路径必须纯 ASCII（见 `Docs/Design/地图系统设计_v1.0.md` §16.6）
- 项目启用新版 Input System（`activeInputHandler = 1`），**不要用 `UnityEngine.Input`**，
  统一走 `CatFight.Core.InputCompat`；EventSystem 必须用 `InputSystemUIInputModule`
- Editor 需开启 Run In Background，否则失焦时游戏冻结在第 1 帧
