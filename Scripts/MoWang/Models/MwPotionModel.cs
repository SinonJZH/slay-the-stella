using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using SlayTheStella.Scripts.Utils;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheStella.Scripts.MoWang.Models;

[RegisterPotion(typeof(MoWangPotionPool), Inherit = true)]
public abstract class MwPotionModel(PotionRarity potionRarity, PotionUsage potionUsage, TargetType targetType)
    : ModPotionTemplate
{
    public override PotionRarity Rarity => potionRarity;
    public override PotionUsage Usage => potionUsage;
    public override TargetType TargetType => targetType;

    public override PotionAssetProfile AssetProfile => new(
        ImagePath: $"{StsConsts.ResPath}/images/potions/{GetType().Name}.png",
        OutlinePath: $"{StsConsts.ResPath}/images/potions/{GetType().Name}_outline.png"
    );
}