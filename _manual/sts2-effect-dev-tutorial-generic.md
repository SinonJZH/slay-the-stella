# 杀戮尖塔 2 效果实现教程 —— 卡牌 / 遗物 / 药水 / Power 怎么写

> **写在前面**：这是我和 AI 斗智斗勇了 6 个小时后弄出来的教程。
>
> **这份教程讲什么**：只讲一件事——**效果代码怎么写**。从"造成 6 点伤害"到"越打越强""复活""每有一张打击牌伤害 +2"，
> 每种效果给出可以直接抄进项目改名的完整代码，并解释为什么这么写。
>
> **这份教程不讲什么**：mod 环境搭建、新文件该放哪、本地化 JSON 的基本格式、调试与热重载——这些
> [官方中文 mod 教程仓库](https://github.com/GlitchedReme/SlayTheSpire2ModdingTutorials)（下称《官方教程》）已经讲得很清楚了
> （推荐先读 `Basics/01-环境配置`、`BaseLib/01-添加卡牌`、`Basics/05-变量与描述`、`Basics/07-快速调试&热重载`）。
>
> 文中所有中文术语（人工制品、集中、奇巧、辉星……）均为**游戏官方简中本地化用词**（以游戏内实际显示为准）。

---

## 0. 开工前：十分钟背景知识

### 0.1 三层世界观（理解了这个，效果代码就不再神秘）

把游戏想成一家餐厅：

| 层 | 是什么 | 你写的代码属于这层吗 |
|---|---|---|
| **模型层**（Models） | **菜单上的菜**：每张卡/遗物/药水/增益是一个类，用属性声明自己的数值和名字 | ✅ 你 99% 的代码在这层 |
| **命令层**（Commands） | **后厨**：`DamageCmd.Attack(...)`、`PowerCmd.Apply<T>(...)` 这些静态方法是唯一允许"动菜"的方式 | ✅ 你在模型层调用它们 |
| **钩子层**（Hooks） | **传菜铃**："每当有菜出餐就响"——游戏在恰当时机自动调用所有模型注册过的回调方法 | ✅ "被动效果"就是重写这些方法 |

一条铁律：**永远通过命令层改状态，绝不直接改数字**。命令内部会自动处理钩子通知、数值修正（力量/易伤）、
战斗历史记录、特效音效——绕过它就等于绕过了整个游戏规则。

### 0.2 读效果代码需要的 C# 最小知识

效果代码用到的 C# 语法其实非常少，看懂下面六个写法就能读 95% 的效果代码：

```csharp
// ① 类与继承：我的卡 = 一个继承 CardModel（RitsuLib 脚手架里是 ModCardTemplate）的类
public sealed class Foo() : ModCardTemplate(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
//                          ↑ 主构造函数：小括号直接跟在类名后，参数原样传给基类
//                            四个参数 = 费用, 卡型, 稀有度, 目标类型

// ② override：重写基类的虚方法/属性——"游戏会在恰当时机调用它，我提供我的版本"
protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) { ... }
protected override void OnUpgrade() { ... }

// ③ 属性（property）：看起来像字段，其实是方法
public override bool GainsBlock => true;          // "表达式体属性"：每次读取返回 true

// ④ async / await / Task：效果是异步的（要播动画、要等动画）
//   规则很简单：基类方法是 async Task 你就写 async Task；里面每一步用 await 串起来
await DamageCmd.Attack(6m).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute(choiceContext);

// ⑤ 泛型 <T>：把"类型"当参数传。Apply<VulnerablePower> = 上"易伤"这个具体的 Power
await PowerCmd.Apply<VulnerablePower>(choiceContext, target, 2m, Owner.Creature, this);

// ⑥ 集合表达式 [ ]：新建一个数组/列表的简写（C# 12）
protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];
```

其余语法（`?.` 空条件、`??` 空合并、lambda `x => x * 2`）遇到时查任何 C# 入门资料即可。

### 0.3 RitsuLib 脚手架的基类与注册约定（30 秒版）

| 内容 | 怎么写 | 要不要特性 |
|---|---|---|
| 新卡牌（普通） | `class XxxCard() : ModCardTemplate(费用, CardType.X, CardRarity.X, TargetType.X)` | ❌ 基类已带 `[RegisterCard(..., Inherit = true)]`，子类自动入池 |
| 初始卡组里的卡 | 同上，再加 `[RegisterCharacterStarterCard(typeof(YourCharacter), 数量)]` | ✅ |
| 主题系列卡（可选） | 继承你自己定义的主题基类（其余写法完全相同） | ❌ |
| 新遗物 | `class XxxRelic() : ModRelicTemplate(RelicRarity.Uncommon)` | ❌ 同上自动注册 |
| 新药水 | `class XxxPotion() : ModPotionTemplate(PotionRarity.X, PotionUsage.X, TargetType.X)` | ❌ |
| 新 Power（增益/减益） | `class XxxPower : ModPowerTemplate` | ✅ `[RegisterPower]` |

本地化键：mod id 前缀 + 类名转大写下划线（如 `MyStrike` → `MYMOD_CARD_MY_STRIKE.title/.description`）；
卡图放 mod 资源目录的 `images/cards/{类名}.png` 即被 AssetProfile 自动找到。

---

## 1. 一切的地基：数值声明（DynamicVar）

**核心思想：卡的数值不写死在代码里，而是声明成"变量"，卡面文字用占位符引用它。**
这样升级、临时增减益、力量修正才能自动反映到卡面上。

每个 DynamicVar 内部有三个值：

| 值 | 干什么用 | 你能碰吗 |
|---|---|---|
| `BaseValue` | **真实数值**，所有游戏逻辑只读它 | ✅ 升级时加 |
| `EnchantedValue` | 附魔加成后的值（玩家眼里"算卡本身的一部分"） | 系统管 |
| `PreviewValue` | 卡面上**显示**的值（算上力量、易伤等一切修正） | ⛔ 绝对不要拿它做结算 |

写卡的固定套路：

```csharp
// ① 声明数值（类里重写这一个属性）
protected override IEnumerable<DynamicVar> CanonicalVars =>
[
    new DamageVar(6m, ValueProp.Move),      // 伤害 6，性质 = 普通攻击（详见 §1.2）
    new RepeatVar(3),                        // 段数 3
];

// ② 效果里读 BaseValue
await DamageCmd.Attack(DynamicVars.Damage.BaseValue)...

// ③ 升级时加
protected override void OnUpgrade()
{
    DynamicVars.Damage.UpgradeValueBy(3m);   // 升级 +3，卡面自动变绿
}
```

> **为什么不直接写死 6？** 因为描述 JSON 里写的是 `造成{Damage:diff()}点伤害`——数值、升级预览、
> 力量加成后的实时变色全靠这个变量体系。写死数字 = 放弃这一切。
>
> 注意：以上是**静态数值**的套路。当数值需要**运行时计算**（依赖目标、战斗状态、其他变量，比如
> "每有一张打击牌伤害 +2"），用下一节 §1.3 的 `ComputedDynamicVar`，声明和读取方式都不同。

### 1.1 常用 Var 速查

| Var | 占位符 | 用途 |
|---|---|---|
| `DamageVar(伤害, ValueProp.Move)` | `{Damage}` | 攻击伤害 |
| `BlockVar(格挡, ValueProp.Move)` | `{Block}` | 获得格挡 |
| `CardsVar(张数)` | `{Cards}` | 抽/弃/生成几张 |
| `EnergyVar(n)` | `{Energy}` | 几点能量 |
| `PowerVar<VulnerablePower>(2)` | `{VulnerablePower}` | 上几层某 Power（占位符名=类名） |
| `RepeatVar(n)` | `{Repeat}` | 重复几次（多段攻击的段数） |
| `new DynamicVar("自定义名", n)` | `{自定义名}` | 任意辅助数值（如"每次打出永久+2"里的 2） |

需要**运行时计算**的数值（"每有一张 X 牌 +2"这类）不要硬凑静态 Var，用下一节的 `ComputedDynamicVar`。

### 1.2 ValueProp：伤害的"性质"（新手最常懵的点）

`DamageVar`/`BlockVar` 的第二个参数是性质标记，决定这次数值**吃不吃力量/敏捷**：

| 写法 | 含义 |
|---|---|
| `ValueProp.Move` | 普通攻击/格挡——**吃**力量/敏捷加成 |
| `ValueProp.Unpowered \| ValueProp.Move` | 无力攻击——**不吃**力量（灼伤型自伤、状态牌伤害用这个） |
| `ValueProp.Unblockable \| ValueProp.Unpowered` | HP 流失——不可格挡也不吃力量（"失去生命"代价、中毒） |

> 记忆法：官方注释把 `Move` 解释为"出招"性质——是"一招"就吃加成；遗物/药水/Power 造成的伤害默认 `Unpowered`
> （所以荆棘反伤不吃你自己 stacks 的力量）。

### 1.3 计算型数值：ComputedDynamicVar（RitsuLib 推荐）

当数值需要**运行时计算**——依赖目标（"+目标身上的力量层数"）、战斗状态（"每有一张打击牌 +2"）、
其他变量、甚至预览模式——静态 Var 就不够了。官方原版有 `CalculatedDamageVar` +
`CalculationBaseVar/CalculationExtraVar` 三件套（读官方卡代码时能认出来即可），
但设计繁琐；**RitsuLib 提供了更好用的 `ModCardVars.Computed*` 系列**：传一个委托，显示值与结算值
运行时直接计算。

最常用的形态（伤害/格挡包装：**预览自动走力量、易伤、附魔等官方修正管道**）：

```csharp
protected override IEnumerable<DynamicVar> CanonicalVars =>
[
    // 显示与结算值 = 底数(6) + 2 × 场上"打击"牌数；预览会经过 Hook.ModifyDamage
    ModCardVars.ComputedDamage("Damage", 6m, CountStrikes),
];

// 委托建议用 static：变量会随卡牌克隆，static 不会意外捕获创建时的卡牌实例
private static decimal CountStrikes(CardModel card, Creature? _)
{
    if (card.Owner?.PlayerCombatState == null)               // 规范卡（图鉴/奖励）无战斗上下文
        return card.DynamicVars["Damage"].BaseValue;         // 直接回底数

    return card.DynamicVars["Damage"].BaseValue
         + 2m * card.Owner.PlayerCombatState.AllCards.Count(c => c.Tags.Contains(CardTag.Strike));
}
```

三条铁规矩：

1. **委托不会自动返回底数**——`baseValue` 只是后备数据，算式里要自己读
   `card.DynamicVars["名"].BaseValue`（上下文形式里是 `ctx.BaseValue`）。写死常量会丢升级。
2. **升级仍然走 `UpgradeValueBy`** 改 BaseValue：`DynamicVars["Damage"].UpgradeValueBy(3m);`
3. **效果里读取用 `EvaluateValueOrDefault`**（不是 `.BaseValue`！它会对目标求值）：

```csharp
decimal dmg = DynamicVars.EvaluateValueOrDefault("Damage", target: cardPlay.Target);
await DamageCmd.Attack(dmg).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute(choiceContext);
```

`ModCardVars` 家族速查（按需求对号入座）：

| 方法 | 用途 |
|---|---|
| `Computed(名, 底数, 委托)` | 通用计算值，**不**走伤害/格挡修正管道 |
| `ComputedDamage` / `ComputedBlock` | 伤害 / 格挡；预览自动过力量·易伤 / 敏捷·脆弱·附魔管道 |
| `ComputedOstyDamage` | 奥斯提的伤害（修正时来源 = 奥斯提） |
| `ComputedEnergy` / `ComputedStars` | 能量 / 辉星图标数量（描述里用 `{名:energyIcons()}` / `{名:starIcons()}` 渲染成图标） |
| `ComputedPower<T>` / `ComputedPowerAmountGiven<T>` | Power 层数；后者预览走 `Hook.ModifyPowerAmountGiven`（与官方 `PowerVar<T>` 同路径） |
| `Int(名, n)` | `new IntVar(名, n)` 的简写 |

还有**上下文形式**：委托写成 `static ctx => ...`，`ctx` 自带一批空值保护成员——`ctx.BaseValue`、
`ctx.Target`/`ctx.HasTarget`、`ctx.IsInCombat`、`ctx.CombatState`、`ctx.Player`、`ctx.IsPreview`/
`ctx.IsUpgradePreview`、`ctx.GetCardIntOrDefault("其他变量")`、防递归的 `ctx.EvaluateCardVarOrDefault(...)`
——不用再手写判空：

```csharp
ModCardVars.ComputedDamage(
    "Damage",
    static ctx => ctx.BaseValue + (ctx.HasTarget ? 2m : 0m),
    baseValue: 6m);
```

> 完整 API（独立预览工厂、共享 tooltip `WithSharedTooltip`、ctx 成员总表）见《官方教程》
> RitsuLib 篇的《01 - 添加基础内容 / 19 - 计算动态变量》。

---

## 2. 通用效果积木（直接抄，改名字就能用）

> 以下每块都是**完整可编译的最小卡牌类**。演示统一用官方 Power（易伤/力量等），你换成任何 Power 类名都行。
> `TargetType` 怎么选：单体攻击 `AnyEnemy`、全体 `AllEnemies`、纯自我 `Self`（完整枚举看 IDE 的补全提示即可）。

### 2.1 造成伤害（单体）

```csharp
/// <summary>造成 6 点伤害。</summary>
public sealed class FooStrike() : ModCardTemplate(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);   // 单体目标卡：目标必不为空，防御性检查

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)  // 每击伤害（读 BaseValue！）
            .FromCard(this, cardPlay)                         // 声明"这刀是谁出的"→ 自动设攻击者/动画
            .Targeting(cardPlay.Target)                       // 打玩家选的目标
            .WithHitFx("vfx/vfx_attack_slash")                // 命中特效（可换，也可整个不写）
            .Execute(choiceContext);                          // 结算！
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
```

要点：链式调用的顺序习惯是 `Attack → FromCard → Targeting → 特效 → Execute`，`Execute` 才真正动手。

### 2.2 多段攻击（打 X 次）

段数也是数值，用 `RepeatVar` 声明，升级就能"多打一次"：

```csharp
public sealed class FooMultiHit() : ModCardTemplate(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new RepeatVar(3),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars.Repeat.IntValue)        // ← 段数来自 RepeatVar
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Repeat.UpgradeValueBy(1m);   // 升级 = 多一段
}
```

### 2.3 全体攻击（+ 顺手给全体上减益）

```csharp
public sealed class FooAoe() : ModCardTemplate(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new PowerVar<VulnerablePower>(1m),                    // 官方 zhs 名：易伤
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)              // 不需要具体目标；! 表示"断言非空"
            .Execute(choiceContext);

        // 全体上易伤：HittableEnemies = 当前"打得到"的敌人列表
        await PowerCmd.Apply<VulnerablePower>(choiceContext, CombatState!.HittableEnemies,
            DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
```

### 2.4 随机目标攻击

把 2.2 的 `.Targeting(...)` 换成 `.TargetingRandomOpponents(CombatState!)`——**每一击都会重新随机摇人**。
同一段数打不同人，这就是"随机 3 次"类卡牌的全部秘密。

### 2.5 X 费卡（费多少能量打多少下）

```csharp
public sealed class FooXCost() : ModCardTemplate(0, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    protected override bool HasEnergyCostX => true;           // ← 声明这是 X 费卡（费用填 0）

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int x = ResolveEnergyXValue();                        // 解析 X = 剩余能量（含 ChemicalX 类修正）

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(x)                                  // X 费的经典用法：段数 = X
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
```

### 2.6 获得格挡

```csharp
public sealed class FooDefend() : ModCardTemplate(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;                  // 声明"我是格挡卡"（悬停提示/系统逻辑用）

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}
```

### 2.7 给自己上增益（Power 卡）

```csharp
public sealed class FooBuff() : ModCardTemplate(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<StrengthPower>(2m)];   // 力量 2

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature,
            DynamicVars.Strength.BaseValue,                   // PowerVar 的属性名 = Power 类名
            Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.Strength.UpgradeValueBy(1m);
}
```

> **Power 卡黄金法则**：卡牌只负责"把 Power 挂上去"；**每回合触发什么，写在 Power 类自己的钩子里**。
> 比如"恶魔形态"卡只有一行 `Apply<DemonFormPower>`，"每回合开始 +力量"在那边。见 §3.5。

### 2.8 攻击附带减益（最经典的卡牌形状）

攻击部分照抄 §2.1，末尾追加一行 Apply 即可：

```csharp
protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
{
    ArgumentNullException.ThrowIfNull(cardPlay.Target);

    await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
        .FromCard(this, cardPlay).Targeting(cardPlay.Target)
        .Execute(choiceContext);

    await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target,        // 官方 zhs 名：虚弱
        DynamicVars.Weak.BaseValue, Owner.Creature, this);
}
```

给敌人上 debuff 的 `TargetType` 要用 `AnyEnemy`；想"全体敌人虚弱+易伤"就仿照 §2.3 对 `HittableEnemies` 施加。

### 2.9 抽牌

```csharp
protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(3)];

protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
{
    await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
}
```

### 2.10 获得能量 / 辉星

```csharp
// 能量 +2（CanonicalVars: new EnergyVar(2)）
await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);

// 辉星 +3（CanonicalVars: new StarsVar(3)）——储君的专属资源，官方名"辉星"
await PlayerCmd.GainStars(DynamicVars.Stars.BaseValue, Owner);
```

"下回合才获得能量"不要自己记状态，直接上现成 Power：`PowerCmd.Apply<EnergyNextTurnPower>(...)`（抢占先机 Outmaneuver 同款）。

### 2.11 回血 / 加上限

```csharp
await CreatureCmd.Heal(Owner.Creature, 5m);                              // 回 5 点
await CreatureCmd.GainMaxHp(Owner.Creature, 3m);                         // 上限 +3（永久）
```

> 回血/上限类效果记得在类里加 `public override bool CanBeGeneratedInCombat => false;`，
> 防止战斗中随机生成打破平衡（官方 FruitJuice/RegenPotion 都这么做）。

### 2.12 弃牌 与"弃 X 抽 X"

```csharp
// 弃一张（配选择界面）
IEnumerable<CardModel> picked = await CardSelectCmd.FromHandForDiscard(
    choiceContext, Owner, new CardSelectorPrefs(SelectionScreenPrompt, 1), null, this);
await CardCmd.Discard(choiceContext, picked);

// 弃任意张、再抽等量（一条命令搞定，别自己拼 Discard+Draw——时序会错！）
List<CardModel> hand = PileType.Hand.GetPile(Owner).Cards.ToList();
await CardCmd.DiscardAndDraw(choiceContext, hand, hand.Count);
```

### 2.13 消耗别的牌换收益

```csharp
// 烧掉手里所有非攻击牌，每张获得 5 格挡（重振精神 SecondWind 模式）
foreach (CardModel card in PileType.Hand.GetPile(Owner).Cards.Where(c => c.Type != CardType.Attack).ToList())
{
    await CardCmd.Exhaust(choiceContext, card);
    await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
}
```

> ⚠️ 消耗牌**必须一张一张来**（官方注释明言不要做批量版）：每张牌的消耗钩子要完整跑完再烧下一张。

### 2.14 生成牌（小刀模式）

生成一张**官方现成的牌**（如小刀 Shiv），直接调它自带的静态工厂：

```csharp
protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(3)];

protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
{
    await Shiv.CreateInHand(Owner, DynamicVars.Cards.IntValue, CombatState!);   // 3 把小刀进手牌
}
```

生成**三选一**的牌（攻击药水模式）：

```csharp
List<CardModel> options = CardFactory.GetDistinctForCombat(Owner,
    Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
        .Where(c => c.Type == CardType.Attack),
    3, Owner.RunState.Rng.CombatCardGeneration);

CardModel? pick = await CardSelectCmd.FromChooseACardScreen(choiceContext, options, Owner, canSkip: true);
if (pick != null)
{
    pick.SetToFreeThisTurn();                                        // 本回合免费（按需保留）
    await CardPileCmd.AddGeneratedCardToCombat(pick, PileType.Hand, Owner);   // ← 必须用这个 Add！
}
```

> ⚠️ 战斗中新生成的卡必须走 `AddGeneratedCardToCombat`（它会写生成历史、触发 `AfterCardGeneratedForCombat`），
> 普通的 `CardPileCmd.Add` 会漏掉这些。
>
> 变体：羽化（Metamorphosis）——随机攻击牌**洗入抽牌堆**且本场战斗 0 费：生成后
> `EnergyCost.SetThisCombat(0)`，再 `CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, Owner, CardPilePosition.Random)`
> （生成入堆一律走 AddGeneratedCardToCombat，用 CardPilePosition 控制塞进抽牌堆的位置）。

### 2.15 状态牌（回合结束在手里烧你）

状态/诅咒牌的三件套：费用 `-1` + `MaxUpgradeLevel => 0` + `Unplayable` 关键词：

```csharp
public sealed class FooStatus() : ModCardTemplate(-1, CardType.Status, CardRarity.Status, TargetType.None)
{
    public override int MaxUpgradeLevel => 0;                 // 不可升级
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];   // 不能被打出
    public override bool HasTurnEndInHandEffect => true;      // ← 必须与 OnTurnEndInHand 成对出现！

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(2m, ValueProp.Unpowered | ValueProp.Move)];   // 无力自伤：不吃力量、不吃易伤

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars.Damage, this, null);
    }
}
```

> 忘写 `HasTurnEndInHandEffect => true` 是新手第一名坑：`OnTurnEndInHand` **根本不会被调用**。
> 所有 11 张官方手牌触发牌都是状态/诅咒。

---

## 3. "每当……就……"：钩子与时机效果

### 3.1 钩子是什么

`AbstractModel` 基类上有 180 多个 `virtual` 方法（钩子），游戏引擎在恰当时机对**所有在场的模型**逐个调用它们。
你的卡/遗物/Power 只要 `override` 自己关心的那个，就实现了"每当 X 发生就 Y"。

三个家族：

| 家族 | 形状 | 语义 | 例 |
|---|---|---|---|
| 通知型 | `Task AfterXxx(...)` / `BeforeXxx(...)` | "X 发生了，你可以做事" | 回合开始、卡被打出、受到伤害 |
| 数值修改器 | `decimal ModifyXxx(...)` | "这个数值要多少，你说了算（返回加值/倍率）" | 力量加伤、易伤乘伤 |
| 谓词 | `bool ShouldXxx(...)` | "X 还被允许吗？"（返回 false 即阻止） | 免死、不清格挡 |

** Early/Late 怎么选**：很多钩子有 `XxxEarly` / `Xxx` / `XxxLate` 三个版本（游戏对全员先跑一遍 Early、再跑一遍普通、再跑一遍 Late）。
**无脑用普通版**。只有两种情况用别的：你的效果必须赶在所有人前面（VeryEarly/Early），或必须等所有人结算完（Late）。
官方注释里反复写 "CAREFUL! You should usually use xxx instead of this" 的就是在警告你这件事。

### 3.2 遗物形态一：拾取即生效（最简单）

```csharp
/// <summary>拾取时最大生命 +7。</summary>
public sealed class FooRelic() : ModRelicTemplate(RelicRarity.Common)
{
    public override bool HasUponPickupEffect => true;        // 声明"拾取即生效"（UI/交易规则用）

    protected override IEnumerable<DynamicVar> CanonicalVars => [new MaxHpVar(7m)];

    public override async Task AfterObtained()
    {
        await CreatureCmd.GainMaxHp(Owner.Creature, DynamicVars.MaxHp.BaseValue);
    }
}
```

`AfterObtained` 是遗物最大的入口（官方 300 件里 84 件重写它）。

### 3.3 遗物形态二：战斗开始给敌人挂减益

```csharp
/// <summary>战斗开始时，给予所有敌人 1 层易伤。</summary>
public sealed class FooAggroRelic() : ModRelicTemplate(RelicRarity.Common)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<VulnerablePower>(1m)];

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        // 首回合才触发：TurnNumber <= 1
        if (participants.Contains(Owner.Creature) && Owner.PlayerCombatState.TurnNumber <= 1)
        {
            Flash();   // 遗物图标闪光（玩家看到"是它触发了"）
            await PowerCmd.Apply<VulnerablePower>(choiceContext, combatState.HittableEnemies,
                DynamicVars.Vulnerable.BaseValue, Owner.Creature, null);
        }
    }
}
```

官方同类：弹珠袋（BagOfMarbles，首回合全敌易伤）与本例完全同款；赤牛（Akabeko，首回合给活力）同样做"首回合判定 + `Flash()`"，只是挂在 `AfterSideTurnStart` 上——照抄即可。

### 3.4 遗物形态三：持续数值修正（改抽牌数/能量上限）

不做"某时刻触发"，而是**只要遗物在，数值就一直被改**：

```csharp
/// <summary>每回合开始时多抽 1 张牌。</summary>
public sealed class FooDrawRelic() : ModRelicTemplate(RelicRarity.Uncommon)
{
    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        return player != Owner ? count : count + 1;   // 多人模式：只改自己；其他人原样放行
    }
}
```

改能量上限用 `ModifyMaxEnergy`、改金币获取用 `ModifyGoldGained`、改 HP 流失用 `ModifyHpLostXxx`。
官方"副作用遗物"——灵体外质（Ectoplasm）：+1 能量/金币归零
（`ModifyMaxEnergy`/`ModifyGoldGained`）就是这个模式；添水（Sozu）：+1 能量/无法获得药水——前者走
Modify*，后者"禁不禁止"走 `ShouldProcurePotion` 谓词。

### 3.5 遗物形态四：计数器与用完即毁

```csharp
/// <summary>每进入 3 个房间获得 25 金币，用完 3 次后作废。</summary>
public sealed class FooCounterRelic() : ModRelicTemplate(RelicRarity.Uncommon)
{
    private const int MaxUses = 3;

    [SavedProperty]                                   // ← 存档持久化（跨存档保存计数）
    public int UsesLeft { get; private set; } = MaxUses;

    public override bool ShowCounter => true;                        // 图标上显示数字
    public override int DisplayAmount => Math.Max(0, UsesLeft);
    public override bool IsUsedUp => UsesLeft <= 0;                  // 用尽：图标变灰"已用完"

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (IsUsedUp || room != Owner.RunState.BaseRoom) return;     // BaseRoom = 地图节点本体（防重复）

        Flash();
        await PlayerCmd.GainGold(25m, Owner);

        UsesLeft--;
        if (IsUsedUp) Status = RelicStatus.Disabled;
        InvokeDisplayAmountChanged();                                // 通知 UI 刷新数字
    }
}
```

> 计数器三件套：`[SavedProperty]`（存档）+ `ShowCounter`/`DisplayAmount`（显示）+ `IsUsedUp`（作废）。
> setter 里改值后记得 `InvokeDisplayAmountChanged()`。

**更多值得研读的官方遗物**（306 条描述逐条核对后挑选，机制都能用本节 + §2.2 钩子拼出来）：

| 遗物 | 机制要点 | 关键钩子 / API |
|---|---|---|
| 冰淇淋 IceCream | 多余能量不清空（留到下回合） | `ShouldPlayerResetEnergy => false` |
| 符文金字塔 RunicPyramid | 回合结束不再自动弃手牌 | `ShouldFlush => false` |
| 宾邦 BingBong | 每当往牌组加牌，额外加一张相同的牌 | `AfterCardChangedPiles` 检测进牌组后克隆塞回 |
| 律动残余 BeatingRemnant | 一回合内失去的生命不超过 20 | `ModifyHpLostAfterOsty` + 每回合未格挡伤害累计 |
| 恶魔之舌 DemonTongue | 每回合首次失去生命时回复等量 | `AfterDamageReceived`（未格挡伤害）+ 首次标记 |
| 历史课 HistoryCourse | 回合开始打出上一回合最后打出的攻击牌的复制品 | 历史查询（§4.1.2）+ `CardCmd.AutoPlay`（§4.3.2） |
| 领主阳伞 LordsParasol | 遇见商人时立刻获得其所有在售物品 | `AfterRoomEntered` + 商店 API |
| 蜥蜴尾巴 LizardTail | 免死一次，回复 50% 最大生命 | `ShouldDieLate` / `AfterPreventingDeath`（§3.8） |

### 3.6 写自己的 Power（增益/减益）

Power 是"挂在生物身上的状态"。**先想清楚 StackType**：可叠层用 `Counter`（易伤/力量），不可叠用 `Single`（壁垒 Barricade）。

```csharp
/// <summary>你的回合开始时，获得 X 点格挡。（示例：可叠层的回合格挡）</summary>
[RegisterPower]
public sealed class FooAuraPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;              // Buff / Debuff
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner)) return;                 // 多人模式：只在自己回合生效

        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);   // Power 给的格挡不吃敏捷
    }
}
```

**持续型减益**（易伤式：几回合后自动消失）——递减时机抄官方三大减益的标准写法：

```csharp
public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
    IEnumerable<Creature> participants)
{
    if (side == CombatSide.Enemy)                  // 在"敌方回合结束"时递减（怪给你上的当回合不扣）
        await PowerCmd.TickDownDuration(this);
}
```

Power 的类名会直接成为卡面占位符（`PowerVar<FooAuraPower>` → `{FooAuraPower}`），
所以**类名起好一点**。Power 本地化在 `powers` 表，键格式与卡牌同理（mod id 前缀 + POWER + 类名大写下划线），
记得在语言目录里补齐 `powers.json` 并核对一次实际键名。

### 3.7 数值修改器：做一个"你的力量"

```csharp
/// <summary>（教学用）打出攻击牌时，每击额外 +1 伤害。</summary>
[RegisterPower]
public sealed class FooMightPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => true;               // 允许被减益扣成负数（力量同款）

    // "这次伤害要加多少？"——返回加值。游戏对每个在场模型都问一遍。
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (Owner != dealer) return 0m;                       // 只加成宿主自己打出的伤害
        if (!props.IsPoweredAttack()) return 0m;              // 无力攻击不吃（药水/荆棘伤害等）
        return Amount;                                        // 每击 +层数
    }
}
```

两行 `return 0m` 的判定是官方 StrengthPower 的原样骨架：**第一问"是我干的吗"，第二问"这刀吃加成吗"**。
易伤/虚弱这类"乘算"写 `ModifyDamageMultiplicative` 返回 `1.5m / 0.75m`；格挡类写 `ModifyBlockXxx`。

### 3.8 Should 谓词：阻止某件事

```csharp
// 壁垒式（Barricade）：挂着此 Power 的生物，回合开始格挡不清空
public override bool ShouldClearBlock(Creature creature)
{
    return Owner != creature;    // 问的不是我 → 不干涉；问我 → false（不许清）
}

// 免死式（瓶中精灵的核心）：
public override bool ShouldDie(Creature creature) => creature != Owner.Creature;
```

实战例子：储备（Reserves）的"能量不够支付时用辉星垫上"（每缺 1 点能量垫 2 颗辉星）走的就是
`ShouldPayExcessEnergyCostWithStars` 谓词——谓词家族的用途远不止"禁止"，还有"代偿"。

谓词是"一问全场上百个模型"的短路问答，返回 false 即阻止，部分谓词还带 `out preventer` 让 UI 显示是谁阻止的。

---

## 4. 选修：特殊效果实现图鉴

> 从这里开始是**选修**内容：§2–§3 的必修积木能拼出约六成官方卡的形状，这一章收录的是**有特殊实现**的效果。
> 全章按主题分为**六组**（4.1 数值与公式 → 4.2 费用操作 → 4.3 牌堆与卡牌操作 → 4.4 生死与驱散 → 4.5 资源与专属系统 → 4.6 流程与规则），
> 组内由浅入深；每节独立、附完整可抄代码，按需取用、不用按顺序读。
>
> | 组 | 解决什么问题 | 小节 |
> |---|---|---|
> | **4.1 数值与公式** | 数值不是写死的常数时 | 计算型与求值来源 · 历史计数 · 持久成长 · 临时属性 · Power 花样 · 翻倍格挡 |
> | **4.2 费用操作** | 改变卡牌的费用 | 静态减费/免费/提费 · 动态减费记账 · "下一张牌"家族 |
> | **4.3 牌堆与卡牌操作** | 牌去哪儿、牌从哪来 | 发现/检索 · 自动出牌 · 去向与复制 · 延迟回手 · 上关键词 · 变形 · 复制减益 |
> | **4.4 生死与驱散** | 死亡、复活与驱散增益 | 斩杀奖励 · 复活与自毁 · 清格挡与人工制品 · 附魔/畸变 |
> | **4.5 资源与专属系统** | 奥斯提 / 铸造 / 充能球 / 药水 | 召唤 · 铸造 · 充能球 · 生成药水 |
> | **4.6 流程与规则** | 干预游戏的流程本身 | 读敌人意图 · 延时效果 · 立即结束回合 · 任务卡 |
>
> 都是官方验证过的实现模式，代码为压缩摘录。

### 4.1 组一：数值与公式——当数值不是写死的常数

#### 4.1.1 计算型数值与求值来源（完美打击 PerfectedStrike / 全身撞击 BodySlam 式）

用 §1.3 的 `ComputedDamage`——**预览会自动经过力量、易伤等官方修正管道**，结算与卡面永远一致。
"每有一张 X 牌伤害 +2"（完美打击式）：

```csharp
public sealed class FooPerfected() : ModCardTemplate(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 总值 = 底数(6) + 2 × 场上"打击"牌数；预览自动过 Hook.ModifyDamage
        ModCardVars.ComputedDamage("Damage", 6m, CountStrikes),
    ];

    // 委托用 static：防止变量随卡牌克隆时捕获创建时的实例
    private static decimal CountStrikes(CardModel card, Creature? _)
    {
        if (card.Owner?.PlayerCombatState == null)              // 图鉴/奖励里的规范卡无战斗上下文
            return card.DynamicVars["Damage"].BaseValue;        // 直接回底数

        return card.DynamicVars["Damage"].BaseValue
             + 2m * card.Owner.PlayerCombatState.AllCards.Count(c => c.Tags.Contains(CardTag.Strike));
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        // 计算型数值的读取：EvaluateValueOrDefault 对目标求值（不是 .BaseValue！）
        decimal dmg = DynamicVars.EvaluateValueOrDefault("Damage", target: cardPlay.Target);
        await DamageCmd.Attack(dmg).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars["Damage"].UpgradeValueBy(2m);   // 底数升级 +2
}
```

描述 JSON 里 `{Damage:diff()}` 会显示 6+2×张数 并实时变色。官方原版的 CalculatedDamageVar +
CalculationBase/ExtraVar 三件套能实现同样效果，但 RitsuLib 文档明确不建议再用——
读官方卡代码时能认出来即可，自己写就用 `ComputedDamage`。格挡版用 `ComputedBlock`（预览走敏捷/脆弱管道）。

**求值来源速查**——"造成等于当前格挡的伤害"（全身撞击 BodySlam 式）"格挡 = 弃牌堆张数"（堆栈式）……
套路相同，唯一的问题是**从正确的位置把量读出来**：

| 想引用的量 | 读法 | 官方例 |
|---|---|---|
| 当前格挡值 | `Owner.Creature.Block` | 全身撞击 BodySlam（伤害 = 当前格挡） |
| 抽牌堆剩余张数 | `PileType.Draw.GetPile(Owner).Cards.Count` | 心灵震慑 MindBlast（伤害 = 剩余牌数） |
| 弃牌堆张数 | `PileType.Discard.GetPile(Owner).Cards.Count` | 堆栈 Stack（格挡 = 弃牌堆张数） |
| 目标身上某 Power 层数 | `target.GetPowerAmount<DoomPower>()` | 大限已至 TimesUp（伤害 = 灾厄层数） |
| 目标减益种数 | `target.Powers.Count(p => p.TypeForCurrentAmount == PowerType.Debuff && p is not ITemporaryPower)` | 撕碎 Rend |
| 队友的格挡 | 队友 `Creature.Block` | 拟态 Mimic（镜像队友格挡） |
| 历史条目（抽牌 / 出牌 / 失去生命） | 见 §4.1.2 | 谋杀 Murder |

BodySlam 式骨架——求值时才读，预览和结算自然一致：

```csharp
protected override IEnumerable<DynamicVar> CanonicalVars =>
    [ModCardVars.ComputedDamage("Damage", 0m, CountBlock)];

private static decimal CountBlock(CardModel card, Creature? _)
    => card.Owner?.Creature == null
        ? 0m                                   // 规范卡（图鉴/奖励）无战斗上下文
        : card.Owner.Creature.Block;           // 求值那一刻的当前格挡
```

#### 4.1.2 战斗历史计数（谋杀 Murder 式）

"查询过去"用战斗历史：`CombatManager.Instance.History.Entries.OfType<具体条目>()`。
配合 §1.3 的 `ComputedDamage`，数值直接跟着历史走，**不需要自己维护计数器**：

```csharp
/// <summary>你在本场战斗中每抽过一张牌，此牌额外造成 1 点伤害。</summary>
public sealed class FooMurder() : ModCardTemplate(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 显示与结算 = 底数(1) + 本场战斗中"我"抽过的牌数；预览自动过力量/易伤管道
        ModCardVars.ComputedDamage("Damage", 1m, CountDrawn),
    ];

    private static decimal CountDrawn(CardModel card, Creature? _)
    {
        if (card.Owner?.Creature == null)                    // 规范卡（图鉴/奖励）无拥有者
            return card.DynamicVars["Damage"].BaseValue;

        return card.DynamicVars["Damage"].BaseValue
             + CombatManager.Instance.History.Entries
                 .OfType<CardDrawnEntry>()                   // 官方的"抽牌"历史条目
                 .Count(e => e.Actor == card.Owner.Creature); // 只数自己抽的
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        decimal dmg = DynamicVars.EvaluateValueOrDefault("Damage", target: cardPlay.Target);
        await DamageCmd.Attack(dmg).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);   // 官方谋杀（Murder）的升级是降费
}
```

> 常用历史条目：`CardDrawnEntry`（抽牌）、`CardPlayFinishedEntry`（卡打出完成，条目上有 `HappenedThisTurn(...)`
> 判"是不是本回合"）。**两种计数姿势怎么选**：加伤害/格挡这类"数值"用历史查询（免维护，如本节）；
> 改费用这类"没有计算型机制"的用 §4.2.2 的钩子记账。

#### 4.1.3 持久成长与防降级（爪击 Claw 式）

```csharp
public sealed class FooClaw() : ModCardTemplate(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    private decimal _extraFromPlays;                          // 记录"本局打出来的成长"

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new DynamicVar("Increase", 2m),                       // 自定义数值 Var："每次+2"里的 2
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);

        // 遍历全场（含抽/弃牌堆）所有同名卡，一起变强
        foreach (FooClaw c in Owner.PlayerCombatState.AllCards.OfType<FooClaw>())
        {
            c.DynamicVars.Damage.BaseValue += DynamicVars["Increase"].BaseValue;
            c._extraFromPlays += DynamicVars["Increase"].BaseValue;   // 私有字段：本卡累计成长量
        }
    }

    // 降级（被效果打回原形）会把 BaseValue 重置——必须在这里把成长补回来，否则白打！
    protected override void AfterDowngraded() => DynamicVars.Damage.BaseValue += _extraFromPlays;
}
```

> 官方爪击（Claw）/Rampage/KinglyPunch 全是这个形状：**私有字段记账 + AfterDowngraded 回补**。
> 跨战斗还要保留的（ GeneticAlgorithm 式），再叠一层 `[SavedProperty]` + 写回 `DeckVersion`。

#### 4.1.4 临时属性：仅本回合生效的力量/敏捷（凌虐 Mangle / 预备打击 SetupStrike 式）

"本回合 +3 力量"和"敌人力量 -10（本回合）"是同一套机制的两面。官方为每张来源卡配一个
**临时包装 Power**，继承抽象类 `TemporaryStrengthPower`（敏捷/集中同理有 `TemporaryDexterityPower` /
`TemporaryFocusPower`）。子类薄到只有两行——**不要自己在卡牌里写"回合结束移除"**，同步与还原
全部由包装自动完成：

```csharp
// 预备打击（SetupStrike）：给自己 +3 临时力量（本回合）
public class SetupStrikePower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<SetupStrike>();   // 来源卡（图标/名称显示用）
    // IsPositive 默认 true = 正向临时力量
}

// 凌虐（Mangle）：敌人力量 -10（本回合）——同一个抽象类的负向版
public class ManglePower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<Mangle>();
    protected override bool IsPositive => false;                    // 负向：显示为减益
}
```

卡牌侧就是普通的 `PowerCmd.Apply`（凌虐：攻击后给敌人挂负向包装；预备打击：攻击后给自己挂正向包装）：

```csharp
// 预备打击（CanonicalVars 用 new PowerVar<StrengthPower>(3m) 声明层数）
await PowerCmd.Apply<SetupStrikePower>(choiceContext, Owner.Creature,
    DynamicVars.Strength.BaseValue, Owner.Creature, this);

// 凌虐（CanonicalVars: new DynamicVar("StrengthLoss", 10m)）
await PowerCmd.Apply<ManglePower>(choiceContext, cardPlay.Target,
    DynamicVars["StrengthLoss"].BaseValue, Owner.Creature, this);
```

机制（`TemporaryStrengthPower` 内部已完成，mod 不用管，但要知道原理）：

1. 包装挂上时（`BeforeApplied`）：自动对目标施加**真正的** `StrengthPower`（+Sign×Amount，静默）——
   本回合的伤害加成走的是真力量，所以"按力量层数"类的效果全部正常工作。
2. 包装层数变化时（`AfterPowerAmountChanged`）：给真力量同步增减。
3. 宿主回合结束时（`AfterSideTurnEnd`）：闪光 → 移除包装 → 对真力量施加 `-Sign×Amount`
   **精确还原**（正负由 `IsPositive` 决定——同一套代码支撑"给自己临时力量"与"给敌人临时减力量"）。
4. 图标名动态显示来源（`OriginModel` 的卡/药水/遗物标题，这就是游戏里 buff 显示为"预备打击"的原因）；
   `ITemporaryPower` 实现者还会被统计类效果特殊对待——苦难（Misery）复制减益、狩猎判定都靠
   `InternallyAppliedPower` 把包装映射回真身。

> 同族：`TemporaryDexterityPower`（临时敏捷）、`TemporaryFocusPower`（临时集中）、
> `SleightOfFleshPower`（吸血变体）、`IllusionPower`。想给敌人"本回合 -X 敏捷"，
> 继承 `TemporaryDexterityPower` 并 `IsPositive => false` 即可。

#### 4.1.5 Power 花样：定期上增益（仪式 Ritual 式）与一次性强化（活力 Vigor 式）

**仪式式**——"Power 定期给自己（或别人）上别的 Power"：

```csharp
private bool _active;                                         // 私有状态；写它的 setter 里按惯例 AssertMutable()

public override Task AfterApplied(Creature? applier, CardModel? cardSource)
{
    _active = true;                                           // 刚挂上：开始生效
    return Task.CompletedTask;
}

public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
    IEnumerable<Creature> participants)
{
    if (!_active || !participants.Contains(Owner)) return;
    await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, 1m, Owner, null);
}
```

**活力式**——"只强化下一次攻击"：用基类的 InternalData（实例隔离、克隆时经 `InitInternalData` 重建）
记住"是哪一次攻击"，加成只作用于它：

```csharp
private sealed class Data { public AttackCommand? Cmd; }

protected override object InitInternalData() => new Data();

public override Task BeforeAttack(AttackCommand command)
{
    if (command.Attacker != Owner || !command.DamageProps.IsPoweredAttack())
        return Task.CompletedTask;
    GetInternalData<Data>().Cmd = command;                    // 记下"这一次"攻击
    return Task.CompletedTask;
}
// 在 ModifyDamageAdditive 里：仅当本次结算属于 Data.Cmd 记录的那次攻击时返回层数，然后清空 Cmd
// （完整官方实现见活力 VigorPower.cs）
```

> 官方仪式（Ritual）Power 还会用私有布尔记录"宿主是否为敌人"（用于跳过首次触发，setter 内
> `AssertMutable()`）。

#### 4.1.6 翻倍现有格挡（巩固 Entrench / 固化药水 Fortifier 式）

```csharp
// "翻倍"= 再获得等于现有格挡的量（现有格挡保持不动）：
await CreatureCmd.GainBlock(Owner.Creature, Owner.Creature.Block, ValueProp.Unpowered, null);
// 固化药水（Fortifier）则是"三倍"：再获得 2 × 现有格挡
await CreatureCmd.GainBlock(target, target.Block * 2, ValueProp.Unpowered, null);
```

要点：带 `Unpowered`——翻倍出来的量不该再吃一次敏捷。想让卡面实时显示"翻倍后总格挡"，
用 §1.3 的 `ComputedBlock`，算式里读 `Owner.Creature.Block`。

### 4.2 组二：费用操作——让牌更便宜（或更贵）

#### 4.2.1 静态减费 / 免费 / 提费

```csharp
someCard.SetToFreeThisTurn();                // 本回合 0 费（生成卡常用）
someCard.EnergyCost.AddThisCombat(-1);       // 本场战斗费用 -1
someCard.EnergyCost.SetThisTurn(0);          // 仅本回合 0 费
```

"每打出一张技能牌，手牌费 -1"这类**持续减费**是"三件套"：
进入战斗时翻战斗历史补账 + `AfterCardPlayed` 实时续减 + 落点 `EnergyCost.AddThisTurn/AddThisCombat(-n)`
（按"本回合/本场战斗"口径选）——外加 `if (card != this) return;` 守卫。完整可抄实现在 §4.2.2（官方精密瞄准 Pinpoint 同款）。
提费同理：重构（Transfigure）式 `EnergyCost.AddThisCombat(1)` + 公开属性 `BaseReplayCount++`（打两次）。

#### 4.2.2 动态减费："每打出过一张技能牌，费用 -1"（精密瞄准 Pinpoint 式双钩子记账）

费用没有"计算型"机制，所以要**记账**。官方精密瞄准（Pinpoint）的完整骨架是"双钩子"：

```csharp
/// <summary>你本回合每打出过一张技能牌，这张牌的费用 -1。</summary>
public sealed class FooPinpoint() : ModCardTemplate(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(15m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
    }

    // ① 补账：这张牌是战斗中途才进手的（生成/检索），把手牌前本回合已发生的技能牌一次性算回来
    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card != this || IsClone) return Task.CompletedTask;     // 两个标准守卫

        int amount = CombatManager.Instance.History.CardPlaysFinished
            .Count(e => e.CardPlay.Card.Type == CardType.Skill
                     && e.CardPlay.Player == Owner
                     && e.HappenedThisTurn(CombatState!));
        ReduceCostBy(amount);
        return Task.CompletedTask;
    }

    // ② 续账：之后每打出一张技能牌，实时再减
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner || cardPlay.Card.Type != CardType.Skill)
            return Task.CompletedTask;
        ReduceCostBy(1);
        return Task.CompletedTask;
    }

    private void ReduceCostBy(int amount) => EnergyCost.AddThisTurn(-amount);   // 本回合口径用 ThisTurn

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4m);
}
```

要点：

- **"本回合每……"用 `AddThisTurn(-n)`，"本场战斗每……"用 `AddThisCombat(-n)`**——口径别选错。
- 两次判重守卫是官方惯例：`if (card != this) return;`（钩子会对每张卡触发）+ `if (IsClone) return;`（复制品不重复记账）。
- 官方同类：午夜（Midnight）/精密瞄准（Pinpoint）/踩踏（Stomp）/女妖之嚎（BansheesCry）/重压（Flatten）
  （触发源分别是消耗/技能/攻击/虚无牌/奥斯提攻击）。

#### 4.2.3 "下一张牌"效果家族（无情猛攻 Unrelenting / 猛扑 Pounce 式）

"你打出的下一张攻击牌耗能为 0""下一张技能牌耗能为 0"……官方做法是**挂一个计数包装 Power，
在 Power 里拦截下一次对应事件**。四张 0 费卡共用同一套骨架（Unrelenting → `FreeAttackPower`、
Pounce → `FreeSkillPower`、Synthesis → `FreePowerPower`、Veilpiercer → `VeilpiercerPower`）：

```csharp
public class FreeAttackPower : PowerModel          // 官方原文骨架（略去无关行）
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner.Creature != Owner) return false;        // 只改自己打出的牌
        if (card.Type != CardType.Attack) return false;        // 只改攻击牌
        if (card.Pile?.Type is not (PileType.Hand or PileType.Play)) return false;   // 手牌中/正在打出
        modifiedCost = 0m;                                     // 免费！
        // ……随后消耗自身一层（在 BeforeCardPlayed 钩子里做，用完即止），原文略
        return true;
    }
}
```

卡牌侧照常一行：`PowerCmd.Apply<FreeAttackPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);`

同族变体只是换一个拦截钩子：

| "下一张……" | 拦截钩子 | 官方例 |
|---|---|---|
| 耗能为 0 | `TryModifyEnergyCostInCombat(Late)` | 无情猛攻 / 猛扑 / Synthesis / Veilpiercer |
| 额外打出一次 | `ModifyCardPlayCount` | 信号增强 SignalBoost（下一张能力牌） |
| 放到抽牌堆顶 | `ModifyCardPlayResultLocation` | 弹回 Rebound |

### 4.3 组三：牌堆与卡牌操作——牌去哪儿、牌从哪来

#### 4.3.1 发现 / 检索：把牌弄进手里（富足 Abundance / 指导 Tutor / 捞牌）

三型入门流程都在 §2.14 的"三选一"（`CardFactory.GetDistinctForCombat` → `FromChooseACardScreen` →
`SetToFreeThisTurn` → `AddGeneratedCardToCombat`）。这里补两个变体：

```csharp
// A 型（富足 Abundance）：发现的候选是【升级版】、不可放弃
foreach (CardModel option in options) CardCmd.Upgrade(option);           // 候选先升级
CardModel? pick = await CardSelectCmd.FromChooseACardScreen(choiceContext, options, Owner);   // 不传 canSkip = 不可放弃
// 另注意：Abundance 本体 CanBeGeneratedInCombat => false（Ancient 查询奖励，防再生成）

// B 型（指导 Tutor）：检索【队友】的抽牌堆，挑一张放进其手牌（多人卡）
public sealed class FooTutor() : ModCardTemplate(1, CardType.Skill, CardRarity.Rare, TargetType.AnyAlly)
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        Player ally = cardPlay.Target.Player;

        CardModel? pick = (await CardSelectCmd.FromCombatPile(choiceContext,
            PileType.Draw.GetPile(ally), ally,
            new CardSelectorPrefs(SelectionScreenPrompt, 1), null)).FirstOrDefault();
        if (pick != null)
            await CardPileCmd.Add(pick, PileType.Hand);   // 牌本来属于队友 → 自动回队友手牌（用 Add，不是生成）
    }
}
```

#### 4.3.2 自动出牌（破灭 Havoc / 倾泻 Cascade 式）

```csharp
// 从抽牌堆顶自动打出 N 张（不够自动洗牌）；破灭（Havoc）传 forceExhaust: true
await CardPileCmd.AutoPlayFromDrawPile(choiceContext, Owner, DynamicVars.Cards.IntValue,
    CardPilePosition.Top, forceExhaust: false);

// 免费自动打"指定的某张牌"（低语耳环 WhisperingEarring 同款；skipXCapture 防 X 费卡二次捕获）
await CardCmd.AutoPlay(choiceContext, someCard, target, AutoPlayType.Default, skipXCapture: true);
```

> `skipXCapture: true` 用于 X 费卡：资源已由调用方花掉，别再捕获一次 X 值。
>
> 变体：地狱狂徒（Hellraiser）——在 `AfterCardDrawnEarly` 里检测抽到的牌带 `CardTag.Strike`
> 标签（打击族）就 `CardCmd.AutoPlay` 自动打出（带每回合次数上限防死循环）。自动出牌的触发时机可以挂在任何钩子上。

#### 4.3.3 打完后的去向：复制自己（愤怒 Anger 式）与改去向钩子

```csharp
// 愤怒（Anger）式：打完再塞一张克隆进弃牌堆
CardModel copy = CreateClone();
CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Discard, Owner), 2.2f);

// 改"打完去哪"：重写去向钩子（魔球 TheBall 用它在多人模式把弃置改投队友牌堆）
public override CardLocation GetResultLocationForCardPlay()
{
    if (Keywords.Contains(CardKeyword.Exhaust))
        return new CardLocation(Owner, PileType.Exhaust, CardPilePosition.Bottom);
    return new CardLocation(Owner, PileType.Discard, CardPilePosition.Bottom);
}
```

> 重写会覆盖默认逻辑（Power/复制品→移除、消耗关键词→消耗堆、否则弃牌堆），照抄默认分支再改你要改的。
> ⚠️ 给别的玩家转移卡不要在 `OnPlay` 里直接调 `GiveToAnotherPlayer`（避坑 #6）。

#### 4.3.4 延迟回手 / 自我循环（流星锤 Bolas / 得力助手 RightHandHand 式）

"下个回合开始时返回手牌"——官方做法依然是**查历史而不是记状态**（和 §4.1.2 同思路）：

```csharp
/// <summary>流星锤：造成 3 点伤害。在你的下个回合开始时，将此卡返回你的手牌。</summary>
public sealed class FooBolas() : ModCardTemplate(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
        // 注意：OnPlay 里什么都不安排！
    }

    // 每次回合开始抽牌前查一遍历史："上一回合我打过这张牌吗？打过、且现在不在手里 → 回手"
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player != Owner) return;

        bool playedLastTurn = CombatManager.Instance.History.CardPlaysFinished
            .Any(e => e.HappenedLastPlayerTurn(Owner) && e.CardPlay.Card == this);

        if (playedLastTurn && Pile?.Type != PileType.Hand)
            await CardPileCmd.Add(this, PileType.Hand);
    }
}
```

同族：无休手斧（ThrummingHatchet，同款）、粒子墙（ParticleWall，**即时**回手——重写
`GetResultLocationForCardPlay` 把打完的弃置去向改成手牌）、得力助手（RightHandHand，"每打出能量值 ≥2 的牌
后把自己从弃牌堆回手"——在 `AfterCardPlayedLate` 里查条件后回手）。

#### 4.3.5 上关键词：虚无 / 保留 / 奇巧（雕琢打击 SculptingStrike / 虚空之唤 CallOfTheVoid 式）

关键词施加只有一个核心 API：`CardCmd.ApplyKeyword(card, params CardKeyword[])`（void 同步调用）。
难点不在 API，在**三种时机**怎么选：

**① 永久上关键词**——雕琢打击：攻击后从手牌选一张（排除已有虚无的），给它永久虚无：

```csharp
protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
{
    ArgumentNullException.ThrowIfNull(cardPlay.Target);
    await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
        .Targeting(cardPlay.Target).Execute(choiceContext);

    // 过滤：排除"自身已带虚无"的牌——注意用 GetKeywordsWithSources(KeywordSources.Local)
    // 只看卡自身的关键词，避免全局来源（别的 Power 给的）干扰判断
    CardModel? pick = (await CardSelectCmd.FromHand(choiceContext, Owner,
        new CardSelectorPrefs(SelectionScreenPrompt, 1),
        (CardModel c) => !c.GetKeywordsWithSources(KeywordSources.Local).Contains(CardKeyword.Ethereal),
        this)).FirstOrDefault();

    if (pick != null)
        CardCmd.ApplyKeyword(pick, CardKeyword.Ethereal);      // 永久虚无（void 同步调用）
}
```

**② 生成的牌自带关键词**——虚空之唤：每回合起手抽牌时，额外生成 N 张"带虚无的随机牌"。
关键点：**在入堆之前**给生成的卡上关键词（`AddGeneratedCardsToCombat` 之前），并排除 Basic/Ancient
这类"不该被改造"的稀有度：

```csharp
// CallOfTheVoidPower 内部（层数 = 每回合生成几张）
public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext,
    ICombatState combatState)
{
    if (player != Owner.Player) return;                        // 多人：只管自己

    List<CardModel> pool = Owner.Player.Character.CardPool
        .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
        .Where(c => c.Rarity != CardRarity.Basic && c.Rarity != CardRarity.Ancient)   // 排除初始/古卡
        .ToList();

    var generated = new CardModel[Amount];
    for (int i = 0; i < Amount; i++)
    {
        generated[i] = CardFactory.GetDistinctForCombat(player, pool, 1,
            player.RunState.Rng.CombatCardGeneration).First();
        CardCmd.ApplyKeyword(generated[i], CardKeyword.Ethereal);   // 入堆前打上虚无
    }

    Flash();
    await CardPileCmd.AddGeneratedCardsToCombat(generated, PileType.Hand, Owner.Player);
}
```

**③ 仅本回合的关键词**——保留/奇巧有现成的单回合 API（常驻的才走 `ApplyKeyword`）：

```csharp
// 本回合抽的牌全部保留（Expertise 式）：对 Draw 返回的卡逐张调用
foreach (CardModel c in await CardPileCmd.Draw(choiceContext, 3, Owner))
    CardCmd.ApplySingleTurnRetain(c);

// 选一张手牌获得奇巧（HandTrick 式）
CardModel? pick = (await CardSelectCmd.FromHand(choiceContext, Owner,
    new CardSelectorPrefs(SelectionScreenPrompt, 1), null, this)).FirstOrDefault();
if (pick != null) CardCmd.ApplySingleTurnSly(pick);
```

> 还有一种"全局关键词来源"：`TryModifyKeywordsInCombat` 钩子（战斗中给某张卡增删关键词，
> 不落库、随来源存在而存在，如 Hex 系）。移除关键词用 `CardCmd.RemoveKeyword`。

#### 4.3.6 变形：把一张卡换成另一张

```csharp
await CardCmd.TransformTo<Abundance>(someCard);                     // 原地变成指定卡
await CardCmd.Transform(someCard, CombatState.CreateCard<OtherCard>(Owner));   // 换成构造好的新卡（官方 CreateCard + Transform 模式）
await CardCmd.TransformToRandom(someCard, rng);                     // 随机变形
```

#### 4.3.7 复制敌人身上的减益（苦难 Misery 式）

不要盲目 `Apply`——先找目标身上有没有同名实例：有就 `PowerCmd.ModifyAmount` 叠层，没有才 Apply 克隆
（盲目 Apply 会丢叠层语义）。简化骨架：

```csharp
// 快照目标身上的全部减益
List<PowerModel> snapshot = target.Powers
    .Where(p => p.TypeForCurrentAmount == PowerType.Debuff)
    .Select(p => (PowerModel)p.ClonePreservingMutability())
    .ToList();

foreach (Creature enemy in CombatState!.HittableEnemies.Where(e => e != target))
{
    foreach (PowerModel clone in snapshot)
    {
        PowerModel? existing = PowerCmd.FindExistingInstanceForStacking(clone, enemy, Owner.Creature);
        if (existing != null)
            await PowerCmd.ModifyAmount(choiceContext, existing, clone.Amount, Owner.Creature, this);   // 已有：叠层
        else
            await PowerCmd.Apply(choiceContext, clone, enemy, clone.Amount, Owner.Creature, this);      // 没有：上克隆
    }
}
```

### 4.4 组四：生死与驱散

#### 4.4.1 斩杀（Fatal）：击杀奖励模板

"击杀时获得 X"的坑在于：目标可能带免死效果（瓶中精灵类）。官方模板两步走：

```csharp
protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
{
    ArgumentNullException.ThrowIfNull(cardPlay.Target);

    // ① 打之前先问一遍：目标身上有没有"免死也不触发击杀奖励"的 Power
    bool shouldTriggerFatal = cardPlay.Target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal());

    AttackCommand cmd = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
        .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute(choiceContext);

    // ② 打完检查结果：Results 是"每击一个列表"的嵌套结构，必须 SelectMany 摊平
    if (shouldTriggerFatal && cmd.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
    {
        await CreatureCmd.GainMaxHp(Owner.Creature, DynamicVars.MaxHp.IntValue);
    }
}
```

卡面记得加 `HoverTipFactory.Static(StaticHoverTip.Fatal)` 提示词（官方中文关键词名：**斩杀**；
官方狂宴 Feed/贪婪之手 HandOfGreed 同款）。

#### 4.4.2 复活与自毁（瓶中精灵 FairyInABottle / 孤注一掷 TheGambit 式）

复活药水是**全自动触发**的药水（`Usage => PotionUsage.Automatic`，玩家不能主动喝）：

```csharp
public sealed class FooFairy() : ModPotionTemplate(PotionRarity.Rare, PotionUsage.Automatic, TargetType.Self)
{
    public override bool CanBeGeneratedInCombat => false;     // 不可在战斗中随机生成

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        AssertValidForTargetedPotion(target);
        await CreatureCmd.Heal(target, Math.Max((decimal)target.MaxHp * 0.3m, 1m));
    }

    // 死亡流程会问全场"还允许死吗"——不是宿主就放行，是宿主就否决
    public override bool ShouldDie(Creature creature) => creature != Owner.Creature;

    // 否决成功后游戏回调这里：复活 = 把"使用药水"的完整流程手动跑一遍
    public override async Task AfterPreventingDeath(Creature creature)
        => await OnUseWrapper(new ThrowingPlayerChoiceContext(), creature);
}
```

反向操作：孤注一掷（TheGambit）——它附加的 TheGambitPower 在 `AfterDamageReceived` 里检测"受到未被
格挡的攻击伤害"后 `CreatureCmd.Kill(Owner.Creature)` 自毁。同一套死亡钩子，既能救场也能立死亡 flag；
`Kill(force: true)` 连免死保护都能一起绕过。

#### 4.4.3 清空敌人的格挡与人工制品（暴露 Expose 式）

```csharp
public sealed class FooExpose() : ModCardTemplate(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Power", 2m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        Creature target = cardPlay.Target;

        // ① 清空格挡：数量直接传 target.Block，一句"全删"
        await CreatureCmd.LoseBlock(choiceContext, target, target.Block, Owner.Creature);

        // ② 显式吃掉人工制品——不先移除，下一步的易伤会被它抵消
        if (target.HasPower<ArtifactPower>())
        {
            await PowerCmd.Remove<ArtifactPower>(target);
        }

        // ③ 现在再上易伤就稳了
        await PowerCmd.Apply<VulnerablePower>(choiceContext, target,
            DynamicVars["Power"].IntValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["Power"].UpgradeValueBy(1m);
}
```

> 通用结论：怕被人工制品挡掉的减益，先 `HasPower<ArtifactPower>()` + `PowerCmd.Remove<ArtifactPower>` 摘除。
> 人工制品的抵消发生在系统层，你的 Apply 不会报错，只会静默无效。

#### 4.4.4 给牌附附魔 / 畸变

```csharp
CardCmd.Enchant<Inky>(someCard, 1m);                     // 附魔（BladeOfInk 给生成的小刀附魔同款）
                                                         // ⚠️ void 同步调用，不 await
await CardCmd.Afflict<SomeAffliction>(someCard, 1m);     // 畸变：异步，失败返回 null
CardCmd.ClearEnchantment(someCard);                      // 清除
```

### 4.5 组五：资源与专属系统

#### 4.5.1 召唤奥斯提（护卫 Bodyguard / 骨头酿 BoneBrew 式）

```csharp
protected override IEnumerable<DynamicVar> CanonicalVars => [new SummonVar(5m)];

protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
{
    await OstyCmd.Summon(choiceContext, Owner, DynamicVars.Summon.BaseValue, this);
    // 目标身上已有奥斯提 → 自动转为加其最大 HP；最后一个参数 source 传 this，供钩子溯源
    // 反向操作——处决自己的奥斯提换收益：CreatureCmd.Kill（BoneShards/Sacrifice 式）
}
```

#### 4.5.2 铸造（王之勇气 KingsCourage 式）

```csharp
protected override IEnumerable<DynamicVar> CanonicalVars => [new ForgeVar(15)];

protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)   // 药水写法
{
    await ForgeCmd.Forge(DynamicVars.Forge.IntValue, target!.Player, this);
    // 本场战斗首次铸造：自动把君王之剑（Sovereign Blade）塞进手牌；之后每次调用 = 君王之剑 +15
}
```

#### 4.5.3 充能球：生成 / 栏位 / 被动（球状闪电 BallLightning / 扩容 Capacitor / 漆黑 Darkness 式）

```csharp
await OrbCmd.Channel<LightningOrb>(choiceContext, Owner);   // 生成一个闪电充能球
await OrbCmd.AddSlots(Owner, 2);                            // +2 栏位（超上限自动钳制）
await OrbCmd.RemoveSlots(Owner, 1);                         // 移除 1 栏位（从队尾，含已生成好的球）
await OrbCmd.EvokeNext(choiceContext, Owner);               // 激发最早生成的球

// 手动触发场上所有暗珠的被动（漆黑 Darkness 式；目标传 null = 珠子自选目标）
foreach (OrbModel orb in Owner.PlayerCombatState.OrbQueue.Orbs.Where(o => o is DarkOrb))
    await OrbCmd.Passive(choiceContext, orb, null);
```

#### 4.5.4 生成药水（炼制药水 Alchemize 式）

```csharp
PotionModel potion = PotionFactory
    .CreateRandomPotionInCombat(Owner, Owner.RunState.Rng.CombatPotionGeneration)
    .ToMutable();

PotionProcureResult result = await PotionCmd.TryToProcure(potion, Owner);   // 药水栏满 → success == false
```

### 4.6 组六：流程与规则

#### 4.6.1 读敌人意图（眼部攻击 GoForTheEyes 式）

"如果敌人的意图是攻击，则……"——意图挂在 `Monster.IntendsToAttack` 上：

```csharp
protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
{
    ArgumentNullException.ThrowIfNull(cardPlay.Target);
    await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
        .Targeting(cardPlay.Target).Execute(choiceContext);

    if (cardPlay.Target.Monster.IntendsToAttack)                 // ← 敌人打算攻击？
    {
        await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target,
            DynamicVars.Weak.BaseValue, Owner.Creature, this);
    }
}

// 加分项：卡面金光联动——场上有敌人打算攻击时发金光，告诉玩家"现在打正合适"
protected override bool ShouldGlowGoldInternal
{
    get
    {
        if (CombatState == null) return false;
        return CombatState.HittableEnemies.Any(e => e.Monster?.IntendsToAttack ?? false);
    }
}
```

> 意图体系（出招表、意图图标、`Stun` 改写出招）属 MonsterMoves 系统，本文档只到 `IntendsToAttack`
> 这一层，想深入可以读反编译源码里的 MonsterMoves 相关类。

#### 4.6.2 延时效果："下个回合……"（抢占先机 Outmaneuver / 瓶中船 ShipInABottle 式）

"下回合获得能量 / 抽牌 / 获得格挡 / 召唤……"**从不靠卡牌自己记账**——挂一个包装 Power，
在合适的回合边界钩子里做事、然后自移除。官方为最常见的三种需求准备了现成 Power，
而且它们恰好示范了三种不同的钩选策略：

| 需求 | 现成 Power | 钩子策略 |
|---|---|---|
| 下回合获得能量 | `EnergyNextTurnPower` | `AfterEnergyReset`：回合开始能量重置时给能量 → 自移除 |
| 下回合获得格挡 | `BlockNextTurnPower` | `AfterBlockCleared`：回合开始旧格挡被清掉**之后**再给（保证新格挡能站到下回合） |
| 下回合多抽牌 | `DrawCardsNextTurnPower` | `ModifyHandDraw` +N（用 `AmountOnTurnStart` 当门闩）+ `AfterSideTurnStart` 自移除 |

用法都是一行（瓶中船 ShipInABottle：现在 +10，下回合开始再 +10）：

```csharp
await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
await PowerCmd.Apply<BlockNextTurnPower>(choiceContext, Owner.Creature,
    DynamicVars.Block.IntValue, Owner.Creature, this);
```

自定义延时（官方没有现成 Power 时）——熔炉（Furnace）"每回合开始铸造"式：

```csharp
public class FooTickPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;   // 层数 = 剩余次数

    public override async Task AfterSideTurnStart(CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner)) return;            // 多人：只在宿主回合走
        Flash();
        /* ……做你的事：抽牌 / 召唤 / 生成充能球…… */
        if (Amount <= 1) await PowerCmd.Remove(this);         // 次数用完自移除
        else await PowerCmd.Decrement(this);                  // 永久循环型则不自移除（仪式式，见 §4.1.5）
    }
}
```

> 钩子怎么选：跟**能量**相关钩 `AfterEnergyReset`；跟**格挡**相关钩 `AfterBlockCleared`（在清除之后给，
> 新格挡才站得住）；跟**抽牌数量**相关实现 `ModifyHandDraw`；需要弹玩家选择的用
> `AfterPlayerTurnStart`。多回合递减用 Counter 层数，无限循环用 §4.1.5 的仪式式。

#### 4.6.3 立即结束回合（虚空形态 VoidForm 式）

```csharp
await PowerCmd.Apply<VoidFormPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
PlayerCmd.EndTurn(Owner, canBackOut: false);   // 注意：同步调用，不 await；先上增益再结束
```

#### 4.6.4 任务卡（Quest）：立牌子 → 记进度 → 领奖收尾

任务卡是一张**不能打出的牌**，躺在你的牌组里充当"任务牌子"：它要么扭曲你的地图/事件（走到指定地方），
要么默默记录进度（做过 N 件事），达成后摇身一变成奖励、或者直接消失。

**骨架（所有官方任务卡一致的四件套）**：

```csharp
public LanternKey() : base(-1, CardType.Quest, CardRarity.Quest, TargetType.Self) { }
//                     ↑ 费用-1      ↑ 任务卡专属卡型/稀有度
public override int MaxUpgradeLevel => 0;                                    // 不可升级
public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];   // 不可打出
```

之后按需求选一条路线：

**路线 A：改地图 / 事件流**（灯火钥匙 LanternKey：把第二幕的未知节点全部锁成特定事件）——
重写两个地图钩子，**不需要任何进度计数**：

```csharp
public sealed class FooQuestA() : ModCardTemplate(-1, CardType.Quest, CardRarity.Quest, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    // ① 未知地图节点只允许摇出事件房
    public override IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes)
    {
        if (Owner.RunState.CurrentActIndex != 2) return roomTypes;   // 只在第二幕生效
        return new HashSet<RoomType> { RoomType.Event };
    }

    // ② 事件本身也锁定成指定的一个
    public override EventModel ModifyNextEvent(EventModel currentEvent)
    {
        if (Owner.RunState.CurrentActIndex != 2) return currentEvent;
        return ModelDb.Event<WarHistorianRepy>();                    // 指定事件（官方：WarHistorianRepy）
    }
}
```

**路线 B：记进度流**（探寻 Dowsing：进 5 次未知房间后变成富足 Abundance）——
`[SavedProperty]` 记进度 + 钩子计数 + 达成收尾，这是任务卡的完全体：

```csharp
public sealed class FooQuestB() : ModCardTemplate(-1, CardType.Quest, CardRarity.Quest, TargetType.None)
{
    private const int Target = 5;

    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Rooms", Target)];
    // 卡面显示"还剩几次"：{Rooms}

    // 进度计数：存档持久化 + setter 里同步卡面数值（官方惯例三连）
    [SavedProperty]
    public int RoomsEntered
    {
        get => _roomsEntered;
        set
        {
            AssertMutable();
            _roomsEntered = value;
            DynamicVars["Rooms"].BaseValue = Target - _roomsEntered;   // 同步卡面显示
        }
    }
    private int _roomsEntered;

    public override async Task BeforeRoomEntered(AbstractRoom room)
    {
        // 守卫：牌必须在牌组里 / 只数地图节点本体（CurrentRoomCount==1 防同节点重复触发）
        if (Pile == null || Pile.Type != PileType.Deck || Owner.RunState.CurrentRoomCount > 1)
            return;
        if (Owner.RunState.CurrentMapPoint is not { } mapPoint || mapPoint.PointType != MapPointType.Unknown)
            return;                                                    // 只数"未知"节点

        RoomsEntered++;
        if (RoomsEntered >= Target)
        {
            PlayerCmd.CompleteQuest(this);                             // ① 标记任务完成
            await CardCmd.TransformTo<Abundance>(this);                // ② 收尾：变成奖励卡（Abundance）
        }
    }
}
```

**收尾方式三种**（完成时选一）：

| 收尾 | API | 例子 |
|---|---|---|
| 变成奖励卡 | `await CardCmd.TransformTo<Abundance>(this)` | 探寻 Dowsing |
| 直接消失 | `await CardPileCmd.RemoveFromDeck(this)` | 藏宝图 SpoilsMap（另外还发金币） |
| 纯标记（供别处判断） | `PlayerCmd.CompleteQuest(this)` | — |

**细节清单**：

- 进度条显示：`DynamicVar("Rooms", n)` + setter 同步（上面代码）；也可以用遗物那套 `ShowCounter/DisplayAmount`。
- 卡面预告奖励：`ExtraHoverTips => [HoverTipFactory.FromCard<Abundance>()]`（探寻 Dowsing 同款）。
- 修改地图本体（往地图上撒宝箱/插任务节点）用 `ModifyGeneratedMap` + `MapPoint.AddQuest(this)`——藏宝图（SpoilsMap）式。
- 守卫顺序别省：`Pile.Type != Deck`（事件/奖励界面里的副本卡不该计数）、`CurrentRoomCount` 防重、幕数判断按需加。

---

## 5. 新手避坑清单（每条都踩过）

| # | 坑 | 正确做法 |
|---|---|---|
| 1 | 拿 `PreviewValue` 做结算 | 只读 `BaseValue`；PreviewValue 仅用于显示 |
| 2 | `OnTurnEndInHand` 不生效 | 忘了配对 `HasTurnEndInHandEffect => true` |
| 3 | 循环调用 `CardCmd.Discard` 弃多张 | 用集合重载；Sly 时序会错 |
| 4 | 批量消耗卡 | 没有批量版！逐张 `Exhaust`，钩子要完整跑 |
| 5 | 生成卡用普通 `Add` | 用 `AddGeneratedCardToCombat`（写历史+触发钩子） |
| 6 | 在 `OnPlay` 里直接 `GiveToAnotherPlayer` | 用 `GetResultLocationForCardPlay` 或 `ModifyCardPlayResultLocation` 钩子改去向 |
| 7 | 持久成长卡被降级后数值清零 | `AfterDowngraded()` 里回补累计增幅 |
| 8 | 击杀奖励发给了免死目标 | Fatal 模板：先问 `ShouldOwnerDeathTriggerFatal`，再查 `Results.SelectMany(...).WasTargetKilled` |
| 9 | 药水效果没拿到目标 | 目标型药水 `OnUse` 首行 `AssertValidForTargetedPotion(target)` |
| 10 | 忘记 `CanBeGeneratedInCombat => false` | 回血/上限/复活类内容要禁随机生成 |
| 11 | 随机生成出"官方不该出现的组合" | 卡/药水都有 `IsMock` 与 Mocks 目录——统计与随机池注意排除 |
| 12 | 本地化词不对 | 用官方名：人工制品/集中/奇巧/固有/虚无/灾厄/覆甲/仪式/辉星/奥斯提（以游戏内实际显示为准） |
| 13 | 类名随手起 | 类名 = 本地化键 = Power 占位符名，起错全链路难看 |
| 14 | 直接 new 模型类 | 模型由 `ModelDb` 注册，重复 ID 会抛 `DuplicateModelException`——命名冲突先查这里 |

---

## 6. 资料地图

| 需要什么 | 去哪 |
|---|---|
| 某个钩子/命令的完整签名与官方注释 | 游戏本体反编译源码的 `src/Core/`（官方注释完整，直接读） |
| "某个钩子有哪些官方在用" | 在游戏本体反编译源码里全局搜索钩子名（IDE 全局搜索 / grep 均可） |
| 官方三语译名（写本地化） | 游戏本体反编译资源里的 `localization/{zhs,eng,jpn}/`（powers/cards/card_keywords 等 JSON） |
| mod 搭建 / 调试 / 热重载 / 控制台发卡 | 《官方教程》仓库（见文首链接）的 `Basics/` 篇（01 环境、07 调试、09 控制台指令） |

**建议的上手路径**：§2.1 打击卡 → §2.6 格挡卡 → §2.7 增益卡 → §2.8 攻击带减益（到这里你就覆盖了官方
约 60% 的卡牌形状）→ §3.2/§3.3 各写一个遗物 → §3.6 写一个自己的 Power → 之后按需回 §4 选修图鉴查抄特殊效果。
