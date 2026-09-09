using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheStella.Scripts.MoWang.Models;
using SlayTheStella.Scripts.Shared.SecondaryRes;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheStella.Scripts.MoWang.Cards;

[RegisterCharacterStarterCard(typeof(MoWang), 5)]
public class MwDefend() : MwHarmonyCardModel(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override HashSet<CardTag> CanonicalTags =>
    [
        CardTag.Defend
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move),
        new BlockVar("HarmonyBlock", 1m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (CheckNoteRequire(cardPlay.Player) > 0)
        {
            await CreatureCmd.GainBlock(Owner.Creature, (BlockVar)DynamicVars["HarmonyBlock"], cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }

    public override IReadOnlyList<(NoteType Note, int Amount)> GetNoteRequire() =>
    [
        (NoteType.Stamina, 1)
    ];
}
