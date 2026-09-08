using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using static MegaCrit.Sts2.Core.Platform.PlatformUtil;

namespace SlayTheStella.Scripts.Utils;

public static class Functions
{
    public static string GetPlayerName(Player player)
    {
        var result = GetPlayerNameRaw(RunManager.Instance.NetService.Platform, player.NetId);
        return result.Length == 0 ? player.NetId.ToString() : result;
    }
}