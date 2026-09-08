using MegaCrit.Sts2.Core.Entities.Cards;
using SlayTheStella.Scripts.Utils;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheStella.Scripts.Shared.Models;

public abstract class StellaCardModel(
    int energyCost,
    CardType type,
    CardRarity rarity,
    TargetType targetType,
    bool shouldShowInCardLibrary = true)
    : ModCardTemplate(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{StsConsts.ResPath}/images/cards/{GetType().Name}.png"
        // 根据不同类型设置不同卡框
        // FramePath: Type switch
        // {
        //     CardType.Attack => "res://RitsuTest/images/card_frame_attack.png",
        //     CardType.Skill => "res://RitsuTest/images/card_frame_skill.png",
        //     CardType.Power => "res://RitsuTest/images/card_frame_power.png",
        //     _ => ""
        // }
        // PortraitBorderPath: "",
        // BannerTexturePath: ""
    );
}