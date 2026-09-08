using MegaCrit.Sts2.Core.Entities.Cards;
using SlayTheStella.Scripts.Shared.SecondaryRes;
using STS2RitsuLib.Combat.SecondaryResources;

namespace SlayTheStella.Scripts.Shared.Models;

public abstract class DiscCardModel(
    int energyCost,
    CardType type,
    CardRarity rarity,
    TargetType targetType,
    bool shouldShowInCardLibrary = true)
    : StellaCardModel(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
{
    public abstract List<SecondaryResourceDefinition> GetDiscNote();

    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            yield return KeyWords.Disc;
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

    protected async Task GainNoteAfterPlay(CardPlay cardPlay)
    {
        foreach (var def in GetDiscNote())
        {
            await ResNotes.GainNote(cardPlay.Player, def, 1);
        }
    }
}