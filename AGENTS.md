# SlayTheStella 项目汇总

> 本文档是项目结构的快速汇总，用于快速了解仓库。
> **如有必要，可随时更新此文件**，请保持内容与代码同步。在每次改动仓库内容后应注意检查是否需要更新本文档，同时在开发过程中发现本文档过时部分也请积极更新。

## 项目简介

- **名称**：杀戮星塔 - SlayTheStella
- **类型**：`Slay the Spire 2`（杀戮尖塔2）的 C# mod。 是游戏《星塔旅人》(Stella Sora/ステラソラ)的同人mod。

## 技术栈与依赖

- **引擎**：Godot 4.5.1 (Mono/C#)，`rendering_method=mobile`（见 `project.godot`）
- **语言**：C# 13 / .NET 9（`net9.0`，`LangVersion=13.0`，`Nullable=enable`）
- **基础库**：`STS2.RitsuLib` 0.5.15（NuGet 包），基于 RitsuLib 脚手架 + 自动注册特性（AutoRegistration）开发
- **游戏引用**：`sts2.dll`、`0Harmony.dll`（引用游戏本体安装目录，`Private=false` 不打包）

## 目录结构

```
slay-the-stella/
├── Scripts/                  # 主要 C# 源码
│   ├── Entry.cs              # mod 入口（[ModInitializer]）
│   ├── MoWang/               # 角色「魔王」
│   │   ├── MoWang.cs         # [RegisterCharacter] 角色定义（配色、性别、初始 HP/金币、攻击特效）
│   │   ├── MoWangCardPool.cs / MoWangRelicPool.cs / MoWangPotionPool.cs  # 卡池/遗物池/药水池
│   │   ├── Cards/            # 卡牌
│   │   ├── Relics/           # 遗物
│   │   ├── Potions/          # 药水
│   │   └── Models/           # 抽象基类：MwCardModel、MwRelicModel、MwPotionModel
│   ├── Shared/               # 共享逻辑（如 SecondaryRes/ 次级资源注册）
│   ├── UI/                   # Godot 节点脚本（.cs，与 SlayTheStella/ui/ 下场景同名对应）
│   └── Utils/                # 工具类：常量/日志/本地化/设置页/音频/数据存取
├── SlayTheStella/            # Godot 资源目录（PCK 打包内容）
│   ├── ui/                   # Godot 场景（.tscn），配套脚本在 Scripts/UI/
│   └── localization/         # 本地化 JSON（zhs 简中 / eng 英文 / jpn 日文：cards/potions/relics/characters + settings）
│                             # （其余资源目录 images/、audios/、mod_image.png 等由用户手动维护）
├── FMod/                     # FMOD Studio 音频工程（项目中使用的音频中间件工程）
├── Tools/                    # 开发辅助脚本（本地化内容一致性检查、术语表索引生成等）
├── _manual/                  # 参考资料
│   ├── SlayTheSpire2ModdingTutorials/  # git submodule：官方中文 mod 教程（Basics/BaseLib）
│   ├── STS2-RitsuLib/        # git submodule：RitsuLib 库源码（本项目依赖的 STS2.RitsuLib NuGet 包源码，查特性/脚手架实现）
│   ├── sts2_export/          # sts2 反编译源码（本地参考，已被 .gitignore 忽略；含 sts2.sln 与反编译出的 C# 源码，localization/ 下有 17 语言官方本地化 JSON）
│   ├── sts2-official-effects-survey.md # STS2 官方效果实现参考手册（agent 实现效果的首选查典：钩子总表/命令 API/模式归档/术语对照）
│   ├── sts2-effect-dev-tutorial.md     # 效果实现教程（面向人类新手的课堂教学版，agent 无需读取）
│   └── stella-sora-glossary.md         # 《星塔旅人》术语表
├── project.godot             # Godot 项目配置
├── SlayTheStella.csproj      # 项目文件（含自动复制 mod 到游戏目录的 Target）
├── SlayTheStella.json        # mod 清单（id/名称/依赖 RitsuLib/has_dll/has_pck）
├── SlayTheStella.sln         # 解决方案（Debug / ExportDebug / ExportRelease）
├── README.md                # 项目说明文档（GitHub README，图标 SlayTheStella/mod_image.png）
└── export_presets.cfg        # Godot 导出预设
```

## 构建与部署

- 项目使用Rider IDE的Publish功能进行构建。
- 禁止在开发时直接运行构建命令，需要进行构建测试等工作时交由用户执行。

## 约定与模式

- **注册方式**：统一用 RitsuLib 的 AutoRegistration 特性，如 `[RegisterCharacter]`、`[RegisterCard]`、`[RegisterCharacterStarterCard]`、`[RegisterRelic]`、`[RegisterCharacterStarterRelic]`、`[RegisterPotion]`
- **角色**：继承 `ModCharacterTemplate<CardPool, RelicPool, PotionPool>`，卡牌/遗物/药水继承 `Models/` 下的抽象基类（基类已带 `[Register...]`，子类自动继承）
- **卡牌效果**：重写 `OnPlay`（异步，用 `DamageCmd.Attack(...)` 等命令）+ `OnUpgrade`；数值用 `DynamicVar`（如 `DamageVar`）驱动，描述文本引用 `{Damage:diff()}` 动态变量
- **本地化**：键格式 `SLAY_THE_STELLA_CARD_{TYPE}.title/.description`、`SLAY_THE_STELLA_RELIC_{NAME}.*`、`SLAY_THE_STELLA_POTION_{NAME}.*`、`SLAY_THE_STELLA_CHARACTER_MO_WANG.*`，目录按语言分 `localization/{zhs|eng|jpn}/` + `localization/settings/{lang}.json`，默认语言 zhs，JSON 统一无 BOM UTF-8
- **资源路径**：统一通过 `StsConsts.ResPath`（`res://SlayTheStella/`）引用
- **卡牌立绘**：放 `SlayTheStella/images/cards/{类名}.png` 即可被 `AssetProfile` 自动找到
- **UI 组织**：Godot 场景放 `SlayTheStella/ui/`（随 PCK 打包），配套节点脚本放 `Scripts/UI/`（编译进 DLL）；场景与脚本同名（如 `NoteUI.tscn` ↔ `NoteUI.cs`），脚本命名空间 `SlayTheStella.Scripts.UI`。分界线是"进 PCK 的资源 vs 进 DLL 的代码"，`.cs` 放 `Scripts/` 下哪个子目录不影响运行时形态
- **战斗内自定义 UI**：需挂到 `NCombatUi` 的自定义面板用 RitsuLib 次级资源注册表 `RegisterCombatUi(localId, factory, update, changed, options)` 注册（次级资源相关内容集中在 `Scripts/Shared/SecondaryRes/ResNotes.cs`，其 `Register()` 由 `Entry.Init` 调用）；factory 用 `GD.Load<PackedScene>` + `Instantiate<T>` 从 `SlayTheStella/ui/` 场景创建，节点提供 `Bind(Player?)`（战斗进入/状态变化时调用）与变化刷新回调
- **内容一致性检查**：`Tools/Check-ContentConsistency.ps1`（纯 PowerShell，不编译 C#，CI 可跑）校验 JSON 无 BOM/可解析、zhs/eng/jpn 键集合与文件集合一致、代码注册类 ↔ zhs 本地化键双向同步。运行 `pwsh ./Tools/Check-ContentConsistency.ps1`；`-Strict` 把 warn 也当失败（CI 门禁用）；新增卡/遗物/药水/角色时忘补本地化会被 [error] 拦下

## 资料检索（搜索优先级）

- **优先查本地**，按顺序：
  1. `_manual/SlayTheSpire2ModdingTutorials/`（官方中文 mod 教程 git submodule，含 Basics/BaseLib/RitsuLib/Visuals 等）
  2. `_manual/STS2-RitsuLib/`（RitsuLib 库源码 git submodule，查 AutoRegistration 特性、`ModCharacterTemplate`、`DamageCmd` 等库内实现）
  3. `_manual/sts2_export/`（sts2 反编译源码，查游戏本体 API/类型签名；本地目录，已被 .gitignore 忽略；`localization/` 下有官方三语本地化 JSON，查术语译名直接 grep）
  4. `_manual/sts2-official-effects-survey.md`（官方效果实现参考手册：实现效果时**首选查这里**，含钩子总表/命令 API/效果模式归档/术语对照；`sts2-effect-dev-tutorial.md` 是配套的人类新手教学版，agent 无需读取）
  能查到就不必联网
- **游戏术语表按索引读**：查《星塔旅人》三语术语（角色/秘纹/音符/物品/阵营等）用 `_manual/stella-sora-glossary.md`；该文件开头有「目录索引（行号导航）」，先读前100行，用索引定位小节行区间，再读所需小节，不要整文件读取；查单个词条也可直接 `grep -F "词条"` 命中整行表格拿三语对照。术语表正文增删行后运行 `pwsh ./Tools/Update-GlossaryIndex.ps1` 。
- **本地不足再联网**：使用`web_search`等在线搜索工具执行联网搜索。
