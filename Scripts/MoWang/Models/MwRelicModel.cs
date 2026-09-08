using MegaCrit.Sts2.Core.Entities.Relics;
using SlayTheStella.Scripts.Utils;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheStella.Scripts.MoWang.Models;

[RegisterRelic(typeof(MoWangRelicPool), Inherit = true)]
public abstract class MwRelicModel(RelicRarity relicRarity) : ModRelicTemplate
{
    public override RelicRarity Rarity => relicRarity;

    public override RelicAssetProfile AssetProfile => new(
        // 小图标（原版85x85）
        IconPath: $"{StsConsts.ResPath}/images/relics/{GetType().Name}.png",
        // 轮廓图标（原版85x85）
        IconOutlinePath: $"{StsConsts.ResPath}/images/relics/{GetType().Name}_outline.png",
        // 大图标（原版256x256）
        BigIconPath: $"{StsConsts.ResPath}/images/relics/{GetType().Name}_big.png"
    );
}