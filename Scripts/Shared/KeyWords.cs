using MegaCrit.Sts2.Core.Entities.Cards;
using SlayTheStella.Scripts.Utils;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace SlayTheStella.Scripts.Shared;

[RegisterOwnedCardKeyword(nameof(Disc), IconPath = $"{StsConsts.ResPath}/images/icons/DiscKeywordIcon.png",
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedKeyword(nameof(Harmony), IconPath = $"{StsConsts.ResPath}/images/icons/HarmonyIcon.png",
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None)]
public sealed class KeyWords
{
    public static readonly CardKeyword Disc =
        ModContentRegistry.GetQualifiedKeywordId(StsConsts.ModId, nameof(Disc)).GetModCardKeyword();

    public static readonly CardKeyword Harmony =
        ModContentRegistry.GetQualifiedKeywordId(StsConsts.ModId, nameof(Harmony)).GetModCardKeyword();
}