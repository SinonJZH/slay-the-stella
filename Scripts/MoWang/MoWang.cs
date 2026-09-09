using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SlayTheStella.Scripts.Utils;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Godot;

namespace SlayTheStella.Scripts.MoWang;

[RegisterCharacter]
public class MoWang : ModCharacterTemplate<MoWangCardPool, MoWangRelicPool, MoWangPotionPool>
{
    // 角色名称颜色
    public override Color NameColor => new(0f, 0.273f, 0.558f);

    // 能量图标轮廓颜色
    public override Color EnergyLabelOutlineColor => new(0f, 0.273f, 0.558f);

    // 地图绘制颜色
    public override Color MapDrawingColor => new(0f, 0.273f, 0.558f);

    // 人物性别（男女中立）
    public override CharacterGender Gender => CharacterGender.Feminine;

    // 初始血量和金币
    public override int StartingHp => 99;
    public override int StartingGold => 99;

    public override CharacterAssetProfile AssetProfile => CharacterAssetProfiles.Merge(
        CharacterAssetProfiles.Ironclad(),
        new(
            Scenes: new(
                // 人物模型tscn路径。
                // VisualsPath: "res://Test/scenes/test_character.tscn",
                // 能量表盘tscn路径。
                // EnergyCounterPath: "res://Test/scenes/test_energy_counter.tscn",
                // 商店人物场景。
                // MerchantAnimPath: "res://Test/scenes/test_character_merchant.tscn",
                // 篝火休息场景。
                // RestSiteAnimPath: "res://Test/scenes/test_character_rest_site.tscn"
            ),
            Ui: new(
                // 对于图片，只要是godot支持的格式都可以，例如png,jpg,svg等等，之后不再说明
                // 人物头像路径。自适应大小。
                IconTexturePath: $"{StsConsts.ResPath}/images/MoWang/MoWang_Avatar.png",
                // 游戏左上角头像、角色统计页头像、每日挑战角色头像。这个是场景而不是图片。
                IconPath: $"{StsConsts.ResPath}/ui/MoWang/MoWangIcon.tscn",
                // 人物选择背景。
                CharacterSelectBgPath: $"{StsConsts.ResPath}/ui/MoWang/MoWangBg.tscn",
                // 人物选择图标。
                CharacterSelectIconPath: $"{StsConsts.ResPath}/images/MoWang/MoWang_Select.png",
                // 人物选择图标-锁定状态。
                CharacterSelectLockedIconPath: $"{StsConsts.ResPath}/images/MoWang/MoWang_Select.png",
                // 人物选择过渡动画。
                // CharacterSelectTransitionPath: "res://materials/transitions/ironclad_transition_mat.tres",
                // 地图上的角色标记图标、表情轮盘上的角色头像。
                MapMarkerPath: $"{StsConsts.ResPath}/images/MoWang/MoWang_Icon.png"
            )
            // Vfx: new(
            //     卡牌拖尾场景。
            //     TrailPath: "res://scenes/vfx/card_trail_ironclad.tscn"
            // ),
            // Audio: new(
            //     攻击音效
            //     AttackSfx: null,
            //     施法音效
            //     CastSfx: null,
            //     死亡音效
            //     DeathSfx: null,
            //     角色选择音效
            //     CharacterSelectSfx: null,
            //     过渡音效
            //     CharacterTransitionSfx: "event:/sfx/ui/wipe_ironclad"
            // ),
            // Multiplayer: new(
            //     多人模式-手指。
            //     ArmPointingTexturePath: null,
            //     多人模式剪刀石头布-石头。
            //     ArmRockTexturePath: null,
            //     多人模式剪刀石头布-布。
            //     ArmPaperTexturePath: null,
            //     多人模式剪刀石头布-剪刀。
            //     ArmScissorsTexturePath: null
            // )
            // 其余如果有需要自行取消注释使用
            // Spine: null,
            // VisualCues: null, // 帧动画静态图人物使用，查看角色动画一章
            // WorldProceduralVisuals: null,
            // 以下为让遗物根据你的人物展现不同的图像资源，在列表里添加即可
            // VanillaCardVisualOverrides: [],
            // VanillaRelicVisualOverrides: [
            //     new (CharacterOwnedVanillaRelicModelId.YummyCookie, new("res://icon.svg")) // 美味饼干覆盖
            // ],
            // VanillaPotionVisualOverrides: []
        ));

    // 攻击和施法动画延迟，以对齐动画
    public override float AttackAnimDelay => 0f;
    public override float CastAnimDelay => 0f;

    // 如果你的人物不需要时间线小故事，加上这句。
    public override bool RequiresEpochAndTimeline => false;

    // 自动转换人物场景，让你不需要手动挂脚本。复制即可。
    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.Scenes!.VisualsPath!);

    // 攻击建筑师的攻击特效列表
    public override List<string> GetArchitectAttackVfx() =>
    [
        "vfx/vfx_attack_blunt",
        "vfx/vfx_heavy_blunt",
        "vfx/vfx_attack_slash",
        "vfx/vfx_bloody_impact",
        "vfx/vfx_rock_shatter"
    ];
}
