# STS2 官方卡牌 / 遗物 / 药水 / Power 效果实现参考

> 状态：✅ 完成（2026-09-11）——第 1~9 章及附录 A/B/C 全部填充；统计全量 + 抽样精读 + 四维长尾分诊兜底，引用类名均经源码核实
> 资料源：`_manual/sts2_export/src/Core`（反编译源码，版本以本地导出为准）。
> 所有代码摘录均为官方实现原文（仅去除编译器噪音与无关行）。
> **组织原则**：按「实现模式」归并整理——相同/相似效果共用一个模式条目和一个代表实现，
> 其余卡牌/药水/遗物仅以名字归档在对应模式下；供 Mod 开发时按「想实现的效果」检索，不做逐张图鉴。

## 1. 总览

官方所有卡牌/遗物/药水/Power 的效果都构建在同一个三层结构上：

- **模型层（Models）**：每张卡/遗物/药水/Power 是一个 `AbstractModel` 子类，用**声明式属性**定义自身（`CanonicalVars` 数值、`Type`/`Rarity`/`TargetType` 等枚举、`CanonicalKeywords` 关键词），并**重写自己关心的时机钩子**；
- **命令层（Commands）**：效果执行一律调用 `DamageCmd` / `PowerCmd` / `CreatureCmd` / `CardPileCmd` 等 20 个静态命令类，绝不直接改状态——命令内部统一完成「钩子分发 → 数值修正管道 → 战斗历史 → VFX/SFX/等待」；
- **钩子层（Hooks）**：`AbstractModel` 上的 182 个可重写点（101 通知钩子 + 39 修改器 + 14 Try + 28 Should）由 `Hooks/Hook.cs` 静态分发器按监听者列表统一触发，卡牌/遗物/药水/Power 走**同一套机制**。

→ 写一个效果 = **声明数值（模型层）+ 在正确时机调正确命令（命令层）+ 需要被动时重写钩子（钩子层）**。

**「我想做 XX 效果 → 该看哪章」导读表**：

| 想做的效果 | 查阅 |
|---|---|
| 给卡牌声明数值并联动描述文本 | 第 4 章（4.3 有 Bash 完整范本） |
| 卡牌造成伤害（单体/多段/全体/随机/无力化） | 3.1（管道与构建器表）+ 5.3 |
| 获得格挡 | 3.1 格挡管道 + 5.4 |
| 上 buff / debuff | 3.2 + 5.5；具体 Power 机制查 6.3/6.4 速查表 |
| 修改伤害/格挡/费用/抽牌数等全局数值 | 2.2.8 修改器清单 + 6.2（Power 实现者矩阵） |
| 在某时点触发效果 | 2.2 钩子总表（含分发机制与 Early/Late 规则） |
| 抽/弃/能量/金币/辉星等资源 | 3.3/3.4 API + 5.6 模式 |
| 生成卡牌 / 变形 / 选牌界面 | 5.8-S1 + 3.3 CardSelectCmd |
| 召唤 Osty / 铸造 / 充能球 / 辉星 | 5.8 S2~S5 |
| 状态卡 / 诅咒卡 / 任务卡 | 5.7-E4、5.8 S6/S7 |
| 做新药水 | 7.1 标准骨架 + 7.2 分组表（65 瓶全归并） |
| 做新遗物 | 8.1 属性开关 + 8.2 选钩子 + 8.3 范式 |
| 死亡保护 / 复活 / 即死 | 7.3 FairyInABottle + 2.2.10 Should* |
| 落地到本项目（RitsuLib 注册、资源、本地化） | 第 9 章 |

## 2. 基础架构：模型体系与钩子系统

### 2.1 模型继承体系

**继承树**（源码位于 `_manual/sts2_export/src/Core/Models/`，命名空间 `MegaCrit.Sts2.Core.Models`）：

```
AbstractModel (abstract, 2475 行)
│   ← 一切模型之根：101 个通知钩子(virtual Task) + 39 个 Modify* 修改器 + 14 个 TryModify* + 28 个 Should* 谓词
├── CardModel (2295 行)     ← 卡牌（596 张官方卡）；专属生命周期：OnPlay / OnUpgrade / OnTurnEndInHand / OnEnqueuePlayVfx / IsPlayable 等
├── PotionModel (401 行)    ← 药水（65 瓶）；专属生命周期：OnUse (+ PassesCustomUsabilityCheck)
├── RelicModel (590 行)     ← 遗物（300 件）；专属生命周期：AfterObtained / AfterRemoved (+ 属性开关)
├── PowerModel (650 行)     ← buff/debuff（268 个）；专属生命周期：BeforeApplied / AfterApplied / AfterRemoved (+ Type/StackType/AllowNegative)
├── MonsterModel            ← 怪物
├── OrbModel                ← 充能球
├── EnchantmentModel        ← 卡牌附魔
├── AfflictionModel         ← 卡牌畸变（Affliction）
├── CharacterModel          ← 可玩角色
├── EventModel → AncientEventModel  ← 事件 / 古人事件
├── EncounterModel          ← 遭遇
├── ModifierModel           ← 周目修饰符
├── SingletonModel / AchievementModel / BadgeModel / ActModel
└── CardPoolModel / PotionPoolModel / RelicPoolModel (IPoolModel)  ← 卡/药水/遗物池
```

**关键机制（阅读所有官方效果前必须理解的四件事）**：

1. **注册与查重**：模型由 `ModelDb` 按 `ModelId` 全局注册；构造器内查重，同 ID 注册两个类型直接抛 `DuplicateModelException`（mod 内容命名冲突会在这里爆）。
2. **Canonical / Mutable 双态**：`ModelDb` 里注册的是**原型（canonical）**实例；进游戏时经 `ToMutable()` 克隆出**可变（mutable）**实例供战斗读写，`AssertMutable()`/`AssertCanonical()` 在所有写入口把关，`CanonicalInstance` 属性回查原型。克隆管线是 `MemberwiseClone` + `DeepCloneFields()`（子类深克隆引用字段）+ `AfterCloned()`（清事件订阅）。
3. **钩子接收范围 `ShouldReceiveCombatHooks`**（abstract 属性）：决定该模型是否进入钩子监听列表——

   | 模型 | 返回值 | 含义 |
   |---|---|---|
   | `CardModel` | `Pile?.IsCombatPile ?? false` | **只有进入战斗牌堆的卡才接收战斗钩子**（牌库里的卡不收 `AfterCardPlayed` 这类，但收 `AfterRoomEntered` 这类全局钩子） |
   | `PotionModel` / `RelicModel` / `PowerModel` / `MonsterModel` / `OrbModel` / `AfflictionModel` / `ModifierModel` / `AchievementModel` / `BadgeModel` | `true` | 恒接收 |
   | `EnchantmentModel` | `Card?.ShouldReceiveCombatHooks ?? false` | 跟随宿主卡 |
   | `CharacterModel` / `ActModel` / `EventModel` / `EncounterModel` / 三个 Pool | `false` | 不接收 |

4. **专属生命周期 vs 通用钩子的分界**：四个内容基类只声明**自己类型特有**的生命周期入口（卡牌打出/升级、药水使用、遗物获得/移除、Power 施加/移除）；**其余一切"在某时点被触发"的效果一律复用 `AbstractModel` 的通用钩子**——遗物的触发时机、Power 的被动、卡牌的手牌触发走同一套机制。注意：卡牌**没有** `OnDiscard`/`OnExhaust`/`OnRetain` 专属虚方法（容易想当然），弃牌/消耗走 `AfterCardDiscarded`/`AfterCardExhausted` 钩子，保留（Retain）是关键词 + `ShouldRetainThisTurn` 状态位。

### 2.2 钩子（触发时机）总表

#### 2.2.0 分发机制：`Hooks/Hook.cs` 静态分发器

子类只管 `override` 钩子方法；真正调用它们的是 **`Hooks/Hook.cs`（2568 行静态类）**：每个钩子对应一个 `Hook.Xxx(状态, 参数…)` 分发方法，内部遍历监听者逐个 `await model.Xxx(…)` 并触发 `InvokeExecutionFinished()`（供 UI 监听）。要点：

- **监听者来源**：战斗钩子遍历 `combatState.IterateHookListeners()`（外面包一层 `IterateCombatHookListeners` 守卫：战斗已结束/正在结束时本次分发静默取消；少数属于死亡/战斗结束序列本身的钩子——`AfterCardPlayed`、`AfterBlockBroken`、`AfterDamageGiven`、`AfterDeath`、`AfterCreatureAddedToCombat` 等——绕过守卫直接分发，保证击杀它的那张卡能结算完）。局外钩子遍历 `runState.IterateHookListeners(combatState)`（战斗内外都发，`combatState` 为 null 时只发给局外监听者）。
- **Early / 普通版 / Late 成对的钩子**（`AfterCardDrawnEarly`+`AfterCardDrawn`、`AfterCardPlayed`+`AfterCardPlayedLate`、回合开始/结束的 5~6 段版本等）= 分发器对监听者列表**跑两/三轮**：先全体 Early，再全体普通版，再全体 Late。官方注释反复警告：除非明确需要先后序，**默认用普通版**。
- **玩家选择**：可能触发玩家选择的钩子（`AfterCardPlayed`、`AfterFlush`、`BeforeSideTurnEnd`、`AfterDeath` 等）以 `PlayerChoiceContext.PushModel(model)/PopModel(model)` 标记当前监听者，需要时会暂停战斗流程等玩家选择。
- **修改器聚合**（Modify*）：顺序为 **附魔加算 → 附魔乘算 → 全体监听者 Additive（累加）→ 全体监听者 Multiplicative（累乘）→ clamp ≥ 0**；实际改了值的监听者记入 `modifiers` 集合，分发结束后逐个回调对应的 `AfterModifyingXxx` 通知钩子（这就是通知型 `AfterModifying*` 只发给"真凶"的原因）。
- **Try\* 链**：`bool Try + out` 形式，按监听者顺序链式传递 `out` 结果，后者看到前者改过的值；`Late` 版第二轮跑。典型：`ModifyEnergyCostInCombat`（原费用 < 0 直接跳过）、`ModifyCardBeingAddedToDeck`（可换卡）。
- **Should\* 谓词**：遍历监听者询问 bool，**短路返回**，多数带 `out AbstractModel? preventer` 报告是谁阻止的（UI 提示用）。
- **两个 flags 枚举控制伤害/HP 修正的阶段**（`Hooks/ModifyDamageHookType.cs`、`Hooks/HpLossHookPhase.cs`）：
  - `ModifyDamageHookType`：`Additive`(2) / `Multiplicative`(4) / `Cap`(8) / `All`——本次伤害结算跑哪几段修改器（如预览与实际结算口径不同）。对应实现者：Additive=力量类、Multiplicative=易伤/虚弱类、Cap=无实体类。
  - `HpLossHookPhase`：`BeforeOsty` / `AfterOsty`——HP 流失修正的两个阶段，中间夹着 **Necrobinder→Osty 的伤害重定向**（目前唯一重定向来源是 `DieForYouPower`）。

> 下表「典型使用者」一列为重写了该钩子的官方内容各一例（类名，分别位于 `Models/Cards|Relics|Powers/`）；「—」表示官方卡/遗物/Power 无重写者（多为事件/修饰符等局外模型在使用）。`choiceContext` 参数已省略。

**钩子三家族，各一个最小官方示例**（源码：`Models/Relics/Akabeko.cs`、`Models/Powers/StrengthPower.cs`、`Models/Powers/BarricadePower.cs`，仅去噪音压缩）：

```csharp
// ① 通知型钩子（遗物 Akabeko）：重写时机钩子，首回合给玩家 8 层 Vigor
public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
{
    if (participants.Contains(base.Owner.Creature) && base.Owner.PlayerCombatState.TurnNumber <= 1)
    {
        Flash();
        await PowerCmd.Apply<VigorPower>(new ThrowingPlayerChoiceContext(), base.Owner.Creature,
            base.DynamicVars["VigorPower"].IntValue, base.Owner.Creature, null);
    }
}

// ② 数值修改器（Power StrengthPower）：不关心时机，只回答"这次攻击伤害加多少"
public sealed class StrengthPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => true;                  // 可被减益扣到负数

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (base.Owner != dealer) return 0m;        // 只加成宿主自己打出的伤害
        if (!props.IsPoweredAttack()) return 0m;    // Unpowered（无力化）伤害不吃加成
        return base.Amount;                          // 每击 += 层数
    }
}

// ③ Should 谓词（Power Barricade 壁垒）：回答"回合开始还允许清它的格挡吗"
public override bool ShouldClearBlock(Creature creature)
{
    if (base.Owner != creature) return true;
    return false;                                    // 挂着壁垒的生物：格挡不清空
}
```

#### 2.2.1 通知型钩子：战斗流程

| 钩子 | 触发时机 | 典型使用者（卡/遗物/Power） |
|---|---|---|
| `BeforeCombatStart()` | 战斗开始前；**对非战斗模型也生效**（如牌库中"第 3 场战斗变形"的卡） | — / `Anchor` / `GalvanicPower` |
| `BeforeCombatStartLate()` | 同上，普通版之后 | — / `PetrifiedToad` / — |
| `AfterCombatEnd(room)` | 战斗结束（**含失败**） | `Guilty` / `BrilliantScarf` / `ForbiddenGrimoirePower` |
| `AfterCombatVictoryEarly(room)` / `AfterCombatVictory(room)` | 战斗胜利后（两段） | — / `MeatOnTheBone`、`BeltBuckle` / — |
| `BeforeCombatRewardOffered(rewards, room)` | 战斗奖励展示前（可改奖励） | — / `LastingCandy` / — |
| `AfterCreatureAddedToCombat(creature)` | 新生物加入战斗（含战斗中召唤/增援） | — / `FurCoat` / `SandpitPower` |
| `AfterTakingExtraTurn(player)` | 玩家获得额外回合后 | — / `PaelsEye` / `AmbergrisPower` |
| `AfterAutoPrePlayPhaseEntered{Early,}`(player) / `...Late(player)` | 回合开始**自动出牌阶段**（Mayhem 类效果在此打牌） | `Bombardment` / `HistoryCourse`、`WhisperingEarring` / `MayhemPower` |
| `AfterAutoPostPlayPhaseEntered(player)` | 回合结束自动出牌阶段 | `HowlFromBeyond` / — / `StampedePower` |

#### 2.2.2 通知型钩子：回合流程（一段回合开始/结束被切成多段，注意选段）

| 钩子 | 触发时机 | 典型使用者（卡/遗物/Power） |
|---|---|---|
| `BeforeSideTurnStart(side, participants, combatState)` | 一方回合开始**之前**（能量重置、抽牌都还没发生） | — / `BeatingRemnant` / `AggressionPower` |
| `AfterSideTurnStart(side, participants, combatState)` | 一方回合开始后；**首回合生效的效果**（打伤害/加格挡）在此配 `RoundNumber==1` 判断 | — / `Akabeko` / `BlurPower` |
| `AfterSideTurnStartLate(...)` | 同上其后 | — / — / `SandpitPower` |
| `AfterPlayerTurnStart{Early,}`(player) / `...Late(player)` | 玩家回合开始（三段）；**需要弹玩家选择的用这一组**（与 `AfterSideTurnStart` 的区别，官方注释：避免与 Mayhem/Stratagem 类抽打效果互相干扰） | — / `Bellows`、`BloodVial` / `CrimsonMantlePower` |
| `BeforeSideTurnEnd{VeryEarly,Early,}`(side, participants) | 一方回合结束前（三段）；**对敌方造成伤害的回合结束效果必须放这组**（The Bomb 类），`AfterSideTurnEnd` 只允许自伤类 | `Regret` / `FakeOrichalcum`、`PaelsEye` / `ChainsOfBindingPower`、`PlatingPower` |
| `AfterSideTurnEnd(...)` / `...Late(...)` | 一方回合结束后（自伤型回合结束效果） | — / `ArtOfWar` / `AsleepPower`、`DisintegrationPower` |

#### 2.2.3 通知型钩子：卡牌相关

| 钩子 | 触发时机 | 典型使用者（卡/遗物/Power） |
|---|---|---|
| `BeforeCardPlayed(cardPlay)` | 卡被打出前 | `Stomp` / `ChemicalX` / `AfterimagePower` |
| `AfterCardPlayed(cardPlay)` / `...Late` | 卡结算完毕后（多段 Replays 全部结束算一次） | `BansheesCry` / `ArtOfWar` / `AfterimagePower`、`MakeItSo` |
| `BeforeCardAutoPlayed(card, target, type)` | 卡被其他效果**自动打出**前 | — / — / —（官方内容未用） |
| `AfterCardDiscarded(card)` | 卡被弃置后 | — / `Tingsha` / — |
| `AfterCardDrawn{Early,}`(card, fromHandDraw) | 卡被抽到后（两段；`fromHandDraw`=回合开始的起手抽牌） | `KinglyPunch` / `HellraiserPower`(Power) / `AutomationPower` |
| `AfterCardChangedPiles(card, oldPileType, clonedBy)` / `...Late` | 卡在牌堆间移动（新牌堆看 `card.Pile`；`clonedBy` 防复制效果递归） | `SovereignBlade` / `BingBong` / —（仅 Mock） |
| `AfterCardEnteredCombat(card)` | 卡进入战斗牌堆（**含敌人塞给你的状态/诅咒**） | `BansheesCry` / `GhostSeed` / `HexPower` |
| `AfterCardGeneratedForCombat(card, creator)` | 卡进入战斗牌堆且**是玩家自己生成的**（`creator` 为 null 表示敌人塞的） | `RocketPunch` / `Regalite` / `ArsenalPower` |
| `AfterCardExhausted(card, causedByEthereal)` | 卡被消耗后（`causedByEthereal`=虚无导致） | `DrumOfBattle` / `BurningSticks` / `DarkEmbracePower` |
| `BeforeCardRemoved(card)` | 卡从牌组移除前（删卡事件） | `SpoilsMap` / — / — |
| `AfterAddToDeckPrevented(card)` | 有模型（`ShouldAddToDeck`）阻止了加卡后 | — / — / — |
| `AfterShuffle(shuffler)` | 抽牌堆洗牌后 | — / `BiiigHug` / `StratagemPower` |
| `BeforeHandDraw(player, combatState)` / `...Late` | 回合开始起手抽牌前（两段） | `Bolas` / `BlessedAntler` / `CallOfTheVoidPower` |
| `BeforeFlush(player)` / `...Late` | 回合结束弃手牌前（两段） | — / — / —（仅 Mock） |
| `AfterFlush(player, flushedCards, retainedCards)` | 弃手牌后（能拿到**被保留**的卡列表） | — / `Bookmark` / — |
| `AfterHandEmptied(player)` | 手牌变为空后 | — / `UnceasingTop` / — |

