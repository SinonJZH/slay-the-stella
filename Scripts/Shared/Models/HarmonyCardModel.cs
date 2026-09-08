using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using SlayTheStella.Scripts.Shared.SecondaryRes;
using STS2RitsuLib.Combat.SecondaryResources;

namespace SlayTheStella.Scripts.Shared.Models;

public abstract class HarmonyCardModel(
    int energyCost,
    CardType type,
    CardRarity rarity,
    TargetType targetType,
    bool shouldShowInCardLibrary = true)
    : StellaCardModel(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
{
    public abstract List<(SecondaryResourceDefinition, int)> GetNoteRequire();

    protected override bool ShouldGlowGoldInternal => CheckNoteRequire(Owner);

    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            yield return KeyWords.Harmony;
            foreach (var keyword in AdditionalCanonicalKeywords)
            {
                yield return keyword;
            }
        }
    }

    /// <summary>
    /// 供子类额外声明的规范关键词（与自动附加的 Harmony 合并）。
    /// 注意：若子类直接覆写 <see cref="CanonicalKeywords"/> 会完全替换本实现，请改用此扩展点。
    /// </summary>
    protected virtual IEnumerable<CardKeyword> AdditionalCanonicalKeywords => [];

    /// <summary>检查玩家当前持有的音符是否满足该卡的全部需求，满足返回 true。</summary>
    protected bool CheckNoteRequire(Player player)
    {
        var playerNotes = ResNotes.GetAllNoteCounts(player);
        var noteRequire = GetNoteRequire();
        return noteRequire.All(noteDef => playerNotes.GetCount(noteDef.Item1) >= noteDef.Item2);
    }
}