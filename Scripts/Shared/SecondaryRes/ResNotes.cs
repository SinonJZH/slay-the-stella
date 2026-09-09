using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using SlayTheStella.Scripts.UI;
using SlayTheStella.Scripts.Utils;
using STS2RitsuLib;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace SlayTheStella.Scripts.Shared.SecondaryRes;

/// <summary>
/// 玩家当前持有的全部 13 种音符数量的快照（对应 <see cref="ResNotes"/>），
/// 由 <see cref="ResNotes.GetAllNoteCounts"/> 生成，可通过 <c>Pummel</c>、<c>Luck</c> 等属性访问具体音符数量。
/// </summary>
public sealed class NoteCount
{
    internal NoteCount(
        int pummel, int luck, int burst, int stamina, int focus, int skill, int ultimate,
        int ignis, int aqua, int ventus, int terra, int lux, int umbra)
    {
        Pummel = pummel;
        Luck = luck;
        Burst = burst;
        Stamina = stamina;
        Focus = focus;
        Skill = skill;
        Ultimate = ultimate;
        Ignis = ignis;
        Aqua = aqua;
        Ventus = ventus;
        Terra = terra;
        Lux = lux;
        Umbra = umbra;
    }

    public int Pummel { get; }
    public int Luck { get; }
    public int Burst { get; }
    public int Stamina { get; }
    public int Focus { get; }
    public int Skill { get; }
    public int Ultimate { get; }
    public int Ignis { get; }
    public int Aqua { get; }
    public int Ventus { get; }
    public int Terra { get; }
    public int Lux { get; }
    public int Umbra { get; }

    /// <summary>
    /// 根据传入的 <see cref="SecondaryResourceDefinition"/> 返回对应音符在本快照中的数量；
    /// 若 definition 不属于本模组注册的 13 种音符资源（或为 null），默认返回 0。
    /// </summary>
    public int GetCount(SecondaryResourceDefinition definition)
    {
        if (definition is null)
        {
            return 0;
        }

        var id = definition.Id;

        if (id == ResNotes.NoteMelodyOfPummelId) return Pummel;
        if (id == ResNotes.NoteMelodyOfLuckId) return Luck;
        if (id == ResNotes.NoteMelodyOfBurstId) return Burst;
        if (id == ResNotes.NoteMelodyOfStaminaId) return Stamina;
        if (id == ResNotes.NoteMelodyOfFocusId) return Focus;
        if (id == ResNotes.NoteMelodyOfSkillId) return Skill;
        if (id == ResNotes.NoteMelodyOfUltimateId) return Ultimate;
        if (id == ResNotes.NoteMelodyOfIgnisId) return Ignis;
        if (id == ResNotes.NoteMelodyOfAquaId) return Aqua;
        if (id == ResNotes.NoteMelodyOfVentusId) return Ventus;
        if (id == ResNotes.NoteMelodyOfTerraId) return Terra;
        if (id == ResNotes.NoteMelodyOfLuxId) return Lux;
        if (id == ResNotes.NoteMelodyOfUmbraId) return Umbra;

        return 0;
    }
}

/// <summary>
/// 《星塔旅人》秘纹系统 13 种音符（Musical Note）的次级资源注册，
/// 对应本地化术语表 _manual/stella-sora-glossary.md 3.5 节。
/// 命名规则：每种音符暴露一对静态成员 ——
///   NoteMelodyOfFocus           完整资源 Id（供 SecondaryResourceCmd 等使用）；
///   NoteMelodyOfFocusDefinition 已注册的 SecondaryResourceDefinition。
/// 完整 Id 形如 SLAY_THE_STELLA_SECONDARY_RESOURCE_NOTE_MELODY_OF_FOCUS。
/// </summary>
public static class ResNotes
{
    // ---------- 强攻之音（Melody of Pummel / 強撃の音符 / 攻击力 +0.3%） ----------
    public static string NoteMelodyOfPummelId { get; private set; } = string.Empty;
    public static SecondaryResourceDefinition NoteMelodyOfPummelDefinition { get; private set; } = null!;

