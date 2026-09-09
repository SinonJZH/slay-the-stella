using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using SlayTheStella.Scripts.Shared.SecondaryRes;

namespace SlayTheStella.Scripts.Shared.Models;

public abstract class HarmonyCardModel(
    int energyCost,
    CardType type,
    CardRarity rarity,
    TargetType targetType,
    bool shouldShowInCardLibrary = true)
    : StellaCardModel(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
{
    protected override bool ShouldGlowGoldInternal => CheckNoteRequire(Owner) > 0;

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

    public abstract IReadOnlyList<(NoteType Note, int Amount)> GetNoteRequire();

    /// <summary>
    /// 计算玩家当前持有的音符满足该卡全部音符需求的倍数 n：对每个需求取「持有数量 / 需求数量」
    /// （整数除法，向下取整）作为该音符自身的倍数，返回其中最小者——即全部需求同时达到 n 倍时的最大 n；
    /// 存在任一需求未满足时返回 0，实际参与计算的需求为空（需求列表为空，或全部需求被忽略）时返回 1。
    /// 注意：Amount 非正（含 0）的需求视为无效，直接忽略、不参与倍数计算。
    /// </summary>
    protected int CheckNoteRequire(Player player)
    {
        var playerNotes = ResNotes.GetAllNoteCounts(player);
        var require = GetNoteRequire().Where(req => req.Amount > 0).ToList();
        return require.Count == 0 ? 1 : require.Min(req => playerNotes.GetCount(req.Note) / req.Amount);
    }
}