#### 2.2.4 通知型钩子：攻击 / 格挡 / 伤害 / HP / 死亡

| 钩子 | 触发时机 | 典型使用者（卡/遗物/Power） |
|---|---|---|
| `BeforeAttack(command)` / `AfterAttack(command)` | 生物发起攻击前/后；**多段攻击整体只算一次**（区别于每击都触发的 `AfterDamageGiven`） | `Flatten` / `BoneFlute` / `GigantificationPower` |
| `BeforeBlockGained(creature, amount, props, cardSource)` / `AfterBlockGained(...)` | 获得格挡前/后 | — / — / `BeaconOfHopePower` |
| `AfterBlockCleared(creature)` | 回合开始格挡被清除后 | — / `CaptainsWheel` / `BlockNextTurnPower` |
| `AfterBlockBroken(target, breaker)` | 格挡被打碎（**非伤害也能触发**，如 Expose；比在 `AfterDamageReceived` 里查 `WasBlockBroken` 可靠） | — / `HandDrill` / `BurrowedPower` |
| `BeforeDamageReceived(target, amount, props, dealer, cardSource)` | 受到伤害前（即使修正后为 0 也触发；荆棘反伤在这层） | — / — / `ThornsPower` |
| `AfterDamageReceived(target, result, props, …)` / `...Late` | 受到伤害后（两段；即使被格挡归零也触发） | — / `BeatingRemnant` / `AsleepPower` |
| `AfterDamageGiven(dealer, result, props, target, …)` | 造成伤害后（每击一次） | — / — / `ConcoctPower` |
| `AfterCurrentHpChanged(creature, delta)` | HP 因**任何原因**变化（负=受伤/失去，正=治疗） | — / `MeatOnTheBone` / `NecroMasteryPower` |
| `BeforeDeath(creature)` | 死亡前（**即使将被复活效果阻止也触发**） | — / — / `HeistPower` |
| `AfterDeath(creature, wasRemovalPrevented, deathAnimLength)` | 死亡后（`wasRemovalPrevented`=复活类效果阻止了移除） | `Melancholy` / `GremlinHorn` / `AdaptablePower` |
| `AfterDiedToDoom(creatures)` | 死于 Doom 结算后 | — / `BookRepairKnife` / — |
| `AfterPreventingDeath(creature)` | **本模型**成功阻止了一次死亡后（复活者回调自己） | — / `LizardTail` / —（仅 Mock） |
| `AfterPreventingBlockClear(preventer, creature)` / `AfterPreventingDraw()` | 有模型阻止了格挡清除 / 抽牌后 | — / `SturdyClamp`、`Fiddle` / `BlurPower` |

#### 2.2.5 通知型钩子：能量 / 资源 / 药水 / Power

| 钩子 | 触发时机 | 典型使用者（卡/遗物/Power） |
|---|---|---|
| `AfterEnergyReset(player)` / `...Late` | 回合开始能量重置后（两段） | — / `ArtOfWar`、`BoundPhylactery` / `EnergyNextTurnPower` |
| `AfterEnergySpent(card, amount)` | 花费能量后 | — / — / `OrbitPower` |
| `AfterStarsGained(amount, gainer)` / `AfterStarsSpent(amount, spender)` | 辉星获得/花费后 | — / `GalacticDust` / `BlackHolePower`、`ChildOfTheStarsPower` |
| `AfterForge(amount, forger, source)` | 铸造触发后（Sovereign Blade +N） | — / — / `HammerTimePower` |
| `AfterSummon(summoner, amount)` | 召唤结算后 | — / — / — |
| `AfterOstyRevived(osty)` | Osty 复活后 | — / — / `SandpitPower` |
| `AfterOrbChanneled(player, orb)` / `AfterOrbEvoked(orb, targets)` | 生成充能球 / 被激发后 | — / `Metronome` / `ThunderPower` |
| `BeforePotionUsed(potion, target)` / `AfterPotionUsed(...)` | 药水使用前/后（**战斗外使用时只通知遗物**） | — / `BeltBuckle` / `SurroundedPower` |
| `AfterPotionDiscarded(potion)` / `AfterPotionProcured(potion)` | 药水被丢弃 / 被获得后 | — / `BeltBuckle` / — |
| `BeforePowerAmountChanged(power, amount, target, applier, cardSource)` / `AfterPowerAmountChanged(...)` | 任一 Power 层数将变/已变（新增或叠加都算） | — / `UnsettlingLamp` / `VoidFormPower`、`PossessSpeedPower` |

#### 2.2.6 通知型钩子：局外流程（金钱 / 商店 / 奖励 / 休息 / 地图 / 房间）

| 钩子 | 触发时机 | 典型使用者（卡/遗物/Power） |
|---|---|---|
| `AfterGoldGained(player)` | 获得金币后 | — / `DragonFruit` / — |
| `AfterItemPurchased(player, item, goldSpent)` | 商店购买后 | — / `MawBank` / — |
| `AfterRewardTaken(player, reward)` | 领取一个奖励后 | — / — / — |
| `AfterRestSiteHeal(player, isMimicked)` / `AfterRestSiteSmith(player)` | 休息点休息/铸造后（`isMimicked`=事件模拟的休息，`RegalPillow` 认、`NightTerrors` 不认） | — / `RegalPillow` / — |
| `AfterActEntered()` | 进入新幕 | — / — / —（事件/修饰符用） |
| `BeforeRoomEntered(room)` / `AfterRoomEntered(room)` | 进房间前/后；**战斗开始上 buff 的官方推荐位置**（`AfterRoomEntered` + `room is CombatRoom` 判断，先于第一回合） | `Dowsing` / `BigMushroom` / — |
| `AfterMapGenerated(map, actIndex)` | 幕地图生成后 | `SpoilsMap` / — / — |

#### 2.2.7 通知型钩子：`AfterModifyingXxx`（修改器生效通知，只发给真改了值的模型）

与 2.2.0 的修改器聚合机制配对。一律只通知实际贡献了修改的监听者，适合"每当你的 X 被修正就 Y"类效果。

| 钩子 | 对应修改器 | 典型使用者（遗物/Power） |
|---|---|---|
| `AfterModifyingBlockAmount(modifiedAmount, cardSource, cardPlay)` | `ModifyBlock*` | `PaelsLegion` / `FastenPower` |
| `AfterModifyingDamageAmount(cardSource)` | `ModifyDamage*` | — / `SlowPower` |
| `AfterModifyingCardPlayCount(card)` | `ModifyCardPlayCount` | `ThrowingAxe` / `BurstPower` |
| `AfterModifyingCardPlayResultLocation(card, location)` | `ModifyCardPlayResultLocation` | — / `CorruptionPower` |
| `AfterModifyingHandDraw()` / `AfterPreventingDraw()` | `ModifyHandDraw*` / `ShouldDraw` | `PollinousCore`、`Fiddle` / `MindRotPower` |
| `AfterModifyingEnergyGain()` | `ModifyEnergyGain` | — / `NoEnergyGainPower` |
| `AfterModifyingGoldGained(player, amount)` | `ModifyGoldGained` | `BowlerHat` / — |
| `AfterModifyingHpLostBeforeOsty()` / `AfterOsty()` | `ModifyHpLost*` | `BeatingRemnant` / `HardenedShellPower`、`IntangiblePower` |
| `AfterModifyingPowerAmountReceived(power)` / `...Given(power)` | `TryModifyPowerAmountReceived` / `ModifyPowerAmountGiven*` | `RuinedHelmet`、`SneckoSkull` / `ArtifactPower` |
| `AfterModifyingOrbPassiveTriggerCount(orb)` | `ModifyOrbPassiveTriggerCounts` | `GoldPlatedCables` / — |
| `AfterModifyingCardRewardOptions()` / `AfterModifyingRewards()` | `ModifyCardReward*` / `TryModifyRewards*` | `SilverCrucible`、`AmethystAubergine` / — |

#### 2.2.8 数值修改器（Modify*，共 39 个：改"数值会变成多少"）

按语义分组（签名均省略修饰符；`props` 为 `ValueProp` 数值属性包，`cardPlay` 仅实际打出时非 null——预览时只有 `cardSource`）：

**伤害与攻击**
| 修改器 | 返回/语义 | 官方注释点名的例子 |
|---|---|---|
| `ModifyDamageAdditive(target, amount, props, dealer, cardSource, cardPlay)` | 返回**加值**（默认 0） | `StrengthPower`、`VigorPower` |
| `ModifyDamageMultiplicative(...)` | 返回**乘数**（默认 1） | `VulnerablePower`、`WeakPower` |
| `ModifyDamageCap(...)` | 返回单次伤害上限（默认无限） | `IntangiblePower` |
| `ModifyAttackHitCount(attack, hitCount)` | 返回新段数 | — |
| `ModifyUnblockedDamageTarget(target, …)` | 返回接替承受未格挡伤害的生物（伤害转移） | — |

**格挡**
| 修改器 | 返回/语义 | 官方注释点名的例子 |
|---|---|---|
| `ModifyBlockAdditive(target, block, props, cardSource, cardPlay)` | 返回加值（默认 0） | `DexterityPower`、`Fasten` |
| `ModifyBlockMultiplicative(...)` | 返回乘数（默认 1） | `FrailPower`、`Unmovable` |

**出牌行为**
| 修改器 | 返回/语义 |
|---|---|
| `ModifyCardPlayCount(card, target, playCount)` | 返回本卡应被打出的次数（"下一张卡打出两次"） |
| `ModifyCardPlayResultLocation(card, isAutoPlay, resources, cardLocation)` | 返回打完后的去向（弃/消耗/移除/给队友） |
| `ModifyXValue(card, originalValue)` | 返回 X 费卡的 X 值（如 `ChemicalX`） |
| `ModifyOrbPassiveTriggerCounts(orb, triggerCount)` / `ModifyOrbValue(orb, value)` | 充能球被动触发次数 / 充能球数值 |

**回合资源**
| 修改器 | 返回/语义 |
|---|---|
| `ModifyHandDraw(player, count)` / `...Late` | 回合开始抽牌数 |
| `ModifyEnergyGain(player, amount)` / `ModifyMaxEnergy(player, amount)` | 能量获取 / 能量上限 |
| `ModifyGoldGained(player, amount)` | 金币获取 |
| `ModifyHpLostBeforeOsty{Late,}` / `ModifyHpLostAfterOsty{Late,}`(target, amount, props, dealer, cardSource) | HP 流失修正，四个时段夹住 Osty 重定向（见 2.2.0） |
| `ModifySummonAmount(summoner, amount, source)` | 召唤量（`source`=召唤来源模型，如 `Bodyguard`、`BoundPhylactery`） |
| `ModifyRestSiteHealAmount(creature, amount)` / `ModifyExtraRestSiteHealText(player, currentExtraText)` | 休息点回复量 / 改写回复时显示的追加文本 |
| `ModifyShuffleOrder(player, cards, isInitialShuffle)` | **原地改写**洗牌顺序（`isInitialShuffle`=战斗开始洗牌） |

**Power 施加量**
| 修改器 | 返回/语义 | 官方注释点名的例子 |
|---|---|---|
| `ModifyPowerAmountGivenAdditive(power, giver, amount, target, cardSource)` | 返回加值；先于 Multiplicative | `SneckoSkull` |
| `ModifyPowerAmountGivenMultiplicative(...)` | 返回乘数 | `UnsettlingLamp` |

**局外（奖励 / 商店 / 地图 / 事件）**
| 修改器 | 返回/语义 |
|---|---|
| `ModifyCardRewardCreationOptions(player, options)` / `...Late` | 改卡牌奖励候选池（增删/加权） |
| `ModifyCardRewardUpgradeOdds(player, card, odds)` | 改奖励卡升级概率 |
| `ModifyMerchantCardPool(...)` / `ModifyMerchantCardRarity(...)` / `ModifyMerchantCardCreationResults(...)` / `ModifyMerchantPrice(player, entry, cost)` | 商店货池 / 稀有度 / 生成结果 / 价格 |
| `ModifyRewards` 相关见 TryModifyRewards*；`ModifyGeneratedMap(runState, map, actIndex)` / `...Late` | 替换/标注地图（Late 只许标注不许换实例，联机存档约束） |
| `ModifyNextEvent(currentEvent)` / `ModifyUnknownMapPointRoomTypes(roomTypes)` / `ModifyOddsIncreaseForUnrolledRoomType(...)` | 改下一个事件 / 未知节点可摇出的房间类型 / 未摇中房间的概率增量 |

#### 2.2.9 TryModify* 修改器（14 个：bool + out，链式）

| Try 修改器 | 语义 |
|---|---|
| `TryModifyCardBeingAddedToDeck(card, out newCard)` / `...Late` | 卡将加入牌组时可**替换成另一张卡**（变形类遗物） |
| `TryModifyEnergyCostInCombat(card, originalCost, out modifiedCost)` / `...Late` | 战斗中改卡费用（原费用为负则跳过；**战斗外费用不走这里**） |
| `TryModifyKeywordsInCombat(card, keywords)` | 战斗中给卡增删关键词（全局关键词来源，与卡自身 Local 关键词并存） |
| `TryModifyStarCost(card, originalCost, out modifiedCost)` | 改辉星费用 |
| `TryModifyPowerAmountReceived(power, target, amount, applier, out modifiedAmount)` | Power 落到目标身上前改层数（**人工制品抵消在这层**） |
| `TryModifyCardRewardOptions(player, options, creationOptions)` / `...Late` | 改卡牌奖励的实际选项 |
| `TryModifyCardRewardAlternatives(player, cardReward, alternatives)` | 改卡牌奖励的替代选项（跳过/治疗/献祭等） |
| `TryModifyRewards(player, rewards, room)` / `...Late` | 增删改任意奖励列表 |
| `TryModifyRestSiteOptions(player, options)` / `TryModifyRestSiteHealRewards(player, rewards, isMimicked)` | 改休息点选项 / 休息附带的奖励 |

#### 2.2.10 Should* 谓词（28 个：问答"还允许吗"，短路 + 报告阻止者）

| 分组 | 谓词 | 默认 |
|---|---|---|
| 打牌 | `ShouldPlay(card, autoPlayType)` | true |
| 死亡 | `ShouldDie(creature)` / `...Late`、`ShouldCreatureBeRemovedFromCombatAfterDeath(creature)`、`ShouldPowerBeRemovedOnDeath(power)`（Calcify 类） | true |
| 格挡 | `ShouldClearBlock(creature)`（Barricade 类"不清格挡"的实现层） | true |
| 抽/弃/保留 | `ShouldDraw(player, fromHandDraw)`、`ShouldFlush(player)`、`ShouldEtherealTrigger(card)` | true |
| 牌组 | `ShouldAddToDeck(card)`、`ShouldAfflict(card, affliction)` | true |
| 能量/辉星 | `ShouldPlayerResetEnergy(player)`、`ShouldPayExcessEnergyCostWithStars(player)`、`ShouldGainStars(amount, player)` | true/false/true |
| 目标 | `ShouldAllowHitting(creature)`、`ShouldAllowTargeting(target)`（"打不到/选不中"类，如无人格状态） | true |
| 资源获取 | `ShouldGenerateTreasure(player)`、`ShouldForcePotionReward(player, roomType)`、`ShouldProcurePotion(potion, player)`、`ShouldTakeExtraTurn(player)` | true/… |
| 商店/奖励界面 | `ShouldRefillMerchantEntry(entry, player)`、`ShouldAllowMerchantCardRemoval(player)`、`ShouldAllowSelectingMoreCardRewards(player, reward)` | false/true/false |
| 地图/流程 | `ShouldProceedToNextMapPoint()`、`ShouldAllowFreeTravel()`、`ShouldStopCombatFromEnding()`（清场但有怪要刷时）、`ShouldAllowAncient(player, ancient)`（Wax Choker 类）、`ShouldDisableRemainingRestSiteOptions(player)` | — |

### 2.3 关键枚举

| 枚举 | 值 | 备注 |
|---|---|---|
| `CardType` | None / **Attack / Skill / Power** / Status / Curse / Quest | Status/Curse/None 按 Skill 渲染卡框；Quest=任务卡 |
| `CardRarity` | None / **Basic / Common / Uncommon / Rare** / Ancient / Event / Token / Status / Curse / Quest | Ancient=古卡（专属边框）；Token=衍生代币卡；Basic=初始卡（`IsBasicStrikeOrDefend` 只认 Basic+Strike/Defend 标签） |
| `TargetType` | None / **Self / AnyEnemy / AllEnemies / RandomEnemy** / AnyPlayer / **AnyAlly** / AllAllies / TargetedNoCreature / Osty | 卡牌与药水共用但**校验逻辑不同**：药水 `Self` 也传入目标（`Owner.Creature` 兜底），卡牌 `Self` 不传目标；`TargetedNoCreature`=要选择但无生物目标（药水用）；`AnyAlly` 场上无其他存活队友时卡不可打出（`UnplayableReason.NoLivingAllies`） |
| `PotionRarity` | None / **Common / Uncommon / Rare** / Event / Token | — |
| `PotionUsage` | None / **CombatOnly / AnyTime** / Automatic | `Automatic`=不可主动使用、自动触发（FairyInABottle 类，见 7.3） |
| `RelicRarity` | None / **Starter / Common / Uncommon / Rare / Shop** / Event / Ancient | 商店基准价：Common 175 / Uncommon 225 / Rare 275 / Shop 200（`RelicModel.MerchantCost`），Starter/Event/Ancient 不可售 |
| `PowerType` | None / **Buff / Debuff** | 正负可翻转：`AllowNegative` 的 Counter 层数 <0 由 `PowerModel.GetTypeForAmount` 判为 Debuff；不允许负值的 Debuff 被压到负数反而显示为 Buff |
| `PowerStackType` | None / **Counter / Single** | Counter=可叠层；Single=单实例不叠层。持续回合型衰减（易伤/虚弱每回合 -1）不走 StackType，由 `PowerModel.SkipNextDurationTick` + PowerCmd 侧 DurationTick 机制处理（详见第 6 章） |