    // ---------- 幸运之音（Melody of Luck / 幸運の音符 / 暴击率 +0.1%） ----------
    public static string NoteMelodyOfLuckId { get; private set; } = string.Empty;
    public static SecondaryResourceDefinition NoteMelodyOfLuckDefinition { get; private set; } = null!;

    // ---------- 暴发之音（Melody of Burst / 爆撃の音符 / 暴击伤害 +0.46%） ----------
    public static string NoteMelodyOfBurstId { get; private set; } = string.Empty;
    public static SecondaryResourceDefinition NoteMelodyOfBurstDefinition { get; private set; } = null!;

    // ---------- 体力之音（Melody of Stamina / 体力の音符 / 最大 HP +0.3%） ----------
    public static string NoteMelodyOfStaminaId { get; private set; } = string.Empty;
    public static SecondaryResourceDefinition NoteMelodyOfStaminaDefinition { get; private set; } = null!;

    // ---------- 专注之音（Melody of Focus / 集中の音符 / 普通攻击伤害 +1.2%） ----------
    public static string NoteMelodyOfFocusId { get; private set; } = string.Empty;
    public static SecondaryResourceDefinition NoteMelodyOfFocusDefinition { get; private set; } = null!;

    // ---------- 技巧之音（Melody of Skill / 器用の音符 / 技能伤害 +0.46%） ----------
    public static string NoteMelodyOfSkillId { get; private set; } = string.Empty;
    public static SecondaryResourceDefinition NoteMelodyOfSkillDefinition { get; private set; } = null!;

    // ---------- 绝招之音（Melody of Ultimate / 必殺の音符 / 必杀技伤害 +1%） ----------
    public static string NoteMelodyOfUltimateId { get; private set; } = string.Empty;
    public static SecondaryResourceDefinition NoteMelodyOfUltimateDefinition { get; private set; } = null!;

    // ---------- 火之音（Melody of Ignis / 火の音符 / 火属性伤害 +0.3%） ----------
    public static string NoteMelodyOfIgnisId { get; private set; } = string.Empty;
    public static SecondaryResourceDefinition NoteMelodyOfIgnisDefinition { get; private set; } = null!;

    // ---------- 水之音（Melody of Aqua / 水の音符 / 水属性伤害 +0.3%） ----------
    public static string NoteMelodyOfAquaId { get; private set; } = string.Empty;
    public static SecondaryResourceDefinition NoteMelodyOfAquaDefinition { get; private set; } = null!;

    // ---------- 风之音（Melody of Ventus / 風の音符 / 风属性伤害 +0.3%） ----------
    public static string NoteMelodyOfVentusId { get; private set; } = string.Empty;
    public static SecondaryResourceDefinition NoteMelodyOfVentusDefinition { get; private set; } = null!;

    // ---------- 地之音（Melody of Terra / 地の音符 / 地属性伤害 +0.3%） ----------
    public static string NoteMelodyOfTerraId { get; private set; } = string.Empty;
    public static SecondaryResourceDefinition NoteMelodyOfTerraDefinition { get; private set; } = null!;

    // ---------- 光之音（Melody of Lux / 光の音符 / 光属性伤害 +0.3%） ----------
    public static string NoteMelodyOfLuxId { get; private set; } = string.Empty;
    public static SecondaryResourceDefinition NoteMelodyOfLuxDefinition { get; private set; } = null!;

    // ---------- 暗之音（Melody of Umbra / 闇の音符 / 暗属性伤害 +0.3%） ----------
    public static string NoteMelodyOfUmbraId { get; private set; } = string.Empty;
    public static SecondaryResourceDefinition NoteMelodyOfUmbraDefinition { get; private set; } = null!;

