namespace SlayTheStella.Scripts.Shared.SecondaryRes;

/// <summary>
/// 《星塔旅人》秘纹系统 13 种音符（Musical Note）种类，见 _manual/stella-sora-glossary.md 3.5 节。
/// 枚举成员顺序 = 注册顺序 = <see cref="NoteCount"/> 内部数组顺序，新增音符请追加到末尾，勿重排。
/// </summary>
public enum NoteType
{
    /// <summary>强攻之音（Melody of Pummel / 強撃の音符）。</summary>
    Pummel,

    /// <summary>幸运之音（Melody of Luck / 幸運の音符）。</summary>
    Luck,

    /// <summary>暴发之音（Melody of Burst / 爆撃の音符）。</summary>
    Burst,

    /// <summary>体力之音（Melody of Stamina / 体力の音符）。</summary>
    Stamina,

    /// <summary>专注之音（Melody of Focus / 集中の音符）。</summary>
    Focus,

    /// <summary>技巧之音（Melody of Skill / 器用の音符）。</summary>
    Skill,

    /// <summary>绝招之音（Melody of Ultimate / 必殺の音符）。</summary>
    Ultimate,

    /// <summary>火之音（Melody of Ignis / 火の音符）。</summary>
    Ignis,

    /// <summary>水之音（Melody of Aqua / 水の音符）。</summary>
    Aqua,

    /// <summary>风之音（Melody of Ventus / 風の音符）。</summary>
    Ventus,

    /// <summary>地之音（Melody of Terra / 地の音符）。</summary>
    Terra,

    /// <summary>光之音（Melody of Lux / 光の音符）。</summary>
    Lux,

    /// <summary>暗之音（Melody of Umbra / 闇の音符）。</summary>
    Umbra,
}