> 补充（与钩子系统配套的两个 flags 枚举见 2.2.0）：`ModifyDamageHookType`（Additive/Multiplicative/Cap/All）、`HpLossHookPhase`（BeforeOsty/AfterOsty/All）。

## 3. 指令层 API 速查（Commands）

> 目录 `src/Core/Commands/`，命名空间 `MegaCrit.Sts2.Core.Commands`，共 20 个静态类（约 6100 行）。**官方效果一律通过 Cmd 执行，不直接改状态**——Cmd 内部统一完成：钩子分发（第 2 章）→ 数值修正管道 → 战斗历史记录 → VFX/SFX/等待。模型代码里唯一常见的例外是读数值（如 `DynamicVars.Damage.BaseValue`）。
> 时序基类 `Cmd`：`Wait(seconds)` / `CustomScaledWait(fastSeconds, standardSeconds)` 是全部游戏内等待的标准做法（尊重 Godot timescale、hitpause 与 FastMode 设置，勿用 `Task.Delay`）。

### 3.1 DamageCmd 与 AttackCommand（攻击怎么做）

`DamageCmd`（30 行）只有两个工厂方法，真正的攻击逻辑在链式构建器 `Commands/Builders/AttackCommand.cs`（693 行）：

| DamageCmd | 语义 |
|---|---|
| `DamageCmd.Attack(decimal damagePerHit)` | 每击固定伤害 → 返回 `AttackCommand` |
| `DamageCmd.Attack(CalculatedDamageVar var)` | 每击伤害由动态变量算出（PerfectedStrike 类"基础+每人 X 点"，中间值有专门取整规则） |

**构建器方法表**（链式调用，`Execute` 收尾）：

| 构建器方法 | 作用 |
|---|---|
| `.FromCard(card, cardPlay)` | 攻击来自某卡；自动设攻击者=卡主人、默认攻击动画与延迟 |
| `.FromOsty(osty, card, cardPlay)` | 攻击来自 Osty（自动换 Osty 攻击音效，动画延迟 0.3s） |
| `.FromMonster(monster)` | 攻击来自怪物；**自动** `.TargetingAllOpponents(...)` |
| `.Targeting(target)` | 单体指定目标 |
| `.TargetingAllOpponents(combatState)` | 全体对手；**每一击之间刷新目标列表**（战斗中新加入的怪也会被后续击命中） |
| `.TargetingRandomOpponents(combatState, allowDuplicates=true)` | 随机对手，**每击重新摇目标**（Rng.CombatTargets） |
| `.WithHitCount(n)` | 多段攻击（n 击；实际段数还会被 `ModifyAttackHitCount` 钩子改） |
| `.Unpowered()` | 无力化：本次攻击不吃力量等加成。⚠️官方注释警告：99% 的"无力伤害"不应是攻击（如 Burn 灼伤属于 HP loss），仅 Omnislice 这类"真攻击但无视 Power"用 |
| `.WithAttackerAnim(animName, delay, visualAttacker?)` / `.WithNoAttackerAnim()` | 自定义攻击者动画 / 不播动画（`visualAttacker` 可让宠物代为出演） |
| `.AfterAttackerAnim(Func<Task>)` | 攻击者动画播完后的插入逻辑（一次性效果用） |
| `.WithAttackerFx(vfx?, sfx?, tmpSfx?)` / `.WithAttackerFx(Func<Node2D?>)` | 攻击者身上的 VFX/SFX |
| `.WithHitFx(vfx?, sfx?, tmpSfx?)` | 命中目标时目标身上的 VFX/SFX |
| `.SpawningHitVfxOnEachCreature()` / `.WithHitVfxSpawnedAtBase()` | AOE 命中特效每怪各放一份 / 特效落在脚底而非中心 |
| `.WithHitVfxNode(Func<Creature, Node2D?>)` | 自定义命中 VFX 节点 |
| `.OnlyPlayAnimOnce()` | 多段攻击只播一次攻击动画 |
| `.WithWaitBeforeHit(fast, standard)` | 每击前等待（同 `CustomScaledWait` 签名） |
| `.BeforeDamage(Func<Task>)` | **每击**伤害结算前的插入逻辑 |
| `.Execute(choiceContext?)` | 执行；`Results` 属性在执行后可读每击的 `DamageResult` |
| `AttackCommand.CreateContextAsync(...)` → `AttackContext` | 把多次零散的 `CreatureCmd.Damage` 聚合成"一次攻击"（`await using` 自动触发 `BeforeAttack`/`AfterAttack` 与段数统计） |

**`Execute` 结算流程**：

1. 守卫：战斗已结束/攻击者已死 → 直接返回；未设目标 → 抛异常。
2. `Hook.BeforeAttack`（整个攻击一次，多段不重复）。
3. `attackCount = Hook.ModifyAttackHitCount(...)`（钩子可改段数）。
4. 循环每击：筛活目标 →（首击或每击）攻击者 VFX/SFX/动画 → 命中 SFX → 随机目标摇人 → hit VFX → `BeforeDamage` 回调 → `CreatureCmd.Damage(每击伤害 | CalculatedDamageVar.Calculate(target), ...)`。
5. 写战斗历史 `History.CreatureAttacked` → `Hook.AfterAttack`。

**伤害结算管道**（`CreatureCmd.Damage`，逐目标；这是"力量/易伤/虚弱/无实体分别在哪个环节生效"的答案）：

```
对每个目标：
① Hook.ModifyDamage(..., ModifyDamageHookType.All)
     ├─ 附魔 EnchantDamageAdditive/Multiplicative（最先）
     ├─ Additive 段：ModifyDamageAdditive ← 力量/Vigor（加算，看 dealer 侧 Power）
     ├─ Multiplicative 段：ModifyDamageMultiplicative ← 易伤/虚弱（乘算，看 target 侧 Power）
     └─ Cap 段：ModifyDamageCap ← 无实体（单次伤害上限）        结果 clamp≥0
② Hook.AfterModifyingDamageAmount（只通知真改了值的模型）
③ Hook.BeforeDamageReceived ← 荆棘反伤挂在这层
④ DamageBlockInternal：格挡吸收
⑤ Hook.ModifyHpLost(BeforeOsty 段) + AfterModifyingHpLostBeforeOsty
⑥ Hook.ModifyUnblockedDamageTarget ← 伤害转移（Die For You→Osty）
⑦ Hook.ModifyHpLost(AfterOsty 段) + AfterModifyingHpLostAfterOsty
⑧ LoseHpInternal：真正扣 HP
⑨ 表现层：伤害数字/HitSpark/受击动画/屏幕震动/音效（FullyBlocked 则播 block_hit）
⑩ 逐结果后置钩子：AfterBlockBroken → AfterCurrentHpChanged → AfterDamageGiven → AfterDamageReceived
   （被击杀的目标延后收集，循环结束后统一 Kill(...) → 死亡序列 BeforeDeath/ShouldDie/AfterDeath）
⑪ Cmd.CustomScaledWait(0.1f, 0.2f)
```

**格挡结算管道**（`CreatureCmd.GainBlock`，返回最终实际获得的格挡值）：`Hook.BeforeBlockGained` → `Hook.ModifyBlock`（附魔 → 全体 Additive〔敏捷/Fasten〕→ 全体 Multiplicative〔易脆/Unmovable〕→ clamp≥0）→ `AfterModifyingBlockAmount` → `GainBlockInternal`+音效VFX+历史 → `Hook.AfterBlockGained`。`fast=true` 跳过等待（连续加格挡场景，如 After Image）。

**ValueProp flags（伤害/格挡的"性质"标记，`ValueProps/` 共 110 行）**：

| Flag | 值 | 含义 |
|---|---|---|
| `Unblockable` | 2 | 不可格挡的 HP 流失（中毒类） |
| `Unpowered` | 4 | 不吃力量/敏捷类加成（遗物/药水/Power 造成的伤害格挡默认带此标记） |
| `Move` | 8 | "出招"性质：攻击卡的攻击、怪物出招 |
| `SkipHurtAnim` | 0x10 | 不播受击动画 |

预设常量（`DamageProps` / `BlockProps` 静态类）：`card`（普通卡伤）/ `cardUnpowered`（无力卡伤，如诅咒自伤）/ `cardHpLoss`（"失去 X 点 HP"代价，如 Offering）/ `monsterMove` / `nonCardUnpowered`（**Thorns**：可格挡但无力的 Power 伤害）/ `nonCardHpLoss`（**Poison**：不可格挡的 Power 伤害）；BlockProps 的 `cardUnpowered`（Entrench 翻倍不吃敏捷）与 `nonCardUnpowered`（Plating 回合末格挡不吃敏捷）。判断扩展 `IsPoweredAttack()` / `IsPoweredCardOrMonsterMoveBlock()` / `IsCardOrMonsterMove()`——力量/敏捷 Power 就是用这些方法判断"这次修正该不该吃"（Power 侧实现者矩阵见 6.2）。

**举一反三——攻击与格挡的两个官方最小例**（`DaggerSpray` / `DefendIronclad`，去噪音压缩）：

```csharp
// 攻击：全体敌人 2 段、每段 4 伤 + 自定义攻击特效 —— DaggerSpray（TargetType.AllEnemies）
await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
    .WithHitCount(2).FromCard(this, cardPlay)
    .TargetingAllOpponents(base.CombatState)
    .WithAttackerFx(() => NDaggerSprayFlurryVfx.Create(base.Owner.Creature, new Color("#b1ccca"), goingRight: true))
    .Execute(choiceContext);
// 举一反三：换单体 .Targeting(cardPlay.Target)（见 4.3 的 Bash）；换随机 .TargetingRandomOpponents(CombatState)；
//           无力化加 .Unpowered()；不再需要特效时删掉 WithAttackerFx 即可。

// 格挡：获得 5 点格挡，升级 +3 —— DefendIronclad
public sealed class DefendIronclad : CardModel
{
    public override bool GainsBlock => true;   // 声明"这是张加格挡的卡"（HoverTip / Osty 目标逻辑用）

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(5m, ValueProp.Move) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);

    protected override void OnUpgrade() => base.DynamicVars.Block.UpgradeValueBy(3m);
}
```

### 3.2 PowerCmd（上 buff/debuff 怎么做）

`Commands/PowerCmd.cs`（300 行），上 buff/debuff 的统一入口：

| 方法 | 语义 |
|---|---|
| `Apply<T>(choiceContext, target, amount, applier, cardSource, silent=false)` → `T?` | 给单个生物上 Power。返回 null 的三种情况：战斗已结束 / **被抵挡（如人工制品 Artifact）** / 净变化为 0（如 3 层力量叠 -3） |
| `Apply<T>(choiceContext, targets, amount, ...)` → 列表 | 多目标版 |
| `Apply(choiceContext, power 实例, target, amount, ...)` | 用已构造好的 PowerModel 实例施加 |
| `ModifyAmount(choiceContext, power, offset, applier, cardSource, silent)` → `int` | 改现有层数（可正可负），返回新层数 |
| `Decrement(power)` | 层数 -1 |
| `TickDownDuration(power)` | **持续回合型**递减（易伤/虚弱类），带回合顺序与 `SkipNextDurationTick` 规则（怪上给玩家的 debuff 当回合不递减） |
| `Remove<T>(creature)` / `Remove(power)` | 移除 Power |
| `FindExistingInstanceForStacking(basePower, target, applier)` | 找目标身上可叠层的已有实例 |

> Apply 内部管道（详见 6.1）：`TryModifyPowerAmountReceived`（人工制品抵消层）→ `Hook.BeforePowerAmountChanged` → `PowerModel.ApplyInternal`（已有实例则叠加，`AllowNegative` 的 Power 归 0 即移除）→ `Hook.AfterPowerAmountChanged`。

### 3.3 卡牌操作类：CardCmd / CardPileCmd / CardSelectCmd

**CardCmd**（902 行）——单卡状态变更：

| 方法 | 语义 |
|---|---|
| `AutoPlay(choiceContext, card, target?, type, skipXCapture, skipVisuals)` | **免费自动打牌**（Havoc"打出抽牌堆顶并消耗"（经 `AutoPlayFromDrawPile`）、历史课 HistoryCourse"回放上一回合最后打出的攻击牌"；`skipXCapture`=X 费不重新捕获已花资源） |
| `Discard(card)` / `Discard(IEnumerable<CardModel>)` | 弃牌。⚠️批量弃牌**勿循环调单张版**（Sly 时序会错），用集合重载 |
| `DiscardAndDraw(cards, n)` | 先弃后抽且弃牌钩子延迟到抽完再触发（CalculatedGamble 类） |
| `Exhaust(card, causedByEthereal=false, skipVisuals)` | 消耗。⚠️官方注释明言**不要做批量版**——每张卡的消耗钩子必须完整跑完再消耗下一张 |
| `Upgrade(card \| cards, style)` / `Downgrade(card)` | 真升级（内部 `UpgradeInternal` → `OnUpgrade()`） / 降级（保留附魔） |
| `Transform(original, replacement)` / `TransformTo<T>` / `TransformToRandom` / `Transform(批量)` | 变形卡牌 |
| `Enchant<T>(card, amount)` / `ClearEnchantment` | 上/清附魔 |
| `Afflict<T>(card, amount)` / `AfflictAndPreview<T>(cards, ...)` / `ClearAffliction` | 上/清畸变 |
| `ApplyKeyword(card, keywords)` / `RemoveKeyword` / `ApplySingleTurnSly(card)` / `ApplySingleTurnRetain(card)` | 关键词操作（后两者只在当回合有效） |
| `Preview(card \| cards, time, style)` / `PreviewCardPileAdd(...)` | 卡牌飞入预览动画 |

**CardPileCmd**（1289 行）——牌堆操作：

| 方法 | 语义 |
|---|---|
| `Add(card \| cards, PileType/CardPile, position=Bottom, clonedBy?, skipVisuals?)` | 加卡入堆（通用） |
| `AddGeneratedCardToCombat(card, pileType, creator?, position)` | **战斗中新生成的卡必须用这个**（写生成历史、触发 `AfterCardGeneratedForCombat`）——Shiv/碎片类 |
| `AddDuringManualCardPlay(card)` | 手动打牌进 Play 堆的快速路径（不等 tween，手感即时） |
| `AddToCombatAndPreview<T>(target(s), pileType, count, creator?)` | 生成 T 类型卡 + 预览动画 |
| `AddCurseToDeck<T>(owner)` / `AddCursesToDeck(curses, owner)` | 塞诅咒进牌库 |
| `Draw(choiceContext, player)` / `Draw(choiceContext, count, player, fromHandDraw)` | 抽牌 |
| `DrawWithoutBlockingOnOtherPlayers(...)` | 联机用：不阻塞发起者等待他人抽牌 |
| `Shuffle(choiceContext, player)` | 弃牌堆洗回抽牌堆 |
| `ShuffleIfNecessary(choiceContext, player)` | 抽牌堆空时才洗 |
| `AutoPlayFromDrawPile(choiceContext, player, count, position, forceExhaust)` | 从抽牌堆顶直接打出 n 张（不够自动洗牌；可强制消耗） |
| `RemoveFromDeck(card \| cards, showPreview)` / `RemoveFromCombat(card \| cards)` | 从牌库删除（删卡事件）/ 移出战斗 |
| `GiveToAnotherPlayer(card, player, pileType, position)` | 转移卡所有权。⚠️官方注释：不要在 `OnPlay` 里直接调（后置钩子会发给新主人），应改 `GetResultLocationForCardPlay` 或用 `ModifyCardPlayResultLocation` 钩子 |

**CardSelectCmd**（1032 行）——选牌界面（统一处理本地玩家弹窗/远程玩家等待/自动测试选择器）：

| 方法 | 场景 |
|---|---|
| `FromChooseACardScreen(context, cards, player, canSkip?)` | 小列表（≤3 张）选择（药水生成卡等） |
| `FromSimpleGrid(...)` / `FromSimpleGridForRewards(...)` | 网格选牌（抽弃堆浏览 / 奖励加卡） |
| `FromCombatPile(context, pile, player, prefs, filter?)` | 从战斗牌堆选 |
| `FromDeckForUpgrade` / `FromDeckForTransformation` / `FromDeckForEnchantment(×3)` / `FromDeckForRemoval` / `FromDeckGeneric` | 牌库选牌（带升级/变形/附魔预览、过滤与排序） |
| `FromHand(...)` / `FromHandForDiscard(...)` / `FromHandForUpgrade(...)` | 手牌选择（Armaments 类战斗内升级） |
| `FromChooseABundleScreen(player, bundles)` | 礼包选择 |

### 3.4 玩家与生物类：PlayerCmd / CreatureCmd

**PlayerCmd**（294 行）：`GainEnergy` / `LoseEnergy` / `SetEnergy`、`GainStars` / `LoseStars` / `SetStars`、`GainGold(amount, player, wasStolenBack)` / `LoseGold(amount, player, goldLossType)` / `SetGold`、`GainMaxPotionCount` / `LoseMaxPotionCount`（药水栏位）、`AddPet<T>(player)`（送宠物）、`MimicRestSiteHeal(player, playSfx)`（模拟休息回血，走 `isMimicked=true` 钩子）、`EndTurn(player, canBackOut, actionDuringEnemyTurn?)`、`CompleteQuest(questCard)`。

**CreatureCmd**（998 行）——生物（玩家+怪物）实体操作：