    /// <summary>
    /// 注册全部音符资源。需在模组初始化（Entry.Init）中调用一次，可重复调用（重复注册返回首次定义）。
    /// </summary>
    public static void Register()
    {
        var registry = RitsuLibFramework.GetSecondaryResourceRegistry(StsConsts.ModId);

        NoteMelodyOfPummelDefinition = RegisterNote(registry, "note_melody_of_pummel", "NoteMelodyOfPummel");
        NoteMelodyOfPummelId = NoteMelodyOfPummelDefinition.Id;

        NoteMelodyOfLuckDefinition = RegisterNote(registry, "note_melody_of_luck", "NoteMelodyOfLuck");
        NoteMelodyOfLuckId = NoteMelodyOfLuckDefinition.Id;

        NoteMelodyOfBurstDefinition = RegisterNote(registry, "note_melody_of_burst", "NoteMelodyOfBurst");
        NoteMelodyOfBurstId = NoteMelodyOfBurstDefinition.Id;

        NoteMelodyOfStaminaDefinition = RegisterNote(registry, "note_melody_of_stamina", "NoteMelodyOfStamina");
        NoteMelodyOfStaminaId = NoteMelodyOfStaminaDefinition.Id;

        NoteMelodyOfFocusDefinition = RegisterNote(registry, "note_melody_of_focus", "NoteMelodyOfFocus");
        NoteMelodyOfFocusId = NoteMelodyOfFocusDefinition.Id;

        NoteMelodyOfSkillDefinition = RegisterNote(registry, "note_melody_of_skill", "NoteMelodyOfSkill");
        NoteMelodyOfSkillId = NoteMelodyOfSkillDefinition.Id;

        NoteMelodyOfUltimateDefinition =
            RegisterNote(registry, "note_melody_of_ultimate", "NoteMelodyOfUltimate");
        NoteMelodyOfUltimateId = NoteMelodyOfUltimateDefinition.Id;

        NoteMelodyOfIgnisDefinition = RegisterNote(registry, "note_melody_of_ignis", "NoteMelodyOfIgnis");
        NoteMelodyOfIgnisId = NoteMelodyOfIgnisDefinition.Id;

        NoteMelodyOfAquaDefinition = RegisterNote(registry, "note_melody_of_aqua", "NoteMelodyOfAqua");
        NoteMelodyOfAquaId = NoteMelodyOfAquaDefinition.Id;

        NoteMelodyOfVentusDefinition = RegisterNote(registry, "note_melody_of_ventus", "NoteMelodyOfVentus");
        NoteMelodyOfVentusId = NoteMelodyOfVentusDefinition.Id;

        NoteMelodyOfTerraDefinition = RegisterNote(registry, "note_melody_of_terra", "NoteMelodyOfTerra");
        NoteMelodyOfTerraId = NoteMelodyOfTerraDefinition.Id;

        NoteMelodyOfLuxDefinition = RegisterNote(registry, "note_melody_of_lux", "NoteMelodyOfLux");
        NoteMelodyOfLuxId = NoteMelodyOfLuxDefinition.Id;

        NoteMelodyOfUmbraDefinition = RegisterNote(registry, "note_melody_of_umbra", "NoteMelodyOfUmbra");
        NoteMelodyOfUmbraId = NoteMelodyOfUmbraDefinition.Id;

        RegisterNotesCombatUi(registry);
        RegisterNotesCardUi(registry);

        StsLogger.InfoDebug("Secondary Resource Notes Registered");
    }

    /// <summary>获取玩家当前持有的全部 13 种音符数量快照（按注册顺序 Pummel → Umbra），并记录一条汇总日志。</summary>
    public static NoteCount GetAllNoteCounts(Player player, bool logDebug = false)
    {
        var counts = new NoteCount(
            SecondaryResourceCmd.Get(player, NoteMelodyOfPummelId),
            SecondaryResourceCmd.Get(player, NoteMelodyOfLuckId),
            SecondaryResourceCmd.Get(player, NoteMelodyOfBurstId),
            SecondaryResourceCmd.Get(player, NoteMelodyOfStaminaId),
            SecondaryResourceCmd.Get(player, NoteMelodyOfFocusId),
            SecondaryResourceCmd.Get(player, NoteMelodyOfSkillId),
            SecondaryResourceCmd.Get(player, NoteMelodyOfUltimateId),
            SecondaryResourceCmd.Get(player, NoteMelodyOfIgnisId),
            SecondaryResourceCmd.Get(player, NoteMelodyOfAquaId),
            SecondaryResourceCmd.Get(player, NoteMelodyOfVentusId),
            SecondaryResourceCmd.Get(player, NoteMelodyOfTerraId),
            SecondaryResourceCmd.Get(player, NoteMelodyOfLuxId),
            SecondaryResourceCmd.Get(player, NoteMelodyOfUmbraId));

        if (logDebug)
        {
            StsLogger.InfoDebug(
                $"Player {Functions.GetPlayerName(player)} current notes count: " +
                $"Pummel:{counts.Pummel}, Luck:{counts.Luck}, Burst:{counts.Burst}, Stamina:{counts.Stamina}, " +
                $"Focus:{counts.Focus}, Skill:{counts.Skill}, Ultimate:{counts.Ultimate}, " +
                $"Ignis:{counts.Ignis}, Aqua:{counts.Aqua}, Ventus:{counts.Ventus}, Terra:{counts.Terra}, " +
                $"Lux:{counts.Lux}, Umbra:{counts.Umbra}");
        }

        return counts;
    }

