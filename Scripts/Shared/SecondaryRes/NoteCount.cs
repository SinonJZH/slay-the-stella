namespace SlayTheStella.Scripts.Shared.SecondaryRes;

/// <summary>
/// 玩家当前持有的全部 13 种音符数量的快照（对应 <see cref="ResNotes"/> 注册表），
/// 由 <see cref="ResNotes.GetAllNoteCounts"/> 生成；内部按 <see cref="NoteType"/> 枚举顺序存储，
/// 通过 <see cref="GetCount"/> 按音符种类取值。
/// </summary>
public sealed class NoteCount
{
    /// <summary>音符种类总数（须与 <see cref="NoteType"/> 成员数一致，新增音符时同步更新）。</summary>
    internal const int Count = 13;

    private readonly int[] _counts; // 按 NoteType 枚举顺序存储

    internal NoteCount(int[] counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        if (counts.Length != Count)
        {
            throw new ArgumentException(
                $"NoteCount 数组长度必须为 {Count}（与 NoteType 成员数一致），实际为 {counts.Length}。",
                nameof(counts));
        }

        _counts = counts;
    }

    /// <summary>获取指定音符在当前快照中的数量。</summary>
    public int GetCount(NoteType note) => _counts[(int)note];
}