| 方法 | 语义 |
|---|---|
| `Damage(...)`（10 个重载） | 伤害结算（管道见 3.1）；重载维度=单/多目标 × `DamageVar`/裸值 × dealer+cardSource+cardPlay 组合；返回 `IEnumerable<DamageResult>`（可多条=伤害被 Osty 之类分流） |
| `GainBlock(creature, BlockVar \| amount+props, cardPlay?, fast=false)` → `decimal` | 获得格挡，返回**修正后**实际值 |
| `LoseBlock(choiceContext, target, amount, remover?)` | 失去格挡（碎格挡触发 `AfterBlockBroken`） |
| `Heal(creature, amount, playAnim=true)` / `SetCurrentHp` | 治疗 / 直接设当前 HP |
| `GainMaxHp` / `LoseMaxHp(choiceContext, creature, amount, isFromCard)` / `SetMaxHp` / `SetMaxAndCurrentHp` | 上限操作（`isFromCard` 决定 Rupture 类是否触发） |
| `Kill(creature(s), force=false)` | 击杀（对方全体视为击杀者）；`force=true` 绕过死亡保护（弃局时用） |
| `Escape(creature)` | 逃离战斗（不死，标记 escaped） |
| `Stun(creature, nextMoveId?)` / `Stun(creature, stunMove, nextMoveId?)` | 击晕（可自定义击晕回合执行的行为） |
| `Add<T>(combatState, slotName?)` / `Add(monster, ...)` / `Add(creature)` | 战斗中加入怪物 |
| `TriggerAnim(creature, triggerName, waitTime)` | 播生物动画 |

### 3.5 其余：ForgeCmd / OrbCmd / OstyCmd / PotionCmd / RelicCmd / RewardsCmd / MapCmd / VfxCmd / SfxCmd / TalkCmd / ThinkCmd

| Cmd | 关键 API |
|---|---|
| `ForgeCmd` | `Forge(amount, player, source)`——铸造：本战斗首次铸造把 Sovereign Blade 加进手牌，之后每次 +amount（返回全部未消耗的 Sovereign Blade） |
| `OrbCmd` | `AddSlots` / `RemoveSlots`、`Channel<T>()` / `Channel(orb)`（充能）、`EvokeNext` / `EvokeLast`（激发）、`Passive(orb, target?, countAffectedByHooks)`（触发被动） |
| `OstyCmd` | `Summon(choiceContext, summoner, amount, source)`——召唤 Osty；已有 Osty 则加其最大 HP（`source`=来源模型，如 Bodyguard） |
| `PotionCmd` | `TryToProcure<T>(player)` / `TryToProcure(potion, player, slotIndex=-1)`——获得药水（**药水栏满则失败返回 null**）、`Discard(potion)` |
| `RelicCmd` | `Obtain<T>(player)` / `Obtain(relic, player, index=-1)`、`Remove(relic)`、`Replace(original, replace)`、`Melt(relic)`（熔毁：保留在栏但失效） |
| `RelicSelectCmd` | `FromChooseARelicScreen(player, relics)`——遗物选择界面 |
| `RewardsCmd` | `OfferForRoomEnd(player, room)` / `OfferCustom(player, rewards)`（弹奖励界面；Custom 版同样过 `TryModifyRewards` 钩子）、`GenerateForRoomEnd` / `GenerateCustom`（只生成不弹窗） |
| `MapCmd` | `SetBossEncounter(runState, boss)` |
| `VfxCmd` | `PlayOnCreature` / `PlayOnCreatureCenter(s)` / `PlayOnSide` / `PlayOnCreatures` / `PlayFullScreenInCombat` / `PlayNonCombatVfx` 等；内置 27 个 VFX 资产路径 |
| `SfxCmd` | `Play(sfx, volume)` / `PlayLoop` / `StopLoop` / `SetParam` / `PlayDamage` / `PlayDeath` / `PlayCardSwooshSfx`（FMOD 事件路径） |
| `TalkCmd` / `ThinkCmd` | `Play(line, speaker, ...)`——战斗内对话气泡 / 思考气泡 |

## 4. 数值系统：DynamicVar 与描述联动

> 目录 `src/Core/Localization/DynamicVars/`，命名空间 `MegaCrit.Sts2.Core.Localization.DynamicVars`，26 个文件（24 个具体 Var + 基类 `DynamicVar` + 集合 `DynamicVarSet`）。

### 4.1 DynamicVar 三值模型

每个 `DynamicVar` 持有三个值，是理解官方卡牌数值显示的关键：

| 值 | 用途 |
|---|---|
| `BaseValue` | **真实逻辑值**——所有结算代码只应读这个（`DynamicVars.Damage.BaseValue`） |
| `EnchantedValue` | 附魔加乘之后、全局钩子之前的值。玩家眼里"附魔属于卡的一部分"，所以它不高亮显示 |
| `PreviewValue` | **仅显示用**：套用全局钩子（力量/易伤/人工制品遗物等）后的最终值，卡面实时变色。⚠️严禁拿它做结算 |

升级联动：`UpgradeValueBy(addend)` 把 `BaseValue` 加上 addend 并标记 `WasJustUpgraded`（升级预览绿色高亮）；升级流程结束时 `FinalizeUpgrade()` 清标记。数值 clamp 上限 999999999。

### 4.2 描述占位符机制：`{Damage:diff()}`

- 卡牌描述存在本地化 JSON（`cards/{Id}.description`），占位符用 **SmartFormat** 语法：`"造成 {Damage:diff()} 点伤害。"`。
- `CardModel.DynamicVars`（`DynamicVarSet`，键=Var 的 `Name`）通过 `AddTo(locString)` 把所有 Var 注入；`CardModel.GetDescriptionForPile` 还额外注入 `OnTable`/`InCombat`/`IsTargeting`/`TargetType`/`GainsBlock`/`IsOstyAlive`/`energyPrefix` 等系统变量（描述文本可据此做条件分支）。
- `:diff()` 是自定义格式化器（`Formatters/HighlightDifferencesFormatter`）：比较 `PreviewValue` 与 `EnchantedValue`，高（绿）/低（红）/相等（默认色）；`WasJustUpgraded` 时强制按"变高"处理。反向（降为优）用 `{X:diff(inverse=true)}` 类调用。
- 每次卡面刷新时 `CardModel.UpdateDynamicVarPreview` 遍历 Var 调 `UpdateCardPreview(card, previewMode, target, runGlobalHooks)`：只有手牌/Play 堆中的卡才跑全局钩子（`runGlobalHooks=true`），牌库/奖励里的卡只算附魔部分。

### 4.3 CanonicalVars 声明与 OnUpgrade 模式（官方范本 `Bash.cs` 全文摘录）

```csharp
public sealed class Bash : CardModel
{
    // 数值声明：攻击 8 点 + 易伤 2 层
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(8m, ValueProp.Move), new PowerVar<VulnerablePower>(2m) };

    public Bash() : base(2, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target,
            DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Vulnerable.UpgradeValueBy(1m);
    }
}
```

要点：① 数值全部声明在 `CanonicalVars`，描述 JSON 里用对应占位符引用；② `OnPlay` 只读 `BaseValue`；③ `OnUpgrade` 全部走 `UpgradeValueBy`（例外卡的构成见 5.1 升级三流派）；④ 带上 Power 的卡通常另写 `ExtraHoverTips`（`HoverTipFactory.FromPower<T>()`）显示 buff 提示。

### 4.4 24 个 DynamicVar 总表

**带实时预览**（重写了 `UpdateCardPreview`，卡面数值会随 buff/debuff/遗物实时变色）：

| Var | 默认占位符名 | 用途 | 预览管道 |
|---|---|---|---|
| `DamageVar(damage, props=Move)` | `Damage` | 攻击伤害 | 附魔加乘→EnchantedValue；`Hook.ModifyDamage`（全段）→PreviewValue |
| `BlockVar(block, props=Move)` | `Block` | 获得格挡 | 同构，走 `Hook.ModifyBlock` |
| `ExtraDamageVar(damage)`（`.FromOsty()`） | `ExtraDamage` | 计算型伤害卡的"每 X 加 Y"中的加值（PerfectedStrike 的"+2"） | 走 `Hook.ModifyDamage`（Osty 版换 dealer） |
| `OstyDamageVar(damage, props)` | `OstyDamage` | Osty 专属攻击伤害（吃 Osty 身上的力量/虚弱） | 走 `Hook.ModifyDamage`，dealer=Osty |
| `SummonVar(amount)` | `Summon` | 召唤 Osty 的 HP 量 | `Hook.ModifySummonAmount` |
| `CalculatedDamageVar` | `CalculatedDamage` | 计算型伤害总值（见下） | `Calculate(target)` 再走完整伤害预览 |
| `CalculatedBlockVar` | `CalculatedBlock` | 计算型格挡总值 | 同构走 `Hook.ModifyBlock` |
| `PowerVar<T>(amount)` | **`typeof(T).Name`**（如 `{VulnerablePower}`） | 上 buff 的层数 | `Hook.ModifyPowerAmountGiven`（SneckoSkull +1 之类会反映到卡面） |

**静态值**（不重写预览，数值固定）：

| Var | 默认占位符名 | 用途 |
|---|---|---|
| `HealVar(healAmount)` | `Heal` | 回复量 |
| `GoldVar(gold)` | `Gold` | 金币 |
| `MaxHpVar(maxHp)` | `MaxHp` | 最大 HP |
| `HpLossVar(hpLoss)` | `HpLoss` | HP 流失代价 |
| `CardsVar(cards)` | `Cards` | 抽/弃/选卡张数 |
| `RepeatVar(times)` | `Repeat` | 重复次数 |
| `EnergyVar(energy)` | `Energy`（`defaultName` 常量） | 能量；带 `ColorPrefix` 属性配合 `energyPrefix` 系统变量给能量图标着色 |
| `StarsVar(stars)` | `Stars` | 辉星 |
| `ForgeVar(forge)` | `Forge` | 铸造加值 |
| `IfUpgradedVar(upgradeDisplay)` | `IfUpgraded` | 按 `UpgradeDisplay`（Normal/Upgraded/UpgradePreview）切换文本段 |
| `BoolVar(name, value)` | 自定义 | 布尔条件文本 |
| `IntVar(name, amount)` | 自定义 | 通用整数 |
| `StringVar(name, baseValue="")` | 自定义 | 任意字符串 |

**计算型专用**（`CalculatedVar` 基类 + 两个配对变量）：

- `CalculationBaseVar`（占位符 `CalculationBase`）/ `CalculationExtraVar`（占位符 `CalculationExtra`）：计算型的"底数"与"每单位加值"。
- `CalculatedVar.WithMultiplier(static Func<CardModel, Creature?, decimal>)` 注册动态倍率（**必须是静态函数**，反编译安全约束）；`Calculate(target) = CalculationBase.BaseValue + CalculationExtra.BaseValue × multiplier(card, target)`，战斗外倍率按 0 处理。
- 典型：PerfectedStrike"6 伤害 + 你每张含 Strike 的卡 +2"→ `CalculationBase=6`、`CalculationExtra=2`、倍率=含 Strike 的卡数；`DamageCmd.Attack(calculatedDamageVar)` 直接吃这个 Var（3.1）。
- `RecalculateForUpgradeOrEnchant`：升级时重算 BaseVar，若底数变了同样标绿。

> 升级行为总结：具体数值 Var 一律 `UpgradeValueBy`；计算型卡升级 `CalculationBase`/`CalculationExtra` 即可，总值自动跟着变；`DynamicVarSet.RecalculateForUpgradeOrEnchant()` 由 `CardCmd.Upgrade → UpgradeInternal` 统一触发。
>
> **本项目（RitsuLib）注意**：上面这套官方三件套能用但繁琐（RitsuLib 文档原话"一般不推荐"）。写 mod 卡的计算型数值一律改用 RitsuLib 的 `ModCardVars.ComputedDynamicVar` 系列——`Computed` / `ComputedDamage` / `ComputedBlock` / `ComputedOstyDamage` / `ComputedEnergy` / `ComputedStars` / `ComputedPower<T>`（显示值由委托运行时计算；`ComputedDamage/ComputedBlock` 的预览自动走本节所述的官方修正管道），效果里用 `DynamicVars.EvaluateValueOrDefault(名, target:)` 读取。详见 RitsuLib 教程《19 - 计算动态变量》与效果教程 §1.3/§4.1.1。

## 5. 卡牌效果实现

> 覆盖 `Models/Cards/` 596 张官方卡（另 8 张 Mocks 测试类不计）。组织原则：每个「实现模式」精讲一个代表卡，其余卡仅以名字归档（见 5.9 索引）。

### 5.1 卡牌生命周期

**卡牌专属可重写点**（`CardModel` 上的，与第 2 章通用钩子相加才是完整可重写面）：

| 入口 | 重写数 | 说明 |
|---|---|---|
| `OnPlay(choiceContext, cardPlay)` | 564 | 效果主体。Replay（打两次）时会被调用多次；`OnPlayWrapper` 保证期间卡已在 Play 堆 |
| `OnUpgrade()` | 541 | 升级逻辑（见下方三种流派） |
| `OnTurnEndInHand(choiceContext)` | 11 | 手牌回合结束触发，**必须**同时 `override bool HasTurnEndInHandEffect => true`；执行时卡在 Play 堆，之后进弃牌堆。全部 11 张都是状态/诅咒（Burn/Doubt/Regret/Debt…） |
| `OnEnqueuePlayVfx(target)` | 4 | 鼠标松开瞬间的前置 VFX/动画（Inflame 类 Power 卡起手演出） |
| `GetResultLocationForCardPlay()` | 3 | 改打完后的去向（默认：Power/复制品→移除、Exhaust 关键词→消耗、否则→弃牌） |
| `IsPlayable` / `ShouldGlowGoldInternal` / `ShouldGlowRedInternal` | 3/15/16 | 自定义可打出条件与金/红光提示（GrandFinale"抽牌堆为空才可打"） |
| `MaxUpgradeLevel`（默认 1） | 41 | 多级升级卡；状态/诅咒卡重写为 `=> 0` 表不可升级 |
| `GainsBlock`（81）/`CanonicalKeywords`(161)/`CanonicalTags`(45)/`ExtraHoverTips`(290) | — | 声明式标记：格挡卡识别、关键词、标签（Strike/Defend/Shiv）、悬停提示 |

**`OnPlayWrapper` 执行管线**（卡被打出时系统做的事，Mod 开发需知道依赖顺序）：进 Play 堆 → `Hook.ModifyCardPlayResultLocation` 定去向 → `GeneratePlayCount`（BaseReplayCount + 附魔 + `ModifyCardPlayCount` 钩子）→ 循环 playCount 次：`Hook.BeforeCardPlayed` → **`OnPlay`** → 附魔/畸变 `OnPlay` → `Hook.AfterCardPlayed(+Late)` → 移入结果牌堆 → 检查空手 → 清理临时费用/标记。

**升级三流派**（541 张有 `OnUpgrade` 的卡）：
1. **数值升级**（约 495 张）：`DynamicVars.X.UpgradeValueBy(n)`（见 4.3 Bash 范本）。
2. **关键词开关**（约 25 张）：`AddKeyword(CardKeyword.Innate)` / `RemoveKeyword(CardKeyword.Exhaust|Ethereal)`（Discovery/Apotheosis 式"升级删除消耗"）。
3. **降费**（约 8 张）：`EnergyCost.UpgradeBy(-1)`（WellLaidPlans/HammerTime/Apotheosis）。

**降级回调**：`AfterDowngraded()`（9 张重写）——持久成长类卡（Claw/Rampage/KinglyPunch…）私有字段记录累计增幅，降级会重置 `BaseValue`，必须在此回补，否则"被降级的卡丢失成长"。

### 5.2 效果手段 × 卡牌数量分布（全量统计）

**CardType**：Skill 246 / Attack 198 / Power 113 / Curse 18 / Status 17 / Quest 4
**TargetType**：Self 307 / AnyEnemy 186 / None 36 / AllEnemies 35 / AnyAlly 15 / RandomEnemy 9 / AllAllies 8

**效果手段（使用了该 API 的卡数，前 16 名）**：

| API | 卡数 | API | 卡数 |
|---|---|---|---|
| `PowerCmd.Apply` | 247 | `CardSelectCmd.FromHand` | 24 |
| `DamageCmd.Attack` | 196 | `PlayerCmd.GainEnergy` | 24 |
| `CreatureCmd.GainBlock` | 80 | `CreatureCmd.Damage`（非攻击伤害） | 18 |
| `CardPileCmd.Draw` | 51 | `CardCmd.Upgrade` | 20 |
| `CardPileCmd.AddGeneratedCard*` | 36 | `OrbCmd.Channel` | 22 |
| `CardCmd.Exhaust` | 12 | `ForgeCmd.Forge` / `OstyCmd.Summon` | 10 / 9 |
| `CardCmd.AutoPlay` | 8 | `CardCmd.Discard` | 9 |
| `PlayerCmd.GainStars` | 9 | `CardPileCmd.Shuffle` 等 | 各 1~3 |

**长尾兜底结果**：全量 API/钩子/Var/体量四维扫描后，低频实例已逐个过目（两轮清点约 110 张），独特实现归入 5.3~5.8 的"独特机制"，其余均可并入已有模式。**通用钩子重写在卡牌层极少**（OnPlay/OnUpgrade 之外全部合计 <60 张），卡牌的"被动效果"大多委托给配套 Power 实现——先确认效果归属在卡还是 Power（Shadowmeld 的"打出限制"其实是 Power 自带钩子）。

**DynamicVar 使用分布**（声明次数）：DamageVar 177、PowerVar 合计 ~140（易伤 17/虚弱 16/力量 15/毒 7/敏捷 7/集中 5…）、CardsVar 89、BlockVar 77、**自定义命名 `new DynamicVar("Xxx", n)` 62**、EnergyVar 47、CalculationBase/Extra 43+24、RepeatVar 24、ExtraDamageVar/CalculatedDamageVar 各 19、Stars/Summon/OstyDamage/Forge 各 11、HpLoss 10。
**关键词卡数**：Exhaust 118、Unplayable 33、Ethereal 26、Retain 30、Innate 24、Sly 9、Eternal 7。

### 5.3 攻击类实现模式

基础链式写法已在 3.1（DaggerSpray）与 4.3（Bash）给出，此处只补模式变体：