    /// <summary>
    /// 为已注册的音符定义生成本地化悬停提示（标题/描述读 static_hover_tips 表的
    /// <c>{Id}.title/.description</c>，含图标）。definition 为 null、未注册或不属于本模组
    /// 13 种音符时返回 null（不抛异常）。
    /// </summary>
    public static IHoverTip? GetNoteHoverTip(SecondaryResourceDefinition? definition)
    {
        if (definition is null)
        {
            return null;
        }

        return TryGetNoteDefinition(definition.Id, out var note)
            ? ModSecondaryResourceRegistry.CreateHoverTip(note.Id)
            : null;
    }

    /// <summary>
    /// 按完整资源 Id 获取对应的音符定义；Id 不属于本模组注册的 13 种音符时返回 false。
    /// </summary>
    public static bool TryGetNoteDefinition(string id, out SecondaryResourceDefinition definition)
    {
        if (id == NoteMelodyOfPummelId) { definition = NoteMelodyOfPummelDefinition; return true; }
        if (id == NoteMelodyOfLuckId) { definition = NoteMelodyOfLuckDefinition; return true; }
        if (id == NoteMelodyOfBurstId) { definition = NoteMelodyOfBurstDefinition; return true; }
        if (id == NoteMelodyOfStaminaId) { definition = NoteMelodyOfStaminaDefinition; return true; }
        if (id == NoteMelodyOfFocusId) { definition = NoteMelodyOfFocusDefinition; return true; }
        if (id == NoteMelodyOfSkillId) { definition = NoteMelodyOfSkillDefinition; return true; }
        if (id == NoteMelodyOfUltimateId) { definition = NoteMelodyOfUltimateDefinition; return true; }
        if (id == NoteMelodyOfIgnisId) { definition = NoteMelodyOfIgnisDefinition; return true; }
        if (id == NoteMelodyOfAquaId) { definition = NoteMelodyOfAquaDefinition; return true; }
        if (id == NoteMelodyOfVentusId) { definition = NoteMelodyOfVentusDefinition; return true; }
        if (id == NoteMelodyOfTerraId) { definition = NoteMelodyOfTerraDefinition; return true; }
        if (id == NoteMelodyOfLuxId) { definition = NoteMelodyOfLuxDefinition; return true; }
        if (id == NoteMelodyOfUmbraId) { definition = NoteMelodyOfUmbraDefinition; return true; }
        definition = null!;
        return false;
    }

    /// <summary>获取玩家当前拥有的「强攻之音」数量。</summary>
    public static int GetPummelCount(Player player) => GetCount(player, NoteMelodyOfPummelId, "Pummel");

    /// <summary>为玩家增加 count 个「强攻之音」，成功返回 true。</summary>
    public static Task<bool> GainPummelNote(Player player, int count)
        => GainNote(player, NoteMelodyOfPummelId, "Pummel", count);

    /// <summary>消耗玩家 count 个「强攻之音」，成功返回 true。</summary>
    public static Task<bool> SpendPummelNote(Player player, int count)
        => SpendNote(player, NoteMelodyOfPummelId, "Pummel", count);

    /// <summary>获取玩家当前拥有的「幸运之音」数量。</summary>
    public static int GetLuckCount(Player player) => GetCount(player, NoteMelodyOfLuckId, "Luck");

