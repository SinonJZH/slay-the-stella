using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheStella.Scripts.MoWang.Models;

namespace SlayTheStella.Scripts.MoWang.Cards;

/// <summary>
/// 测试卡牌：先对选择的目标造成 2 点伤害，然后对所有敌人造成 1 点伤害。
/// </summary>
public sealed class MwTestCard() : MwCardModel(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    // 卡牌基础数值
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move),                   // 对选择的目标造成的伤害
        new DamageVar("SplashDamage", 1m, ValueProp.Move)    // 对所有敌人造成的伤害
    ];

    // 打出时的效果逻辑
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        ArgumentNullException.ThrowIfNull(CombatState);

        // 先对选择的目标造成 2 点伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        // 然后对所有敌人造成 1 点伤害
        await DamageCmd.Attack(DynamicVars["SplashDamage"].BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .Execute(choiceContext);
    }

    // 升级后的效果逻辑
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars["SplashDamage"].UpgradeValueBy(1m);
    }
}