- **A1 单体攻击**（~140 张）：`Attack(DamageVar).FromCard(this, cardPlay).Targeting(cardPlay.Target).WithHitFx(...).Execute(ctx)`，代表 `StrikeIronclad`（五角色 Strike 仅立绘/特效/配色不同，靠 Tag+Var 参数化）。
- **A2 多段**：段数来源三种——固定 `WithHitCount(n)`；`RepeatVar`（SwordBoomerang `.WithHitCount(DynamicVars.Repeat.IntValue)`，升级=段数+1）；**X 费** `HasEnergyCostX => true` + `WithHitCount(ResolveEnergyXValue())`（Whirlwind）。
- **A3 全体**：`TargetingAllOpponents(CombatState)`（Thunderclap：AOE 后对 `CombatState.HittableEnemies` 上易伤——**AOE 攻击配全体 debuff 的标准二连**）。
- **A4 随机**：`TargetingRandomOpponents(CombatState)`（每击重摇；SwordBoomerang/狂乱类）。
- **A5 无力化/HP loss**：状态牌反伤用 `CreatureCmd.Damage(..., DamageProps.cardUnpowered)`（Burn）；"真正的攻击但无视力量"才用 `.Unpowered()`（Omnislice）。
- **A6 攻+上 debuff**：Bash/Uppercut（复合双 debuff，段间用 `WithAttackerAnim` 重型动画）。
- **A7 跨场持久成长**（Claw 式，7 张）：代表 `Claw`：

```csharp
public sealed class Claw : CardModel
{
    private decimal _extraDamageFromClawPlays;   // 私有字段记录"本局打出的成长"
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(3m, ValueProp.Move), new DynamicVar("Increase", 2m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
        // 遍历全场（含抽弃堆）所有同类卡，齐涨
        foreach (Claw c in base.Owner.PlayerCombatState.AllCards.OfType<Claw>())
            c.BuffFromClawPlay(base.DynamicVars["Increase"].BaseValue);
    }
    protected override void AfterDowngraded()   // 降级回补，否则丢成长
        => base.DynamicVars.Damage.BaseValue += _extraDamageFromClawPlays;
}
```
  持久化变体：GeneticAlgorithm/TheScythe 用 `[SavedProperty]` 存增量 + 写回 `DeckVersion`，实现**跨战斗永久成长**。
- **A8 斩杀（Fatal）击杀奖励**（Feed/HandOfGreed/TheHunt 的标准模板，已核对源码）：

```csharp
bool shouldTriggerFatal = cardPlay.Target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal()); // 有免死类 Power 则不发奖励
AttackCommand cmd = await DamageCmd.Attack(...).Targeting(cardPlay.Target).Execute(choiceContext);
if (shouldTriggerFatal && cmd.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
    await CreatureCmd.GainMaxHp(base.Owner.Creature, base.DynamicVars.MaxHp.IntValue);
```
  注意 `Results` 是"每击一个列表"的嵌套结构，必须 `SelectMany`；卡面配 `HoverTipFactory.Static(StaticHoverTip.Fatal)`。
- **A9 计算型伤害**（"每 X 得 Y"，19+ 张）：`CalculatedDamageVar.WithMultiplier(静态lambda)`，lambda 可读战斗历史（GoldAxe 按本场出牌数、Murder 按本场抽牌数）、目标 Power 层数（TimesUp 按 Doom 层数）、甚至镜像队友格挡（Mimic）；配合 `CalculationBaseVar/CalculationExtraVar` 声明（见 4.4）。
- 同模式归档：Clash/BodySlam（0费条件）、Skewer/Whirlwind（X费多段）、Rampage/Maul/TheBall/Thrash/KinglyPunch（成长系）。

### 5.4 格挡与防御类

- **D1 基础格挡**：`DefendIronclad`（3.1 已全文摘录）——`GainsBlock => true` + `BlockVar` + `GainBlock`。
- **D2 格挡+附加**（FlameBarrier）：格挡 + `DynamicVar("DamageBack")` 配套 Power（反伤），双 Var 分别升级。
- **D3 条件批量格挡**（SecondWind）：循环手牌逐张 `CardCmd.Exhaust` + 逐张 `GainBlock`——**不要合并成一次大格挡**，每张的消耗钩子要完整跑完（3.3 的官方警告）。
- **D4 格挡数值操作**：Entrench 翻倍用 `BlockProps.cardUnpowered`（不吃敏捷）；Barricade 不清格挡在 Power 侧 `ShouldClearBlock`。
- **D5 清除敌方格挡**（Expose）：`CreatureCmd.LoseBlock(ctx, target, target.Block, remover)` 传 `target.Block` 即"清空"，随后 `PowerCmd.Remove<ArtifactPower>(target)` 显式吃掉人工制品再上易伤。
- 同模式归档：ShrugItOff（格挡+抽）、IronWave（格挡+攻）、Leap/NeutronAegis（大格挡）、UltimateDefend…

### 5.5 上 buff/debuff 类（PowerCmd.Apply 用法集）

- **B1 Power 卡自我强化**（~113 张 Power 卡的主流）：`PowerVar<T>` + `PowerCmd.Apply<T>(ctx, Owner.Creature, ...)`——**Power 卡的回合成长逻辑全部写在同名 Power 类里**（DemonForm 卡只上 DemonFormPower，回合开始+力量在 Power 的 `AfterSideTurnStart`），代表 `Inflame`/`DemonForm`。
- **B2 给敌人上 debuff**：`Apply<WeakPower>(ctx, cardPlay.Target, ...)`。
- **B3 全体上**：`Apply<T>(ctx, CombatState.HittableEnemies, ...)`（Thunderclap）。
- **B4 多人目标变体**：给队友上（Soulbound，MultiplayerOnly）。
- **B5 改已有 Power 层数而非重复施加**（3 张，注意语义）：`PowerCmd.ModifyAmount(ctx, power, offset, applier, cardSource)`；跨敌人复制 debuff 的标准姿势（Misery）：先 `FindExistingInstanceForStacking(basePower, enemy, applier)` 找已有实例，命中则 ModifyAmount 叠层，未命中才 `Apply(克隆)`——盲目 Apply 会丢叠层语义。
- **B6 Apply 返回值利用**：`Apply` 返回 `T?`（被人工制品抵挡/净变化 0/战斗结束时为 null）；Monologue 拿返回的 Power 实例**回写其 DynamicVars 同步卡面数值**。
- 同模式归档：Bludgeon(攻+易伤)/LegSweep(虚弱+缓速)/Footwork/Metallicize/Demon Form/Afterimage/Buffer 等 247 张（完整名单见 5.9）。

### 5.6 过牌与资源类（抽/弃/能量/回血/金币/辉星）

- **R1 抽牌**（51 张）：`CardPileCmd.Draw(ctx, CardsVar, player)`；带副作用的写法=Draw 后紧跟 Apply（BattleTrance 抽3+NoDrawPower）；补满手牌（Scrawl：`手牌上限 - 现有数`）。
- **R2 弃抽循环**（CalculatedGamble）：`CardCmd.DiscardAndDraw(手牌, 同数)`——弃牌钩子延迟到抽完再触发，勿用 Discard+Draw 拼。
- **R3 能量**（24 张）：当回合给=`PlayerCmd.GainEnergy`；下回合给=`PowerCmd.Apply<EnergyNextTurnPower>`（Outmaneuver）；失去能量（Void 诅咒）在 `AfterCardDrawn` 里 `if (card == this)` 后 `LoseEnergy`。
- **R4 回复/HP上限**：`CreatureCmd.Heal`（Spur 治疗召唤物/NotYet 治疗自己）；`GainMaxHp`（Feed，见 A8）；`LoseMaxHp(ctx, creature, n, isFromCard: true)` 代价型（BrightestFlame，放效果最后）。
- **R5 金币**：战斗中得金=斩杀（Fatal）模板（HandOfGreed）；扣金诅咒（Debt）在 `OnTurnEndInHand` 里 `Mathf.Min(gold, owner.Gold)` 钳制后 `LoseGold`。
- **R6 辉星**（12 张）：花星=`CanonicalStarCost` 覆写（RoyalGamble：花5星得星）；得星=`PlayerCmd.GainStars`；四合一卡 BigBang 一个 OnPlay 里 Draw+Energy+Stars+Forge 各一行。
- **R7 自动出牌**（8 张）：`CardPileCmd.AutoPlayFromDrawPile(ctx, player, n, CardPilePosition.Top, forceExhaust)`（Havoc=true）；免费自动打手牌/堆顶卡用 `CardCmd.AutoPlay`（X 费传 `skipXCapture: true`）。
- **R8 抽牌分发/不阻塞**（多人）：`DrawWithoutBlockingOnOtherPlayers`（HuddleUp 对每个存活队友调用）。
- 同模式归档：Skim/PommelStrike（抽+杂）、Expertise（抽到6张+单回合Retain）…

### 5.7 消耗 / 弃置 / 保留 / 手牌触发 / 费用操作

- **E1 消耗换收益**：本卡消耗=关键词声明（Discovery/Apotheosis），升级删关键词（5.1 流派2）；消耗**其他**卡=循环 `CardCmd.Exhaust`（SecondWind/Cleanse/TrueGrit，12 张）。
- **E2 弃牌**：`CardCmd.Discard` 单张/集合两个重载（批量勿循环单张）；弃手牌选择=`CardSelectCmd.FromHandForDiscard`。
- **E3 保留/奇巧操作**：`CardCmd.ApplySingleTurnRetain`（Expertise：对 `Draw` 返回的卡列表逐张调用）/`ApplySingleTurnSly`（HandTrick：先 `FromHand` 选 1 张）；常驻关键词直接 `ApplyKeyword`（Snap/SculptingStrike——雕琢打击的过滤用 `GetKeywordsWithSources(KeywordSources.Local)` 排除已带虚无的牌）；虚空之唤（CallOfTheVoid）则给**生成的**随机牌预先上虚无（排除 Basic/Ancient 后入堆前 `ApplyKeyword`）。
- **E4 手牌回合结束触发**（11 张，全状态/诅咒）：`HasTurnEndInHandEffect => true` + `OnTurnEndInHand`，代表 `Burn`：

```csharp
public sealed class Burn : CardModel
{
    public override int MaxUpgradeLevel => 0;                      // 状态牌三件套之一
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Unplayable };
    public Burn() : base(-1, CardType.Status, CardRarity.Status, TargetType.None) { }   // 费用-1
    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
        => await CreatureCmd.Damage(choiceContext, base.Owner.Creature, base.DynamicVars.Damage, this, null);
    // CanonicalVars: new DamageVar(2m, ValueProp.Unpowered | ValueProp.Move) —— 无力化自伤
}
```
  动态数值诅咒 `Regret`：`BeforeSideTurnEnd` 记录手牌数 → `OnTurnEndInHand` 按 `CardsInHand` 造成 `Unblockable|Unpowered|Move` 伤害。
- **E5 费用操作**（减费三件套，5 张）：进入战斗时用 `CombatManager.Instance.History.Entries.OfType<…>()` 回溯本回合已发生事件补减 + `AfterCardPlayed`/`BeforeCardPlayed`/`AfterCardExhausted` 等实时钩子续减，落点都是 `EnergyCost.AddThisCombat(-n)` / `AddThisTurn(-n)` / `SetThisTurn(0)`；守卫惯例：`if (card != this) return`、`if (IsClone) return`。提费（Transfigure）：`EnergyCost.AddThisCombat(1)` + 公开属性 `BaseReplayCount++`（**直接可写**，实现"打两次"）。
- **E6 打完去向控制**：`override GetResultLocationForCardPlay()`（TheBall 多人模式把弃置改投队友牌堆；Anger 打完 `CreateClone()` 塞一张进弃牌堆=`AddGeneratedCardToCombat(clone, PileType.Discard)`+`PreviewCardPileAdd`）。

### 5.8 特殊机制（召唤 / 辉星 / 铸造 / 充能球 / 生成卡 / 状态诅咒 / 任务 / 独特）

- **S1 生成卡牌**（36 张）：生成方=静态工厂（`Shiv.CreateInHand(owner, combatState)`，每张可生成卡自带工厂方法）→ 调用方 `AddGeneratedCardToCombat(card, PileType.Hand, owner)`（写生成历史）；"免费塞手"两连=`card.SetToFreeThisTurn()` + `AddGeneratedCardToCombat`（Discovery）；随机生成=`CardFactory.GetDistinctForCombat(...)` + `CardSelectCmd.FromChooseACardScreen(..., canSkip)`（Discovery/InfernalBlade/Alchemize）。
- **S2 召唤 Osty**（9 张）：`OstyCmd.Summon(ctx, owner, SummonVar, this)`（Bodyguard，配 Necrobinder 角色的召唤动画 helper）；处决自己的召唤物=`CreatureCmd.Kill`（BoneShards/Sacrifice——注意 Kill 的"对面全体视为击杀者"语义）。
- **S3 铸造**（10 张）：`ForgeCmd.Forge(n, player, this)`（首次铸造自动发 Sovereign Blade 到手，之后 +n）；SovereignBlade 本体是 252 行 Token 卡：`TargetType`/`GainsBlock` 随 SeekingEdge/Parry Power 动态切换。
- **S4 充能球**（~30 张）：生成 `OrbCmd.Channel<T>()`（BallLightning 攻+生成二连）；栏位 `AddSlots`（上限 10 自动钳制）/`RemoveSlots`；手动激发被动 `OrbCmd.Passive`（Darkness 传 null 目标/TeslaCoil 指定敌人）；激发 `EvokeNext/EvokeLast`。
- **S5 辉星**（12 张）：辉星收益逻辑全部委托同名 Power（ChildOfTheStars 只上 Power）。
- **S6 状态/诅咒卡**（35 张）：三件套=`CardType.Status|Curse` + cost `-1` + `MaxUpgradeLevel => 0`，多数加 `Unplayable`。变体：Wound 纯死牌（无任何效果）；Wither 假升级（换三套立绘但 `MaxUpgradeLevel=0`）；FranticEscape **可打出的状态**（每次打出给自己 `EnergyCost.AddThisCombat(1)` 涨费）；Guilty 走 `AfterCombatEnd` 在牌库里计数、满 5 场 `CardPileCmd.RemoveFromDeck(this)` 自毁。
- **S7 任务卡**（4 张）：`CardType.Quest` + Unplayable；改地图流（LanternKey：`ModifyUnknownMapPointRoomTypes` 把未知节点锁成 Event + `ModifyNextEvent` 锁定指定事件）；计数流（Dowsing `BeforeRoomEntered` 计数、SpoilsMap `ModifyGeneratedMap` 撒宝箱）；完成=`PlayerCmd.CompleteQuest` → `TransformTo<T>`/`RemoveFromDeck` 收尾；进度用 `[SavedProperty]` 持久化。
- **S8 附魔/畸变**：`CardCmd.Enchant<T>(card, amount)`（BladeOfInk 给生成的 Shiv 上附魔——**void 同步调用不 await**）；畸变 `CardCmd.Afflict`。
- **S9 独特机制逐例**：
  - **MadScience**（303 行，最大卡）：`[SavedProperty]` 存 `CardType` 字段驱动 Type/TargetType/立绘/描述全动态，11 个 Var + `AddExtraArgsToDescription` 拼文案。
  - **Misery**：`ClonePreservingMutability` 快照目标全部 debuff（临时 Power 并回本体）→ 批量复制给其他敌人（B5 姿势）。
  - **VoidForm**：打出后立即结束回合=`PlayerCmd.EndTurn(owner, canBackOut: false)`（**同步调用**，先 Apply 后 EndTurn）。
  - **药水交互**（Alchemize）：`PotionFactory.CreateRandomPotionInCombat(...).ToMutable()` 生成 + `PotionCmd.TryToProcure`（药水栏满会失败）。

### 5.9 效果模式 → 卡牌归档索引

> 仅名字归档便于反查（括号为该组要点）；代表卡加粗。

