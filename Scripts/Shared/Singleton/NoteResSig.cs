using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheStella.Scripts.Shared.SecondaryRes;
using SlayTheStella.Scripts.Utils;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace SlayTheStella.Scripts.Shared.Singleton;

[RegisterSingleton]
public class NoteResSig() : HookedSingletonModel(HookType.Combat), ISecondaryResourceHookListener
{
    public async Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext context)
    {
        StsLogger.InfoDebug(
            $"Player {Functions.GetPlayerName(context.Player)} resources [{context.Definition.Id}] changed: {context.Delta}");

        if (context.Definition.Id.Equals(ResNotes.NoteMelodyOfPummelId))
        {

        }
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        var noteCount = ResNotes.GetAllNoteCounts(player, true);
    }
}