    /// <summary>为玩家增加 count 个「幸运之音」，成功返回 true。</summary>
    public static Task<bool> GainLuckNote(Player player, int count)
        => GainNote(player, NoteMelodyOfLuckId, "Luck", count);

    /// <summary>消耗玩家 count 个「幸运之音」，成功返回 true。</summary>
    public static Task<bool> SpendLuckNote(Player player, int count)
        => SpendNote(player, NoteMelodyOfLuckId, "Luck", count);

    /// <summary>获取玩家当前拥有的「暴发之音」数量。</summary>
    public static int GetBurstCount(Player player) => GetCount(player, NoteMelodyOfBurstId, "Burst");

    /// <summary>为玩家增加 count 个「暴发之音」，成功返回 true。</summary>
    public static Task<bool> GainBurstNote(Player player, int count)
        => GainNote(player, NoteMelodyOfBurstId, "Burst", count);

    /// <summary>消耗玩家 count 个「暴发之音」，成功返回 true。</summary>
    public static Task<bool> SpendBurstNote(Player player, int count)
        => SpendNote(player, NoteMelodyOfBurstId, "Burst", count);

    /// <summary>获取玩家当前拥有的「体力之音」数量。</summary>
    public static int GetStaminaCount(Player player) => GetCount(player, NoteMelodyOfStaminaId, "Stamina");

    /// <summary>为玩家增加 count 个「体力之音」，成功返回 true。</summary>
    public static Task<bool> GainStaminaNote(Player player, int count)
        => GainNote(player, NoteMelodyOfStaminaId, "Stamina", count);

    /// <summary>消耗玩家 count 个「体力之音」，成功返回 true。</summary>
    public static Task<bool> SpendStaminaNote(Player player, int count)
        => SpendNote(player, NoteMelodyOfStaminaId, "Stamina", count);

    /// <summary>获取玩家当前拥有的「专注之音」数量。</summary>
    public static int GetFocusCount(Player player) => GetCount(player, NoteMelodyOfFocusId, "Focus");

    /// <summary>为玩家增加 count 个「专注之音」，成功返回 true。</summary>
    public static Task<bool> GainFocusNote(Player player, int count)
        => GainNote(player, NoteMelodyOfFocusId, "Focus", count);

    /// <summary>消耗玩家 count 个「专注之音」，成功返回 true。</summary>
    public static Task<bool> SpendFocusNote(Player player, int count)
        => SpendNote(player, NoteMelodyOfFocusId, "Focus", count);

    /// <summary>获取玩家当前拥有的「技巧之音」数量。</summary>
    public static int GetSkillCount(Player player) => GetCount(player, NoteMelodyOfSkillId, "Skill");

    /// <summary>为玩家增加 count 个「技巧之音」，成功返回 true。</summary>
    public static Task<bool> GainSkillNote(Player player, int count)
        => GainNote(player, NoteMelodyOfSkillId, "Skill", count);

    /// <summary>消耗玩家 count 个「技巧之音」，成功返回 true。</summary>
    public static Task<bool> SpendSkillNote(Player player, int count)
        => SpendNote(player, NoteMelodyOfSkillId, "Skill", count);

    /// <summary>获取玩家当前拥有的「绝招之音」数量。</summary>
    public static int GetUltimateCount(Player player) => GetCount(player, NoteMelodyOfUltimateId, "Ultimate");

    /// <summary>为玩家增加 count 个「绝招之音」，成功返回 true。</summary>
    public static Task<bool> GainUltimateNote(Player player, int count)
        => GainNote(player, NoteMelodyOfUltimateId, "Ultimate", count);

    /// <summary>消耗玩家 count 个「绝招之音」，成功返回 true。</summary>
    public static Task<bool> SpendUltimateNote(Player player, int count)
        => SpendNote(player, NoteMelodyOfUltimateId, "Ultimate", count);

    /// <summary>获取玩家当前拥有的「火之音」数量。</summary>
    public static int GetIgnisCount(Player player) => GetCount(player, NoteMelodyOfIgnisId, "Ignis");

    /// <summary>为玩家增加 count 个「火之音」，成功返回 true。</summary>
    public static Task<bool> GainIgnisNote(Player player, int count)
        => GainNote(player, NoteMelodyOfIgnisId, "Ignis", count);