| 模式 | 代表 | 同模式卡牌 |
|---|---|---|
| 单体攻击 | **StrikeIronclad** | Strike 五角色系、Bash、Bludgeon、Clash、BodySlam、TwinStrike、Reap、SuckerPunch、DeadlyPoison、BlightStrike、Snakebite 等约 140 张 |
| 多段攻击 | **SwordBoomerang**（RepeatVar） | Skewer、FiendFire（按手牌数）、SwordBoomerang、DaggerSpray、Conflagration、RipAndTear、Eradicate、FlakCannon（随机段） |
| X 费攻击 | **Whirlwind** | Cascade、Dirge、Eradicate、HeavenlyDrill、Malaise、MultiCast、Skewer、Tempest、Volley（能量 X 共 10 张，实测 `HasEnergyCostX`；另有辉星 X：Stardust） |
| 全体攻击 | **Thunderclap** | Thunderclap、SweepingBeam、CorrosiveWave、ReaperForm 系、DarkShackles |
| 随机目标 | **SwordBoomerang** | 狂乱系、SwordBoomerang、FlakCannon、BouncingFlask（随机上毒） |
| 无力化/HP loss | **Burn** | Offering、Bloodletting、Debt、Void、Burn、Wither、Regret（自伤/代价/诅咒） |
| 攻+debuff | **Bash** | Bash、Uppercut（双 debuff）、LegSweep、GoForTheEyes、Expose（清挡+易伤）、PoisonedStab |
| 持久成长攻击 | **Claw** | Claw、Rampage、Maul、TheBall、Thrash、KinglyPunch（AfterCardDrawn 成长）、GeneticAlgorithm、TheScythe（[SavedProperty] 跨战持久） |
| 斩杀奖励（Fatal） | **Feed** | Feed、HandOfGreed、TheHunt |
| 计算型数值 | **PerfectedStrike** | PerfectedStrike、GoldAxe（按出牌数）、Murder（按抽牌数，`CardDrawnEntry` 历史计数）、TimesUp（按 Doom）、Mirage（全场毒）、Mimic（镜像格挡）、Rend（按减益数）、DemonicShield、全身撞击（按当前格挡）、心灵震慑（按抽牌堆剩余）、堆栈（按弃牌堆张数）。**本项目实现改用 RitsuLib `ComputedDynamicVar`（见 §4.4 尾注与第 9 章），不抄官方三件套** |
| "下一张牌"家族 | **无情猛攻 Unrelenting** | Unrelenting、Pounce（猛扑）、Synthesis、Veilpiercer（0 费包装 `Free*Power` 拦截 `TryModifyEnergyCostInCombatLate`）、SignalBoost（下一张能力多打一次）、Rebound（下一张牌置顶） |
| 延迟回手 | **流星锤 Bolas** | Bolas、无休手斧 ThrummingHatchet（下回合开始查历史回手）、粒子墙 ParticleWall（即时回手）、得力助手 RightHandHand（条件回手） |
| 多人协作 | **Tutor** | Tutor（检索队友抽牌堆入手，MultiplayerOnly）、Soulbound（给队友上增益）、HuddleUp（给每个存活队友抽牌，不阻塞）、TheBall（弃置改投队友）、DrawWithoutBlockingOnOtherPlayers |
| 基础格挡 | **DefendIronclad** | Defend 五角色系、ShrugItOff、Leap、IronWave、Finesse、Slice、NeutronAegis、UltimateDefend |
| 格挡+附加 | **FlameBarrier** | FlameBarrier（反伤）、Barricade（不清挡，Power 侧）、Entrench（翻倍 Unpowered）、SecondWind（消耗换挡） |
| Power 卡自我强化 | **Inflame** | Inflame、DemonForm、Footwork、Metallicize、Afterimage、EchoForm、Buffer、VoidForm、WellLaidPlans、HammerTime、MachineLearning、Speedster、Cruelty、Envenom、Arsenal、BeaconOfHope 等 ~110 张 |
| 全体/多目标 debuff | **Thunderclap** | Thunderclap、Haze（双 debuff）、Shockwave、NoxiousFumes、DarkShackles |
| 改已有 Power 层数 | **Misery** | Misery（复制 debuff）、ImitationLearning、FranticEscape（改敌方 Power） |
| 抽牌 | **BattleTrance** | Skim、PommelStrike、Expertise、Scrawl（补满）、ThinkingAhead（抽2置顶1）、BurningPact、EscapePlan |
| 弃抽/弃牌收益 | **CalculatedGamble** | CalculatedGamble、Tactician（弃牌得能量）、Reflex、AllForOne（捞 0 费） |
| 能量操作 | **Outmaneuver** | Outmaneuver、DoubleEnergy、EnergySurge、Turbo、Void（扣能量诅咒） |
| 金币/辉星 | **RoyalGamble** | HandOfGreed、Jackpot、RoyalGamble、BigBang（四合一）、ChildOfTheStars、GuidingStar、CloakOfStars |
| 回复/HP上限 | **Feed** | Feed、Spur、NotYet、BrightestFlame、Bloodletting（自损换益） |
| 自动出牌 | **Havoc** | Havoc、Cascade、IAmInvincible、HowlFromBeyond（回合末自动打）、Mayhem、Bombardment |
| 消耗换收益 | **SecondWind** | SecondWind、Cleanse、TrueGrit、FiendFire、BurningPact、Purity、Scavenge、Stoke、Thrash、FlakCannon |
| 保留/奇巧 | **Expertise** | Expertise、HandTrick、Snap、SculptingStrike、WellLaidPlans、Fasten、Reflect |
| 手牌回合结束触发 | **Burn** | Burn、Doubt、Regret、Debt、Shame、Decay、Toxic、Infection、Wither、BadLuck、Beckon（11 张全状态/诅咒） |
| 费用操作 | **Midnight** | Midnight、Pinpoint、Stomp、BansheesCry、Flatten（减费三件套）；Transfigure（提费+Replay）；SovereignBlade（铸造降费） |
| 生成卡牌 | **BladeDance** | BladeDance、InfernalBlade、Discovery、HiddenGem（+Replay）、Abundance、Jackpot、DualWield、WhiteNoise、Quasar、Distraction、Severance、Splash（跨池选牌）、Outrage、BundleOfJoy、MadScience 等 36 张 |
| 召唤/献祭 Osty | **Bodyguard** | Bodyguard、NecroMastery、Reanimate、Afterlife、Dirge、LegionOfBone、PullAggro、Spur、Cleanse；BoneShards、Sacrifice（Kill 召唤物） |
| 铸造 | **RefineBlade** | RefineBlade、HammerTime、BigBang、Bulwark、Conqueror、SeekingEdge、SpoilsOfBattle、SummonForth、TheSmith、WroughtInWar、BeatIntoShape；SovereignBlade（本体） |
| 充能球 | **BallLightning** | BallLightning、Zap、Chill（逐敌生成）、Fusion、Ignition、Rainbow、Capacitor/Modded（栏位）、Darkness/TeslaCoil（手动被动）、Loop、Dualcast（激发） |
| 任务卡 | **LanternKey** | LanternKey（锁地图事件）、Dowsing（房间计数）、SpoilsMap（地图宝箱+金币）、ByrdonisEgg |
| 可打出条件/出牌限制 | **GrandFinale** | GrandFinale（IsPlayable+金光）、Normality（ShouldPlay veto+红光）、Clash、HighFive；Enthralled（诅咒 veto） |
| 跨战斗持久 | **TheScythe** | TheScythe、GeneticAlgorithm（[SavedProperty]+DeckVersion）、Guilty（AfterCombatEnd 计数自毁） |
| 独特机制 | **MadScience** | MadScience（动态类型卡）、Misery、VoidForm（立即结束回合）、TheBall（多人弃置去向）、Wither（假升级）、FranticEscape（可打出状态）、BladeOfInk（附魔）、Alchemize（生成药水）、Monologue（回写 Power Var）、Melancholy（AfterDeath 费用成长）、Hologram |

## 6. Power（buff/debuff）系统

> 覆盖 `Models/Powers/` 268 个官方 Power（另 18 个 Mocks）。命名空间 `MegaCrit.Sts2.Core.Models.Powers`。
> 名称对照：本章及全文的中文用词已统一为官方 zhs 本地化（如 Vulnerable=易伤、Artifact=人工制品），三语对照表见附录 C（逐键核对 `localization/{zhs|jpn}/powers.json`）。

### 6.1 PowerModel 结构与生命周期

**全量分布**：`Type` 可静态判定 244 处——Buff 200 / Debuff 44（其余为动态判定，见 `GetTypeForAmount`）；`StackType`：Counter 208（层数型）/ Single 39（单实例型）/ None 2。

**关键字段与可重写点**（基类 650 行，详见 2.1）：

| 成员 | 说明 |
|---|---|
| `Amount` / `AmountOnTurnStart` | 层数；后者在回合开始时快照，用于"仅当回合开始时就存在才触发"的判定 |
| `Owner` / `Applier` / `Target` | 挂载者 / 施加者；多人模式按玩家实例化时 `Target` 指向对应玩家 |
| `DynamicVars` / `_internalData` | 公开数值（可入文案、随克隆复制）vs 私有数据（实例隔离、克隆时经 `InitInternalData()` 重建）——见下方 Vigor 范例 |
| `SkipNextDurationTick` | 持续型递减的"跳过一次"标记（怪当回合给玩家上 debuff 时不立即减） |
| `AllowNegative`（5 个） | 允许负层数：Strength / Dexterity / Focus / Shriek / Shrink；归 0 即移除（`ShouldRemoveDueToAmount`） |
| `IsVisibleInternal`（默认 true） | 隐藏 Power（仅 AmbergrisPower）——后台逻辑型 |
| `ShouldScaleInMultiplayer` + `GetScaledAmountForMultiplayer` | 多人缩放（Artifact/Regen 等按人数放大） |
| `OwnerIsSecondaryEnemy` | 次要敌人标记（Minion 爪牙） |
| `ShouldPowerBeRemovedAfterOwnerDeath` / `ShouldOwnerDeathTriggerFatal` | 死亡联动：复活类 Power 可留场；false 则宿主死亡**不触发 Fatal**（Minion） |

**生命周期**（与 3.2 的 PowerCmd 对应）：`PowerCmd.Apply` → `TryModifyPowerAmountReceived`（**人工制品抵消在这层**）→ `Hook.BeforePowerAmountChanged` → `ApplyInternal`（已有实例则叠加，`AllowNegative` 归 0 即移除）→ `BeforeApplied` / `AfterApplied`（**仅首次施加**时跑，叠加层数不跑）→ 战斗中随时可 `ModifyAmount`/`Decrement`/`TickDownDuration` → 移除时 `AfterRemoved`。

**持续回合衰减节律**（易伤/虚弱/脆弱的标准写法）：

```csharp
public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
{
    if (side == CombatSide.Enemy)
        await PowerCmd.TickDownDuration(this);   // 敌方回合结束时 -1
}
```
即 debuff 在**敌方回合结束**时递减；配合 `SkipNextDurationTick` 实现"怪物当回合给我上的 debuff 本回合不扣"。

**ITemporaryPower 实现者**（5 个，"临时/当回合" Power 接口，统计类效果会排除它们）：Illusion、SleightOfFlesh、TemporaryStrength、TemporaryDexterity、TemporaryFocus。机制（凌虐/预备打击的"临时力量"实现）：包装 Power 在 `BeforeApplied`/`AfterPowerAmountChanged` 中镜像同步真正的 `StrengthPower`（经 `InternallyAppliedPower` 声明等价物），宿主回合结束时移除包装并施加 `-Sign×Amount` 精确还原；正负由 `IsPositive` 决定，同一抽象类同时支撑"给自己临时力量"与"给敌人临时减力量"（ManglePower/SetupStrikePower 各只有两行）。

**修改器范例**（Strength 全文见第 2 章示例②）；**InternalData 范例**（Vigor"仅下一次攻击"的实现骨架）：`BeforeAttack` 钩子里把 `AttackCommand` 和当前层数存进 `GetInternalData<Data>()`，由 `ModifyDamageAdditive` 只对记录的那次攻击加成并清空——"一次性强化"用私有数据 + 两个钩子配合即可，不需要额外字段轮询。

### 6.2 数值修改器方法分类（Power 影响战斗的完整手段清单）

「修改器 × Power 实现者」全量矩阵（grep 实测）：

| 修改器 | 实现者（全部列出） |
|---|---|
| `ModifyDamageAdditive`（加伤，8） | Strength、Vigor、Accuracy、Calcify、Leadership、OneForAll、PhantomBlades、Tainted |
| `ModifyDamageMultiplicative`（乘伤，20） | **Vulnerable(×1.5 受击)、Weak(×0.75 造成)**、DoubleDamage、Tank、Slow、Surrounded、Colossus、Covered、Conqueror、Flanking、Flutter、Gigantification、Guarded、Hang、Intercept、Knockdown、Lethality、Shrink、Soar、Tracking |
| `ModifyDamageCap`（伤害上限，2） | Intangible、HardToKill |
| `ModifyBlockAdditive`（加挡，2） | Dexterity、Fasten |
| `ModifyBlockMultiplicative`（乘挡，4） | **Frail(×0.75)**、NoBlock(×0)、Shadowmeld(×2)、Unmovable(×2) |
| `ModifyCardPlayCount`（多次打出，6） | Burst、Duplication、EchoForm、OneTwoPunch、SignalBoost、TagTeam |
| `ModifyCardPlayResultLocation`（去向，4） | Corruption、Feral、Nostalgia、Rebound |
| `ModifyHandDraw`（抽牌数，7） | Clarity、Demesne、DrawCardsNextTurn、MachineLearning、MindRot、ToolsOfTheTrade、Tyranny |
| `ModifyMaxEnergy`（能量上限，4） | Demesne、Friendship、Pyre、WasteAway |
| `ModifyEnergyGain` / `ModifyOrbValue` / `ModifyUnblockedDamageTarget` | 各 1（后者=DieForYou，Osty 重定向唯一来源） |
| `ModifyHpLostAfterOsty`（2） | Intangible（重伤→1）、Slippery |
| `TryModifyPowerAmountReceived`（1） | **Artifact（人工制品抵消）** |
| `TryModifyEnergyCostInCombat`（3） | BorrowedTime、Curious、Tangled |
| `TryModifyKeywordsInCombat`（1） | Hex 系 |
| `ShouldClearBlock`（不清挡，3） | Barricade、Blur、Burrowed |
| `ShouldAllowHitting`（不可被选中，4） | Adaptable、DieForYou、Illusion、Reattach |
| `ShouldStopCombatFromEnding`（战斗不结束，5） | Adaptable、Infested、SteamEruption、Stock、Surprise |
| 其余各 1 | ShouldDraw、ShouldTakeExtraTurn、ShouldPowerBeRemovedOnDeath 等 |

**通知钩子重写 top**（被动触发型 Power 的主战场）：`AfterSideTurnEnd` 56（回合末触发型）、`AfterCardPlayed` 25、`AfterDeath` 19、`AfterApplied` 18、`AfterSideTurnStart` 17、`BeforeCardPlayed` 22、`AfterDamageReceived` 12、`AfterPlayerTurnStart` 9。

### 6.3 常见 buff 速查表

| 名称（中/英） | 类型/Stack | 实现要点 | 官方使用者 |
|---|---|---|---|
| 力量 Strength | Buff/Counter/可负 | `ModifyDamageAdditive`：宿主自己的有力攻击每击 +层数（示例②） | 卡 15 张（Inflame/DemonForm）；怪物 56 处 |
| 敏捷 Dexterity | Buff/Counter/可负 | `ModifyBlockAdditive`：自己出牌加挡 +层数（来源卡主人校验） | 卡 7 张（Footwork）；怪物 3 处 |
| 集中 Focus | Buff/Counter/可负 | 充能球数值加成（经 `ModifyOrbValue` 管道） | BiasedCognition、Defragment、FocusedStrike、Hotfix |
| 活力 Vigor | Buff/Counter（一次性） | `BeforeAttack` 记录下次攻击 + `ModifyDamageAdditive` 加成后清空（InternalData 范例） | Akabeko（遗物）、Patter、Terraforming |
| 人工制品 Artifact | Buff/Counter | `TryModifyPowerAmountReceived` 把可见 Debuff 压 0 → `AfterModifyingPowerAmountReceived` 自减 1；多人缩放 | UnsettlingLamp（遗物）；怪物自上 9 处 |
| 再生 Regen | Buff/Counter | `BeforeSideTurnEndEarly`：回血 Amount 后自减 1 | RegenPotion（药水） |
| 荆棘 Thorns | Buff/Counter | `BeforeDamageReceived`：被有力攻击命中 → 反伤 `Unpowered\|SkipHurtAnim` | Caltrops、Abrasive、BronzeScales、LiquidBronze；怪物 4 处 |
| 无实体 Intangible | Buff/Counter | `ModifyHpLostAfterOsty`（HP 流失→1）+ `ModifyDamageCap`（格挡/预览同步→1）双点实现 | Apparition、WraithForm、GhostInAJar（药水） |
| 覆甲 Plating | Buff | 回合末 +X 格挡（`BlockProps.nonCardUnpowered`，不吃敏捷） | 怪物 6 处 |
| 爪牙 Minion | Buff/Single | `ShouldOwnerDeathTriggerFatal=false` + 死亡移除特化 + 隐藏 VFX + 次要敌人 | 怪物召唤物 6 处 |

### 6.4 常见 debuff 速查表

| 名称（中/英） | 类型/Stack | 实现要点 | 官方使用者 |
|---|---|---|---|
| 易伤 Vulnerable | Debuff/Counter | `ModifyDamageMultiplicative`：受击 ×1.5（`IsPoweredAttack` 门槛；PaperPhrog/Cruelty/Debilitate 可改倍率）；敌方回合末 -1 | 卡 17 张（Bash/Thunderclap）；怪物 9 处 |
| 虚弱 Weak | Debuff/Counter | `ModifyDamageMultiplicative`：宿主造成 ×0.75（dealer 侧判定） | 卡 16 张（Uppercut）；怪物 12 处 |
| 脆弱 Frail | Debuff/Counter | `ModifyBlockMultiplicative`：加挡 ×0.75 | Shame（诅咒）；怪物 15 处 |
| 中毒 Poison | Debuff/Counter | `AfterSideTurnStart`：受 `Amount` 点不可格挡无力伤害后自减 1（循环触发）；Accelerant 增加触发次数 | 卡 7 张（DeadlyPoison/BouncingFlask）；毒系 Power |
| 灾厄 Doom | Debuff/Counter | 层数存于目标，"死亡时爆发"类联动 | 卡 5 张（TimesUp 按层数计伤） |
| 减益通用写法 | — | 三大 debuff 共用骨架：`DynamicVar` 存倍率 + 修改器内 `Owner`/`target`/`dealer` 侧判定 + `IsPoweredAttack()` 门槛 + 敌方回合末 `TickDownDuration` | — |
| 允许负层数的 Debuff 化 | — | Strength/Dexterity/Focus `AllowNegative`：被压到负数即变 debuff（`GetTypeForAmount` 负值翻转为 Debuff 显示） | 给力量上负值的卡：Friendship、Malaise、Resonance、SharedFate |

### 6.5 怪物专属 Power 与意图联动

**怪物最常上的 Power**：Strength 56 / Frail 15 / Weak 12 / Vulnerable 9 / Artifact 9——怪物大量复用玩家侧通用 Power；真正专属的多是"出招机制"型：SteamEruption(6)、Plating(6)、Minion(6)、Ritual(3)、BattlewornDummyTimeLimit(3)。

**例 1：AsleepPower（沉睡→受击/倒计时唤醒，Lagavulin 系）**——怪物 Power 与意图联动的完整范例：

```csharp
public sealed class AsleepPower : PowerModel
{
    public override async Task AfterDamageReceived(...)   // 受击即醒
    {
        if (target == base.Owner && result.UnblockedDamage != 0)
        {
            // 移除睡眠期护甲(Plating) → 播放唤醒音效/动画 → 置 monster.IsAwake
            await CreatureCmd.Stun(base.Owner, monster.WakeUpMove, "SLASH_MOVE"); // 醒来当回合改执行 WakeUpMove
            await PowerCmd.Remove(this);
        }
    }
    public override async Task AfterSideTurnEnd(...)      // 倒计时自然醒
    {
        await PowerCmd.Decrement(this);
        if (base.Amount <= 0) await lagavulinMatriarch.WakeUpMove(...);
    }
}
```
要点：唤醒动作通过 `CreatureCmd.Stun(creature, 自定义出招委托, 下一招id)` 注入"这一回合执行什么"；睡眠期免伤用 Plating 配合、醒来/受击时移除。

**例 2：RitualPower（仪式，每回合末 +力量）**——`AfterApplied` 里用私有布尔 `WasJustAppliedByEnemy`（setter 内 `AssertMutable()`）记录施加来源，`AfterSideTurnEnd` 据此每回合 `PowerCmd.Apply<StrengthPower>`；"Power 定期给自己上别的 Power"的模板。

