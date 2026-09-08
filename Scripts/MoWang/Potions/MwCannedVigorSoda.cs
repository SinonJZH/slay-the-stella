using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SlayTheStella.Scripts.MoWang.Models;

namespace SlayTheStella.Scripts.MoWang.Potions;

public class MwCannedVigorSoda() : MwPotionModel(PotionRarity.Common, PotionUsage.CombatOnly, TargetType.AnyPlayer)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(3)
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        AssertValidForTargetedPotion(target);
        NCombatRoom.Instance?.PlaySplashVfx(target, new Color("f2e35c"));
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, target.Player);
    }
}