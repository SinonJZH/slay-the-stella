using MegaCrit.Sts2.Core.Entities.Cards;
using SlayTheStella.Scripts.Shared.Models;
using SlayTheStella.Scripts.Utils;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheStella.Scripts.MoWang.Models;

[RegisterCard(typeof(MoWangCardPool), Inherit = true)]
public abstract class MwCardModel(
    int energyCost,
    CardType type,
    CardRarity rarity,
    TargetType targetType,
    bool shouldShowInCardLibrary = true)
    : StellaCardModel(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
{
}

[RegisterCard(typeof(MoWangCardPool), Inherit = true)]
public abstract class MwDiscCardModel(
    int energyCost,
    CardType type,
    CardRarity rarity,
    TargetType targetType,
    bool shouldShowInCardLibrary = true) : DiscCardModel(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
{
}

[RegisterCard(typeof(MoWangCardPool), Inherit = true)]
public abstract class MwHarmonyCardModel(
    int energyCost,
    CardType type,
    CardRarity rarity,
    TargetType targetType,
    bool shouldShowInCardLibrary = true)
    : HarmonyCardModel(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
{
}