**例 3：MinionPower（爪牙）**——Single 实例 + `ShouldPlayVfx=false` + `OwnerIsSecondaryEnemy`（不计入主敌人位）+ 死亡不触发 Fatal、移除规则特化——召唤物类怪物的标配。

**意图联动要点**：怪物 Power 普遍配合 `CreatureCmd.Stun`（含自定义出招委托）改写出招节奏；`ShouldStopCombatFromEnding`（5 个 Power）用于"全灭但还有增援"的演出；怪物意图本身属 MonsterMoves 体系，本文档不展开。

## 7. 药水效果实现

### 7.1 PotionModel 生命周期与目标校验

> 65 瓶全部精读完毕（共 2347 行，单瓶平均 36 行——药水是官方内容里最小的创作单元）。药水没有升级概念，效果主体就是单个 `OnUse`。

**三个 abstract 声明 + 一个效果入口**：`Rarity` / `Usage`（CombatOnly 59 / AnyTime 5 / Automatic 1）/ `TargetType`（AnyPlayer 51 / AnyEnemy 9 / AllEnemies 3 / Self 1 / 动态 1）+ `OnUse(choiceContext, target)`。

**OnUseWrapper 管线**（基类已实现，Mod 只写 `OnUse`）：`RemoveBeforeUse()`（先从腰带移除）→ `Hook.BeforePotionUsed` → **自动投掷 VFX**（`NItemThrowVfx` 从施用者飞向目标，约 0.5s；战斗外跳过）→ **`OnUse`** → `Hook.AfterPotionUsed` + 战斗历史。所以药水本体代码里一般不再写投掷动画，只补落点特效 `NCombatRoom.Instance?.PlaySplashVfx(target, color)`。

**目标校验与卡牌不同**（勿复用逻辑）：药水 `Self` 也传目标（`EnqueueManualUse` 会兜底 `Owner.Creature`）；`AnyAlly` 不能选自己；`AnyPlayer`＝任意玩家（**多人友好的默认值**，单人时即自己）；`TargetedNoCreature`＝选一个"位置"而非生物（商店场景的 FoulPotion，TargetType 可按战斗内外动态返回不同值）。惯例：`OnUse` 首行 `PotionModel.AssertValidForTargetedPotion(target)` 保证目标型药水拿到目标。

**其他开关**：`PassesCustomUsabilityCheck`（自定义可用性，FoulPotion 限定战斗/商店/假商人事件）；`CanBeGeneratedInCombat => false` 防止战斗中随机生成（FairyInABottle/FruitJuice/RegenPotion——会改变资源上限的药水）。

**标准骨架**（`StrengthPotion` 全文，全库约半数药水都是这个模板的填空题）：

```csharp
public sealed class StrengthPotion : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<StrengthPower>(2m) };
    public override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<StrengthPower>() };

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PotionModel.AssertValidForTargetedPotion(target);
        NCombatRoom.Instance?.PlaySplashVfx(target, new Color("fd2155"));
        await PowerCmd.Apply<StrengthPower>(choiceContext, target, base.DynamicVars.Strength.BaseValue, base.Owner.Creature, null);
    }
}
```

### 7.2 药水效果模式分组表（65 瓶全量归并）

| 模式 | 代表（精讲） | 组内其他药水 | 关键 API |
|---|---|---|---|
| 上玩家 buff | **StrengthPotion**（7.1 全文） | DexterityPotion(敏捷2)、SpeedPotion(敏捷5,过期收回)、FocusPotion(集中2)、LiquidBronze(荆棘3)、HeartOfIron(覆甲7)、RegenPotion(再生5)、LuckyTonic(缓冲)、MazalethsGift(仪式Power)、GhostInAJar(无实体1)、GigantificationPotion、StableSerum(保留手牌2回合)、RadiantTincture(能量1+Radiance3)、FyshOil(力量1+敏捷1)、Ambergris(50%回血+附魔Power) | `PowerCmd.Apply<T>` + `PowerVar<T>`；变体 Power 实现"过期收回"（FlexPotionPower/SpeedPotionPower） |
| 上敌方 debuff | **VulnerablePotion**（易伤3，同模板） | WeakPotion(虚弱3)、PoisonPotion(毒6)、PotionOfDoom(灾厄33)、BeetleJuice(缩小4)、PowderedDemise(9)、PotionOfBinding(全体虚弱+易伤)、ShacklingPotion(全体力量-7) | 同上；全体版对 `CombatState.HittableEnemies` 施加 |
| 抽牌/手牌/牌堆操作 | **SwiftPotion**（抽3，最纯） | Clarity(抽1+集中Power)、BottledPotential(手牌洗回+抽5)、DropletOfPrecognition(抽牌堆选1入手)、LiquidMemories(弃牌堆选1免费入手)、GamblersBrew(弃任意抽同数)、DistilledChaos(自动打出堆顶3)、GlowwaterPotion(弃光手牌抽10)、SneckoOil(抽7+全手牌随机0~3费)、Ashwater(弃置任意张手牌)、TouchOfInsanity(选1张手牌本战斗免费) | `CardPileCmd.Draw` / `CardSelectCmd.FromCombatPile` / `CardCmd.DiscardAndDraw` / `AutoPlayFromDrawPile` / `EnergyCost.SetThisTurnOrUntilPlayed` / `SetToFreeThisCombat` |
| 能量/辉星 | **EnergyPotion**（+2能量） | CureAll(能量1+抽2——注意：**不是**清除 debuff)、StarPotion(辉星3) | `PlayerCmd.GainEnergy` / `GainStars` |
| 回复/上限 | **BloodPotion**（20%回血，AnyTime） | FruitJuice(+5上限，AnyTime) | `CreatureCmd.Heal(MaxHp*百分比)` / `GainMaxHp` |
| 投掷伤害 | **FirePotion**（单体20，`DamageVar(20, Unpowered)`） | ExplosiveAmpoule(全体10)、PotionShapedRock(15, Token) | `CreatureCmd.Damage` + `ValueProp.Unpowered`（药水伤害不吃力量） |
| 生成卡牌 | **AttackPotion**（攻击池三选一免费塞手） | SkillPotion、PowerPotion、ColorlessPotion(无色池)、CosmicConcoction(无色3张全升级)、OrobicAcid(攻/技/力各1张全免费)、CunningPotion(升级Shiv×3)、PotOfGhouls(Soul×2) | `CardFactory.GetDistinctForCombat` + `CardSelectCmd.FromChooseACardScreen` + `SetToFreeThisTurn` + `AddGeneratedCardToCombat`（与卡牌生成四连完全一致，见 5.8-S1）；静态工厂 `Shiv.CreateInHand` |
| 格挡 | **BlockPotion**（12挡，`BlockVar(12, Unpowered)`） | ShipInABottle(10挡+下回合再10挡)、Fortifier(格挡变为三倍) | `CreatureCmd.GainBlock` + `BlockProps` 不吃敏捷口径 |
| 战斗资源/召唤/充能球/铸造 | **KingsCourage**（铸造15） | BoneBrew(召唤15)、PotionOfCapacity(充能球栏位+2)、EssenceOfDarkness(充满暗珠)、BlessingOfTheForge(升级全手牌)、SoldiersStew(全场Strike `BaseReplayCount++`) | `ForgeCmd.Forge` / `OstyCmd.Summon` / `OrbCmd.AddSlots` / `Channel` / `CardCmd.Upgrade` |

**药水名 → 模式反查**：上表"组内其他药水"列即映射（各药水括号内为效果速记）；65 瓶 = 上buff 17（含 Ambergris/Duplicator 两个跨组） + 上debuff 8 + 抽牌/手牌/牌堆 11 + 能量辉星 3 + 回复 2 + 投掷 3 + 生成卡 8 + 格挡 3 + 资源特殊 6 + 特殊逐例 3（见 7.3）+ 占位 1（DeprecatedPotion，Rarity=None 无效果）= 65。

### 7.3 特殊药水逐例（模式表装不下的）

**FairyInABottle（死亡复活，全库唯一 `Automatic` 药水）**——"不可主动使用的药水"的实现：

```csharp
public sealed class FairyInABottle : PotionModel
{
    public override PotionUsage Usage => PotionUsage.Automatic;    // 不可主动使用（2.3 枚举注）
    public override TargetType TargetType => TargetType.Self;
    public override bool CanBeGeneratedInCombat => false;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
        => await CreatureCmd.Heal(target, Math.Max((decimal)target.MaxHp * 0.3m, 1m));

    public override bool ShouldDie(Creature creature)
        => creature != base.Owner.Creature;   // 宿主死亡时否决（Should* 谓词，见 2.2.10）

    public override async Task AfterPreventingDeath(Creature creature)
        => await OnUseWrapper(new ThrowingPlayerChoiceContext(), creature);  // "复活"＝手动跑一遍完整使用流程
}
```
要点：复活的本质是 **`ShouldDie` 否决 + `AfterPreventingDeath` 里调 `OnUseWrapper`**——药水的效果复用、移除、历史记录全部走正常使用管线；`CreatureCmd.Kill(force:true)` 可绕过它（3.4）。

**EntropicBrew（填满药水栏，AnyTime）**：`while (targetPlayer.HasOpenPotionSlots)` 循环 `PotionFactory.CreateRandomPotionOutOfCombat(...).ToMutable()` + `PotionCmd.TryToProcure`（失败即 break——药水栏满会失败返回，见 3.5）；生成随机药水的标准两连。

**FoulPotion（169 行，最长药水；战斗内外双形态）**：`TargetType` 动态返回（战斗=AllEnemies / 局外=TargetedNoCreature）；`PassesCustomUsabilityCheck` 限定"战斗中或商店/假商人事件"；`OnUse` 内三分支——战斗=对所有非宠物生物泼 12 伤；商店=向商人按钮泼 VFX + `PlayerCmd.GainGold(100)`；假商人事件=多玩家同步触发事件回调。展示了"药水可以作用于战斗外世界"的完整姿势（事件侧钩子由事件模型提供）。

**Duplicator**——初看以为机制特殊，实际实现就是标准模板：`PowerCmd.Apply<DuplicationPower>(ctx, target, 1m, target, null)`（下一张卡打两次），已归入 7.2 上 buff 系。

**其余全部 61 瓶均落入 7.2 的九个模式**，无额外特例。

## 8. 遗物效果实现

### 8.1 RelicModel 生命周期与属性开关

> 300 件遗物（无 Mocks），共 15635 行（平均 52 行/件）。遗物 `ShouldReceiveCombatHooks => true`——**战斗内外全程在场**的钩子监听者，这是它与卡牌（只在战斗牌堆中监听）最大的不同。

**专属生命周期只有两个**：`AfterObtained()`（84 件重写——最大入口，拾取/事件给遗物时触发）与 `AfterRemoved()`（**0 件重写**——官方没有"移除时生效"的遗物）。没有"每场战斗重置"的虚方法，战斗内状态靠钩子时机自己管理。

**属性开关全量统计**（重写件数）：

| 开关 | 件数 | 语义 |
|---|---|---|
| `HasUponPickupEffect` | 50 | 声明"拾取即生效"（配合 `AfterObtained` 做一次性效果；此类遗物不可交易） |
| `ShowCounter` / `DisplayAmount` | 38 / 36 | 计数器显示（改值后调 `InvokeDisplayAmountChanged()`） |
| `IsUsedUp` | 10 | 有限次数用尽（UI 显示"已用完"；用尽时通常置 `Status = RelicStatus.Disabled`） |
| `MerchantCost` | 9 | 商店定价（默认按稀有度 175/225/275/200，Ancient/Starter/Event 不可售） |
| `IsAllowedInShops` / `FlashSfx` / `SpawnsPets` / `IsStackable` / `AddsPet` | 5/6/3/2/2 | 进商店白名单 / 触发音效 / 生成宠物 / 可堆叠（`IncrementStackCount`）/ 附带宠物 |
| `IsAllowed(IRunState)` / `IsAllowedAtNeow` | 20 / 2 | 生成条件（基类给 helper `IsBeforeAct3TreasureChest`：第三章宝箱后停出"减益型"遗物） |

**其他基类设施**：`[SavedProperty]` 持久化计数（跨存档）；`Flash(targets)` 触发图标闪光；`Status`（Normal/Active/Disabled 驱动图标着色）；`IsWax`（蜡封，Wax Choker 机制）/`IsMelted`（熔毁后失效）；`MerchantCost` 见上。

### 8.2 触发时机 × 遗物数量分布（全量统计）

**通知钩子**（async+sync 合并件数；≥3 件的）：

| 钩子 | 件数 | 钩子 | 件数 |
|---|---|---|---|
| `AfterObtained`（拾取） | 84 | `AfterCombatVictory(+Early)` | 6 |
| `AfterCombatEnd`（**最大族群**） | 40 | `AfterDamageReceived` | 6 |
| `AfterRoomEntered`（进房=战斗开始前置） | 33 | `BeforeCardPlayed` / `AfterCardExhausted` / `AfterCardChangedPiles` | 5/4/4 |
| `AfterSideTurnStart`（回合开始） | 29 | `AfterSideTurnEnd` / `AfterEnergyReset` / `AfterBlockCleared` | 各 5/4/3 |
| `AfterCardPlayed` | 27 | `AfterModifyingHpLostAfterOsty` | 3 |
| `BeforeSideTurnStart`（回合开始前） | 21 | `AfterPotionUsed` / `AfterShuffle` / `AfterStarsSpent` / `AfterPlayerTurnStartLate` / `AfterCardDiscarded` | 各 2 |
| `BeforeCombatStart` | 19 | `BeforeHandDraw` | 8 |
| `AfterPlayerTurnStart(+Late)` | 13 | 其余约 35 个钩子 | 各 1 |

**数值修改器/Try/Should 重写**：`ModifyMaxEnergy` 12（能量遗物群）、`ModifyHandDraw` 11、`TryModifyCardRewardOptionsLate` 9、`TryModifyRewards` 6、`TryModifyRestSiteOptions` 5、`ModifyMerchantCardCreationResults` 4、`ModifyDamageAdditive` 4、`TryModifyCardBeingAddedToDeck` 4、`ModifyDamageMultiplicative` 3、`ModifyBlockMultiplicative` 3、`ModifyMerchantPrice`/`ModifyHpLostAfterOsty`/`ModifyGoldGained`/`ModifyUnknownMapPointRoomTypes`/`ShouldFlush` 各 2、另有约 18 个修改器/谓词各 1 件（含 `ModifyPowerAmountGivenMultiplicative`=UnsettlingLamp、`ShouldClearBlock`=SturdyClamp、`ShouldProcurePotion`=Sozu 等）。

**解读**：遗物的"触发时机"天然分散——战斗结束类（40+6）与回合类（21+29+13+9）是两大主流；"持续数值修正"型（改上限/抽牌数，不走钩子走 Modify*）合计 20+ 件。

### 8.3 各触发类别实现模式与代表遗物

**拾取即生效（50 件声明 + AfterObtained 84）**——最简模式，`Strawberry` 全文级摘录：

```csharp
public sealed class Strawberry : RelicModel
{
    public override bool HasUponPickupEffect => true;      // 拾取即生效标记（UI/交易规则用）
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new MaxHpVar(7m) };
    public override async Task AfterObtained()
        => await CreatureCmd.GainMaxHp(base.Owner.Creature, base.DynamicVars.MaxHp.BaseValue);
}
```

**战斗开始上 debuff（BagOfMarbles）**：`BeforeSideTurnStart` + `TurnNumber <= 1` 判断（Akabeko 首回合 Vigor 的三连相同：`participants.Contains(Owner.Creature)` + `Flash()` + `PowerCmd.Apply`，但它挂在 `AfterSideTurnStart` 上）。

**持续数值修正（Ancient 副作用遗物对）**——Ectoplasm/Sozu 都是 `RelicRarity.Ancient` + `EnergyVar(1)` 的"+1 能量换代价"型：

```csharp
// Ectoplasm：+1 能量 / 获得金币归零
public override decimal ModifyMaxEnergy(Player player, decimal amount)
    => player != base.Owner ? amount : amount + base.DynamicVars.Energy.IntValue;
public override decimal ModifyGoldGained(Player player, decimal amount)
    => player != base.Owner ? amount : 0m;
// 配套通知钩子：真改了值才闪光
public override Task AfterModifyingGoldGained(Player player, decimal amount) { Flash(); return Task.CompletedTask; }
// Sozu 则是 ShouldProcurePotion 里 return player != Owner（禁药）
```

**计数器 + 用尽型（三件套组合）**——`MawBank`（Event：进房间 +12 金，购物一次后作废）：

```csharp
[SavedProperty] public bool HasItemBeenBought { get; set { AssertMutable(); ...; if (IsUsedUp) Status = RelicStatus.Disabled; } }
public override bool IsUsedUp => HasItemBeenBought;
public override async Task AfterRoomEntered(AbstractRoom room) { if (BaseRoom==room && !used) { Flash(); GainGold(12); } }
public override Task AfterItemPurchased(Player player, ...) { ...; HasItemBeenBought = true; }
```
计数循环型看 `PenNib`（`[SavedProperty] int AttacksPlayed` 取模计数 + `ShowCounter`/`DisplayAmount => AttacksPlayed % 10` + 命中阈值时改写下一张攻击）；限时型看 `BoneTea`（`CombatsLeft` 倒数，**setter 内同步 `DynamicVars["Combats"].BaseValue` + `InvokeDisplayAmountChanged()` + 用尽置 Disabled**——持久化计数的标准姿势）；条件用尽型看 `SilverCrucible`（Ancient：每宝箱免费拿 N 张，`IsUsedUp` 由 `TimesUsed`/`TreasureRoomsEntered` 两个 `[SavedProperty]` 组合推导）。

**奖励/地图/商店改造类**：`TryModifyCardRewardOptionsLate`（9 件，改卡牌奖励选项）、`TryModifyRewards`（6 件，任意奖励增删）、`ModifyMerchantCardCreationResults`（4 件，商店卡生成）、`ModifyMerchantPrice`（2 件）、`ModifyUnknownMapPointRoomTypes`（2 件，改未知节点可摇房间——与任务卡 LanternKey 同款 API）；战斗结束族群 `AfterCombatEnd`（40 件）多做"本战斗条件计数/转化"。

