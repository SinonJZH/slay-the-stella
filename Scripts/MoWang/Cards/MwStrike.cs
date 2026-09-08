using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheStella.Scripts.MoWang.Models;
using SlayTheStella.Scripts.Shared.SecondaryRes;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheStella.Scripts.MoWang.Cards;

[RegisterCharacterStarterCard(typeof(MoWang), 5)]
public sealed class MwStrike() : MwHarmonyCardModel(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags =>
    [
        CardTag.Strike
    ];

    // 卡牌基础数值
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move),
        new DamageVar("HarmonyDmg", 1m, ValueProp.Move)
    ];

    // 打出时的效果逻辑
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        if (CheckNoteRequire(cardPlay.Player))
        {
            await DamageCmd.Attack(DynamicVars["HarmonyDmg"].BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .Execute(choiceContext);
        }
    }

    // 升级后的效果逻辑
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }

    public override List<(SecondaryResourceDefinition, int)> GetNoteRequire() =>
    [
        (ResNotes.NoteMelodyOfPummelDefinition, 1)
    ];
}