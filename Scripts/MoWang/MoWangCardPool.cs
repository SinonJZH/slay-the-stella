using Godot;
using SlayTheStella.Scripts.Utils;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace SlayTheStella.Scripts.MoWang;

public class MoWangCardPool : TypeListCardPoolModel
{
    public override string Title => "STSMoWang";
    public override string EnergyColorName => "STSMoWang";
    
    // 描述中使用的能量图标。大小为24x24。
    public override string? TextEnergyIconPath => $"{StsConsts.ResPath}/images/MoWang/energy_mowang.png";
    // tooltip和卡牌左上角的能量图标。大小为74x74。
    public override string? BigEnergyIconPath => $"{StsConsts.ResPath}/images/MoWang/energy_mowang_big.png";
    
    // 卡池的主题色。
    public override Color DeckEntryCardColor => new(0f, 0.273f, 0.558f);
    // 能量表盘文字轮廓颜色
    public override Color EnergyOutlineColor => new(0f, 0.273f, 0.558f);

    // 根据你使用的卡框决定使用哪个Material
    private static readonly Material? _poolFrameMaterial = MaterialUtils.CreateHsvShaderMaterial(0.561f, 2.5f, 0.51f); // 如果你使用原版卡框，使用这个直接替换色调。
    // private static readonly Material? _poolFrameMaterial = MaterialUtils.CreateUnmodulatedHsvShaderMaterial(); // 如果你是自定义卡框，使用这个
    public override Material? PoolFrameMaterial => _poolFrameMaterial;
    
    public override bool IsColorless => false;
}