**Fake\* 假货系列（10 件）**：`FakeAnchor/FakeBloodVial/FakeHappyFlower/FakeLeesWaffle/FakeMango/FakeMerchantsRug/FakeOrichalcum/FakeSneckoEye/FakeStrikeDummy/FakeVenerableTeaSet`——Event 稀有度的"仿制版真遗物"（配合假商人事件出售，`MerchantCost` 自定如 50），钩子形态与真品一致但行为有差异/阉割（如 FakeOrichalcum：无格挡时回合末 +3 挡的两段式 `BeforeSideTurnEndVeryEarly` 记录 → `BeforeSideTurnEnd` 结算）。实现上没有任何特殊基类设施，就是普通遗物+Event 稀有度。

**其余类别代表遗物索引**（来自 2.2 钩子总表实测）：受击计数=BeatingRemnant、弃牌伤=Tingsha、消耗联动=BurningSticks、攻击联动=ArtOfWar/BoneFlute、格挡清除=CaptainsWheel、进房词条=BigMushroom（33 件族群）、复活=LizardTail、辉星=GalacticDust、召唤联动=BoundPhylactery、Power 施加量改写=UnsettlingLamp、不清格挡=SturdyClamp、地图任务=SpoilsMap 配套的 ModifyGeneratedMap（2 件）。

**描述全文扫描补遗**（306 条遗物描述逐条核对，2026-09-11）：值得追加研究的代表——冰淇淋（能量不清空：`ShouldPlayerResetEnergy => false`）、符文金字塔（回合结束不弃手牌：`ShouldFlush => false`）、宾邦 BingBong（往牌组加牌时额外复制一张：`AfterCardChangedPiles` 检测进牌组后克隆塞回）、律动残余（一回合 HP 流失上限 20：`ModifyHpLostAfterOsty` + 每回合未格挡伤害累计）、恶魔之舌（每回合首次失去生命回复等量）、历史课 HistoryCourse（回合开始复现上一回合最后打出的攻击：历史查询 + 克隆 + AutoPlay）、领主阳伞（遇见商人全拿在售物品）、图伊盒 ToyBox（发放蜡制遗物并逐场融化）。**结论：遗物层几乎不引入新机制，全部是 §2.2 钩子与 §3 命令的组合**。

## 9. 官方 API → 本项目（RitsuLib Mod）用法映射

> 对照物：`Scripts/MoWang/`（魔王角色现有实现）。核心结论：**效果执行 API 与官方完全一致，差异只在"注册/资源/本地化"三件事**——RitsuLib 用特性注册替代官方的 ModelDb 硬编码，用 AssetProfile 约定资源路径。

### 9.1 注册与基类对照

| 内容 | 官方写法 | 本项目写法（RitsuLib） |
|---|---|---|
| 卡牌类 | `public sealed class Bash : CardModel`（构造函数传参） | `[RegisterCharacterStarterCard(typeof(MoWang), 4)]` / `[RegisterCard(...)]` + **主构造函数**继承 RitsuLib 模板：`class MwStrike() : MwHarmonyCardModel(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)` |
| 卡牌基类 | `CardModel` | RitsuLib 脚手架 `StellaCardModel`（本项目包一层 `MwCardModel`，`[RegisterCard(typeof(MoWangCardPool), Inherit = true)]` 挂在抽象基类上让子类自动入池）；秘纹主题子类 `HarmonyCardModel` / `DiscCardModel` 承载音符机制 |
| 遗物 | `class X : RelicModel` + `override RelicRarity Rarity` | `[RegisterRelic(typeof(MoWangRelicPool), Inherit = true)]` 的 `MwRelicModel(RelicRarity relicRarity) : ModRelicTemplate`；图片经 `RelicAssetProfile` 约定到 `res://SlayTheStella/images/relics/{类名}.png`（含 `_outline`/`_big` 变体） |
| 药水 | `class X : PotionModel`（三 abstract 属性） | `[RegisterPotion(...)]` 的 `MwPotionModel(PotionRarity, PotionUsage, TargetType) : ModPotionTemplate` + `PotionAssetProfile` |
| 角色/池 | 官方角色硬编码 | `[RegisterCharacter]` 的 `MoWang` + `MoWangCardPool/RelicPool/PotionPool` |
| 本地化 | 官方 `cards/{Id}.title` | 键格式 `SLAY_THE_STELLA_CARD_{ID}.title/.description`（zhs/eng/jpn 三份 JSON，见 `Tools/Check-ContentConsistency.ps1` 门禁） |

### 9.2 效果执行 API（零差异，直接 `using MegaCrit.Sts2.Core.*`）

`DamageCmd.Attack` 链、`PowerCmd.Apply<T>`、`CreatureCmd.GainBlock/Damage/Heal`、`CardPileCmd.Draw/AddGeneratedCardToCombat`、`PlayerCmd.GainEnergy`、`ForgeCmd.Forge`、`OrbCmd.Channel`、`DynamicVars`/`UpgradeValueBy`、`AttackContext`……官方第 3、4 章的全部 API 在 mod 中原样可用。唯一注意：数值统一读 `BaseValue`（4.1）。计算型数值是例外：声明用 RitsuLib `ModCardVars.ComputedDamage` 等（预览自动走官方修正管道），效果里用 `DynamicVars.EvaluateValueOrDefault(名, target:)` 读取——官方 `CalculatedDamageVar` 三件套不推荐（见 4.4 尾注与 RitsuLib 教程 19）。

**本项目范本 `MwStrike`**（初始打击，含 mod 特有的"协奏附加段"与 AttackContext 实战）：

```csharp
[RegisterCharacterStarterCard(typeof(MoWang), 4)]
public sealed class MwStrike() : MwHarmonyCardModel(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];          // C# 13 集合表达式
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move),
        new DamageVar("HarmonyDmg", 1m, ValueProp.Move)                            // 具名 Var：协奏附加段
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        // AttackContext：把"主伤害 + 协奏附加伤害"合并为一次攻击——
        // Vigor 这类按攻击命令消耗的 buff 对两段生效且只消耗一次（3.1）
        await using (AttackContext attack = await AttackCommand.CreateContextAsync(CombatState!, choiceContext, cardPlay))
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Attack", Owner.Character.AttackAnimDelay);
            attack.AddHit(await CreatureCmd.Damage(choiceContext, cardPlay.Target, DynamicVars.Damage, this, cardPlay));
            if (CheckNoteRequire(cardPlay.Player) > 0)                             // 音符条件（mod 扩展，挂在卡基类）
                attack.AddHit(await CreatureCmd.Damage(choiceContext, cardPlay.Target,
                    (DamageVar)DynamicVars["HarmonyDmg"], this, cardPlay));
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
    public override IReadOnlyList<(NoteType Note, int Amount)> GetNoteRequire() => [(NoteType.Pummel, 1)];
}
```

### 9.3 mod 特有扩展的挂载位置

| mod 机制 | 挂载点 | 参照 |
|---|---|---|
| 音符/秘纹（协奏） | `MwHarmonyCardModel`/`MwDiscCardModel` 子类（`GetNoteRequire`/`CheckNoteRequire`/`GetDiscNote`/`GainNoteAfterPlay`） | MwStrike/MwDefend/MwTracesOfStarlight |
| 战斗内自定义 UI | RitsuLib 次级资源注册表 `RegisterCombatUi(...)`（`Scripts/Shared/SecondaryRes/ResNotes.cs`） | AGENTS.md |
| 多段伤害合并攻击 | `AttackContext.CreateContextAsync` + `AddHit`（官方几乎未用但完全公开） | 3.1、MwStrike |
| 技能卡追加悬停提示 | RitsuLib `MoreAdditionalHoverTips`（对应官方 `ExtraHoverTips` 的追加式变体） | MwTracesOfStarlight |

> 写新卡 checklist：继承正确的 Mw 基类 → 声明 `CanonicalVars` → `OnPlay` 用 Cmd API → `OnUpgrade` 走 `UpgradeValueBy` → 补三语 JSON 键 → 放卡图 `SlayTheStella/images/cards/{类名}.png` → 跑 `pwsh ./Tools/Check-ContentConsistency.ps1`。

## 附录

### A. 检索命令集（调查中实际验证可用）

```bash
ROOT="_manual/sts2_export/src/Core"
# ⚠️ 本环境 grep -rl 输出带 CRLF：管道把文件名传给后续命令前必须 | tr -d '\r'

# ① 导出 AbstractModel 全部通知钩子
grep -nE "virtual Task [A-Z][A-Za-z]+\(" "$ROOT/Models/AbstractModel.cs"

# ② 某目录 API 调用频次表（Cards 换成 Relics/Potions/Powers 同理）
grep -rhoE "[A-Z][A-Za-z]+Cmd\.[A-Za-z]+" "$ROOT/Models/Cards" --include='*.cs' | sort | uniq -c | sort -rn

# ③ 钩子重写频次（含非 Task 重写）
grep -rhoE "override (async )?(Task|void|bool|decimal|int) [A-Z][A-Za-z]+[\(<]" "$ROOT/Models/Cards" --include='*.cs' | sort | uniq -c | sort -rn

# ④ 修改器 × 实现者矩阵（Powers/Relics 通用）
grep -rlE "override [A-Za-z<>?,. ]*ModifyDamageAdditive\(" "$ROOT/Models/Powers"/*.cs   # 换修改器名即可

# ⑤ 按钩子找使用者（例：哪些遗物在战斗开始做事）
grep -rl "Task BeforeCombatStart(" "$ROOT/Models/Relics" --include='*.cs'

# ⑥ 基础枚举分布（TargetType/CardType/PowerType/StackType 同理）
grep -h ": base(" "$ROOT/Models/Cards"/*.cs | grep -oE "TargetType\.[A-Za-z]+" | sort | uniq -c

# ⑦ 谁在使用某个 Power（找代表卡）
grep -rlE "Apply<VulnerablePower>|PowerVar<VulnerablePower>" "$ROOT/Models" --include='*.cs'

# ⑧ 体量长尾（结构异常兜底）
find "$ROOT/Models/Cards" -name '*.cs' -not -path '*Mocks*' | xargs wc -l | sort -rn | head -25

# ⑨ OnUpgrade 不走标准升级调用的卡（注意用 -E，本环境 ugrep 对 \| 处理不同）
for f in $(grep -rl "override void OnUpgrade" "$ROOT/Models/Cards" --include='*.cs' | grep -v Mocks | tr -d '\r'); do
  grep -Eq "UpgradeValueBy|UpgradeStarCostBy|EnergyCost\.UpgradeBy" "$f" || echo "$f"
done

# ⑩ 为每个钩子找典型使用者（实测脚本，h 换成钩子名）
for h in AfterCombatEnd AfterSideTurnStart; do
  echo "$h | 卡:$(grep -rl "Task $h(" "$ROOT/Models/Cards" --include='*.cs' | head -1) 遗物:$(grep -rl "Task $h(" "$ROOT/Models/Relics" --include='*.cs' | head -1)"
done
```

### B. 反编译源码阅读注意事项

- **编译器噪音标识符对照**：`_003C_003Ez__ReadOnlySingleElementList<T>`＝单元素集合（读作 `new T[]{ x }`）；`_003C_003Ez__ReadOnlyArray<T>`＝数组包装（读作 `new T[]{...}`）；`IL_xxxx` 标签 + `goto`＝编译器把 switch/模式匹配/展开降低后的产物，按控制流读；`num/num2/flag/item2` 等自动命名忽略。
- **注释是完整的**：反编译保留了全部 XML doc（`<summary>`/`<param>` 与 `cref` 引用），官方注释频繁点名"该用哪个钩子/别用哪个"，是第一手资料。
- **克隆管线噪音**：各模型 `AfterCloned()` 里把事件字段置 null、`DeepCloneFields()` 深拷引用、`NeverEverCallThisOutsideOfTests_*`——都是框架代码，不是业务逻辑。
- **Mocks/ 目录**：各 Models 子目录下的 `Mocks/`（MockCard/MockPotion/MockRelic/MockPower 等，共约 30 文件）仅供测试，**统计时必须排除**；它们引入了官方内容没有的抽象成员（如 `MockCardModel.GetBaseBlock()`），grep 时会混入假阳性。
- **Fake\* 系列**：`Models/Relics/` 下 10 件 Event 稀有度"仿制遗物"，配合假商人事件，无特殊机制（见 8.3）。
- **Multiplayer Net\* 系列**：`HookPlayerChoiceContext`/`LocalContext.NetId`/`GameActionType`/`AssignTaskAndWaitForPauseOrCompletion` 等是联机同步管线；`CardMultiplayerConstraint.MultiplayerOnly` 卡与 `DrawWithoutBlockingOnOtherPlayers` 是仅有的联机特化点。本次调查只记录结论不展开；机制复杂的自动出牌（AutoPlay）同理。
- **官方本地化就在导出目录里**：`sts2_export/localization/` 下有 17 种语言的完整官方 JSON（`powers.json` / `cards.json` / `card_keywords.json` / `relics.json` / `potions.json` 等，键名＝大写类名，如 `VULNERABLE_POWER.title`）——查官方译名、校对 mod 三语本地化直接 grep 这里，不必联网（附录 C 即出自此处）。
- **本次未展开的相邻体系**（同一钩子机制的消费者，需要时可按同法调查）：`Models/Orbs/`（充能球）、`Models/Enchantments/`（附魔）、`Models/Afflictions/`（畸变）、`Models/Modifiers/`（周目修饰符）、`Models/Monsters/`（怪物出招）。

### C. 术语三语对照表（官方本地化逐键实测）

> 来源：`_manual/sts2_export/localization/{zhs|eng|jpn}/` 下的官方 JSON（导出自带 17 种语言，键名＝大写类名，如 `VULNERABLE_POWER.title`）。下表逐键核对，正文用词已统一为官方 zhs 名。

**核心 Power**：

| zhs（官方） | eng | jpn（官方） | 一句话机制 |
|---|---|---|---|
| 易伤 | Vulnerable | 弱体 | 受击 ×1.5 |
| 虚弱 | Weak | 脱力 | 造成 ×0.75 |
| 脆弱 | Frail | 脆弱 | 加挡 ×0.75 |
| 力量 | Strength | 筋力 | 每击加伤（可负） |
| 敏捷 | Dexterity | 敏捷 | 加挡（可负） |
| 集中 | Focus | 集中力 | 充能球数值（可负） |
| 人工制品 | Artifact | アーティファクト | 抵消负面效果 |
| 再生 | Regen | 再生 | 回合末回血后 -1 |
| 中毒 | Poison | 毒 | 回合开始结算 |
| 无实体 | Intangible | 霊体 | HP 流失→1 |
| 荆棘 | Thorns | トゲ | 受击反伤 |
| 活力 | Vigor | 活力 | 下次攻击加成 |
| 灾厄 | Doom | 破滅 | 层数存于目标，死亡类联动 |
| 覆甲 | Plating | プレート | 回合末 +格挡 |
| 爪牙 | Minion | ミニオン | 召唤物标配 |
| 仪式 | Ritual | 儀式 | 每回合末 +力量 |
| 缓冲 | Buffer | バッファー | LuckyTonic 赠送 |
| 缩小 | Shrink | 縮小 | 乘伤类修改器实现者（可负） |
| 摧残 | Debilitate | 衰弱 | 改易伤/虚弱倍率 |

**卡牌关键词**：

| zhs（官方） | eng | jpn（官方） |
|---|---|---|
| 消耗 | Exhaust | 廃棄 |
| 保留 | Retain | 保留 |
| 虚无 | Ethereal | エセリアル |
| 固有 | Innate | 天賦 |
| 奇巧 | Sly | スライ |
| 永恒 | Eternal | 永劫 |
| 不能被打出 | Unplayable | プレイ不可 |

**机制动词 / 提示词**（出处：`localization/*/static_hover_tips.json`）：

| 官方 zhs | eng | 说明 |
|---|---|---|
| 生成 | Channel | 把充能球放进第一个空栏位（满则自动激发第一个） |
| 激发 | Evoke | 消耗充能球触发其效果（jpn 解放） |
| 铸造 | Forge | 每场战斗首次铸造时获得君王之剑 |
| 斩杀 | Fatal | 这张牌杀死一名非爪牙敌人时触发 |
| 击晕 | Stun | 该敌人下一回合无法行动 |

> ⚠️**与社区习惯叫法差异较大的官方名**（写 mod 本地化以官方名为准）：人工制品（旧译"神器"）、无实体（旧译"无形"）、灾厄（旧译"毁灭"）、覆甲（旧译"铁甲"）、集中（旧译"专注"）、奇巧（旧译"灵巧"）、固有（旧译"先天"）、仪式（旧译"献祭"）、缩小（旧译"萎缩"）。本文档正文已全部统一为官方名。

**专有名词官方名**（同出自本地化 JSON）：Osty＝**奥斯提**（jpn オスティ，`OSTY.name`）；Stars＝**辉星**（jpn スター，`STAR_COUNT.title`，储君（Regent）角色的专属资源）；Orb＝**充能球**（jpn オーブ，栏位叫"充能球栏位"、Channel 动词官方叫"生成"）；Sovereign Blade＝**君王之剑**（jpn ソヴリン・ブレード，`SOVEREIGN_BLADE.title`，铸造型卡牌的核心卡）。

**《星塔旅人》术语衔接**（模组主题词，均已三语核对——查证方式：`grep -F "词条" _manual/stella-sora-glossary.md`，行区间见该文件目录索引）：

| 本项目概念 | 星塔旅人术语（中/英/日） | glossary 位置 |
|---|---|---|
| 模组角色 MoWang | 魔王 = The Tyrant = 魔王 | §1.1（L79-83） |
| 占位遗物 Vita | 维塔 = Vita = ヴェータ | §2.1（L136-140） |
| 秘纹卡机制 | 秘纹 = Disc = ロスレコ；音符 = Musical Note | §3.1/§3.5（L155-172、L340-361） |
| `NoteType.Pummel` / `.Stamina` | 强攻之音 = Melody of Pummel = 強撃の音符 / 体力之音 = Melody of Stamina = 体力の音符 | §3.5（L340-361） |