    /// <summary>消耗玩家 count 个「火之音」，成功返回 true。</summary>
    public static Task<bool> SpendIgnisNote(Player player, int count)
        => SpendNote(player, NoteMelodyOfIgnisId, "Ignis", count);

    /// <summary>获取玩家当前拥有的「水之音」数量。</summary>
    public static int GetAquaCount(Player player) => GetCount(player, NoteMelodyOfAquaId, "Aqua");

    /// <summary>为玩家增加 count 个「水之音」，成功返回 true。</summary>
    public static Task<bool> GainAquaNote(Player player, int count)
        => GainNote(player, NoteMelodyOfAquaId, "Aqua", count);

    /// <summary>消耗玩家 count 个「水之音」，成功返回 true。</summary>
    public static Task<bool> SpendAquaNote(Player player, int count)
        => SpendNote(player, NoteMelodyOfAquaId, "Aqua", count);

    /// <summary>获取玩家当前拥有的「风之音」数量。</summary>
    public static int GetVentusCount(Player player) => GetCount(player, NoteMelodyOfVentusId, "Ventus");

    /// <summary>为玩家增加 count 个「风之音」，成功返回 true。</summary>
    public static Task<bool> GainVentusNote(Player player, int count)
        => GainNote(player, NoteMelodyOfVentusId, "Ventus", count);

    /// <summary>消耗玩家 count 个「风之音」，成功返回 true。</summary>
    public static Task<bool> SpendVentusNote(Player player, int count)
        => SpendNote(player, NoteMelodyOfVentusId, "Ventus", count);

    /// <summary>获取玩家当前拥有的「地之音」数量。</summary>
    public static int GetTerraCount(Player player) => GetCount(player, NoteMelodyOfTerraId, "Terra");

    /// <summary>为玩家增加 count 个「地之音」，成功返回 true。</summary>
    public static Task<bool> GainTerraNote(Player player, int count)
        => GainNote(player, NoteMelodyOfTerraId, "Terra", count);

    /// <summary>消耗玩家 count 个「地之音」，成功返回 true。</summary>
    public static Task<bool> SpendTerraNote(Player player, int count)
        => SpendNote(player, NoteMelodyOfTerraId, "Terra", count);

    /// <summary>获取玩家当前拥有的「光之音」数量。</summary>
    public static int GetLuxCount(Player player) => GetCount(player, NoteMelodyOfLuxId, "Lux");

    /// <summary>为玩家增加 count 个「光之音」，成功返回 true。</summary>
    public static Task<bool> GainLuxNote(Player player, int count)
        => GainNote(player, NoteMelodyOfLuxId, "Lux", count);

    /// <summary>消耗玩家 count 个「光之音」，成功返回 true。</summary>
    public static Task<bool> SpendLuxNote(Player player, int count)
        => SpendNote(player, NoteMelodyOfLuxId, "Lux", count);

    /// <summary>获取玩家当前拥有的「暗之音」数量。</summary>
    public static int GetUmbraCount(Player player) => GetCount(player, NoteMelodyOfUmbraId, "Umbra");

    /// <summary>为玩家增加 count 个「暗之音」，成功返回 true。</summary>
    public static Task<bool> GainUmbraNote(Player player, int count)
        => GainNote(player, NoteMelodyOfUmbraId, "Umbra", count);

    /// <summary>消耗玩家 count 个「暗之音」，成功返回 true。</summary>
    public static Task<bool> SpendUmbraNote(Player player, int count)
        => SpendNote(player, NoteMelodyOfUmbraId, "Umbra", count);

    /// <summary>
    /// 为玩家增加 count 个指定 <paramref name="definition" /> 对应的音符，成功返回 true；
    /// definition 为 null 或玩家不在战斗中时返回 false。
    /// </summary>
    public static async Task<bool> GainNote(Player player, SecondaryResourceDefinition definition, int count)
    {
        if (definition is not null) return await GainNote(player, definition.Id, definition.LocalId, count);
        StsLogger.Warn($"Gain note Failed! definition is null.");
        return false;
    }

