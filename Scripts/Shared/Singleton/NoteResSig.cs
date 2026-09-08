using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SlayTheStella.Scripts.Shared.SecondaryRes;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace SlayTheStella.Scripts.Shared.Singleton;

[RegisterSingleton]
public class NoteResSig() : HookedSingletonModel(HookType.Combat)
{
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        var noteCount = ResNotes.GetAllNoteCounts(player);
        
        
    }
}