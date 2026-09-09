using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
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
    /// <summary>
    /// 依据 <see cref="GetDiscNote"/> 自动为卡面悬停提示附加对应音符的悬停提示
    /// （读 static_hover_tips 本地化表，含图标），并合并 <see cref="MoreAdditionalHoverTips"/>。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            foreach (var note in GetDiscNote())
            {
                var tip = ResNotes.GetNoteHoverTip(note);
                if (tip is not null)
                {
                    yield return tip;
                }
            }
            foreach (var tip in MoreAdditionalHoverTips)
            {
                yield return tip;
            }
        }
    }

    /// <summary>
    /// 供子类额外声明的卡面悬停提示（与自动附加的音符提示合并）。
    /// 注意：若子类直接覆写 <see cref="AdditionalHoverTips"/> 会完全替换本实现，请改用此扩展点。
    /// </summary>
    protected virtual IEnumerable<IHoverTip> MoreAdditionalHoverTips => [];

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

    public abstract List<SecondaryResourceDefinition> GetDiscNote();

    protected async Task GainNoteAfterPlay(CardPlay cardPlay)
    {
        foreach (var def in GetDiscNote())
        {
            await ResNotes.GainNote(cardPlay.Player, def, 1);
        }
    }
}