    /// <summary>获取玩家当前持有的指定音符数量并记录日志。</summary>
    private static int GetCount(Player player, string id, string noteName)
    {
        var count = SecondaryResourceCmd.Get(player, id);
        StsLogger.InfoDebug($"Get player {Functions.GetPlayerName(player)} {noteName} Count :{count}");
        return count;
    }

    /// <summary>为玩家增加 count 个指定音符，成功返回 true。</summary>
    private static async Task<bool> GainNote(Player player, string id, string noteName, int count)
    {
        try
        {
            await SecondaryResourceCmd.Gain(player, id, count);
            StsLogger.InfoDebug($"Gain {noteName} Note for player {Functions.GetPlayerName(player)} Count :{count}");
            return true;
        }
        catch (Exception ex)
        {
            StsLogger.Warn(
                $"Gain {noteName.ToLowerInvariant()} notes for player {Functions.GetPlayerName(player)} Failed! Message: {ex.Message}");
            StsLogger.Error(ex.ToString());
            return false;
        }
    }

    /// <summary>消耗玩家 count 个指定音符，成功返回 true。</summary>
    private static async Task<bool> SpendNote(Player player, string id, string noteName, int count)
    {
        StsLogger.InfoDebug($"Player {Functions.GetPlayerName(player)} spend {noteName} Note Count :{count}");
        return await SecondaryResourceCmd.Spend(player, id, count);
    }

    /// <summary>
    /// 注册音符面板战斗 UI（SlayTheStella/ui/NoteUI.tscn）：
    /// 通过 RitsuLib <c>RegisterCombatUi</c> 将场景挂载到 NCombatUi 作为本模组次级资源的战斗界面，
    /// 进入战斗 / 战斗状态变化时绑定本地玩家显示并刷新，音符数量变化时即时更新计数；
    /// 战斗结束（AnimOut/Deactivate）时由框架自动隐藏。
    /// </summary>
    private static void RegisterNotesCombatUi(ModSecondaryResourceRegistry registry)
    {
        registry.RegisterCombatUi(
            "notes_combat_ui",
            _ =>
            {
                var scene = GD.Load<PackedScene>($"{StsConsts.ResPath}/ui/NoteUI.tscn");
                return scene.Instantiate<NoteUI>();
            },
            ctx => ctx.Node.Bind(ctx.Player),
            ctx => ctx.Node.OnNoteChanged(ctx.Change),
            new NodeAttachmentOptions
            {
                Name = "NoteUI",
                DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName,
            });
    }

    private static void RegisterNotesCardUi(ModSecondaryResourceRegistry registry)
    {
        registry.RegisterCardUi(
            "notes_card_ui",
            _ =>
            {
                var scene = GD.Load<PackedScene>($"{StsConsts.ResPath}/ui/NoteCardUI.tscn");
                return scene.Instantiate<NoteCardUi>();
            },
            ctx => ctx.Node.OnUpdate(ctx.Card),
            new NodeAttachmentOptions
            {
                Name = "NoteUI",
                DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName,
            });
    }

    private static SecondaryResourceDefinition RegisterNote(
        ModSecondaryResourceRegistry registry,
        string localId,
        string iconStem)
    {
        var iconPath = $"{StsConsts.ResPath}/images/icons/{iconStem}.png";
        var definition = registry.Register(localId, CreateNoteDefinition(iconPath));
        return definition;
    }

    /// <summary>
    /// 音符统一的资源定义：起始 0、无上限、回合开始不自动变化、战斗内存储、默认读 static_hover_tips 表
    /// （本地化键位于 localization/&lt;lang&gt;/static_hover_tips.json）。
    /// 图标采用约定路径 res://SlayTheStella/images/icons/&lt;NoteMelodyOfX&gt;.png，
    /// 小/大图标暂时指向同一张（后续玩法需要时再区分 _light 或另配大图）。
    /// </summary>
    private static SecondaryResourceDefinition CreateNoteDefinition(string iconPath) => new(
        defaultAmount: 0,
        baseMaxAmount: null,
        minAmount: 0,
        turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
        persistencePolicy: SecondaryResourcePersistencePolicy.Combat,
        smallIconPath: iconPath,
        largeIconPath: iconPath
    );
}
