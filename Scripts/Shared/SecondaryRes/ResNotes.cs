using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using SlayTheStella.Scripts.UI;
using SlayTheStella.Scripts.Utils;
using STS2RitsuLib;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace SlayTheStella.Scripts.Shared.SecondaryRes;

/// <summary>音符注册元数据（<see cref="NoteSpec"/> 表顺序须与 <see cref="NoteType"/> 枚举一致）。</summary>
internal sealed record NoteSpec(NoteType Note, string LocalId, string IconStem);

/// <summary>
/// 《星塔旅人》秘纹系统 13 种音符（Musical Note）的次级资源注册表门面，
/// 对应本地化术语表 _manual/stella-sora-glossary.md 3.5 节。
/// 完整资源 Id 形如 SLAY_THE_STELLA_SECONDARY_RESOURCE_NOTE_MELODY_OF_FOCUS，
/// 由 RitsuLib 在 <see cref="Register"/> 中以 localId（note_melody_of_focus…）注册生成；
/// 外部通过 <see cref="NoteType"/> 访问：<see cref="GetDefinition"/> 取定义、
/// <see cref="TryGetNoteType"/> 按完整 Id 反查、<see cref="GetNoteHoverTip"/> 取悬停提示。
/// </summary>
public static class ResNotes
{
    // ---------- 音符注册元数据表（顺序须与 NoteType 枚举一致，新增音符两处同步追加） ----------
    private static readonly NoteSpec[] Specs =
    [
        new(NoteType.Pummel, "note_melody_of_pummel", "NoteMelodyOfPummel"),
        new(NoteType.Luck, "note_melody_of_luck", "NoteMelodyOfLuck"),
        new(NoteType.Burst, "note_melody_of_burst", "NoteMelodyOfBurst"),
        new(NoteType.Stamina, "note_melody_of_stamina", "NoteMelodyOfStamina"),
        new(NoteType.Focus, "note_melody_of_focus", "NoteMelodyOfFocus"),
        new(NoteType.Skill, "note_melody_of_skill", "NoteMelodyOfSkill"),
        new(NoteType.Ultimate, "note_melody_of_ultimate", "NoteMelodyOfUltimate"),
        new(NoteType.Ignis, "note_melody_of_ignis", "NoteMelodyOfIgnis"),
        new(NoteType.Aqua, "note_melody_of_aqua", "NoteMelodyOfAqua"),
        new(NoteType.Ventus, "note_melody_of_ventus", "NoteMelodyOfVentus"),
        new(NoteType.Terra, "note_melody_of_terra", "NoteMelodyOfTerra"),
        new(NoteType.Lux, "note_melody_of_lux", "NoteMelodyOfLux"),
        new(NoteType.Umbra, "note_melody_of_umbra", "NoteMelodyOfUmbra"),
    ];

    /// <summary>已注册的音符定义（索引 = (int)NoteType，由 <see cref="Register"/> 填充）。</summary>
    private static readonly SecondaryResourceDefinition[] Definitions =
        new SecondaryResourceDefinition[Specs.Length];

    /// <summary>完整资源 Id → 音符种类（供 SecondaryResourceCmd 变更回调等按 Id 反查）。</summary>
    private static readonly Dictionary<string, NoteType> NoteByResourceId = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 注册全部音符资源与音符 UI。需在模组初始化（Entry.Init）中调用一次，可重复调用（重复注册返回首次定义）。
    /// </summary>
    public static void Register()
    {
        var registry = RitsuLibFramework.GetSecondaryResourceRegistry(StsConsts.ModId);

        foreach (var spec in Specs)
        {
            var definition = RegisterNote(registry, spec.LocalId, spec.IconStem);
            Definitions[(int)spec.Note] = definition;
            NoteByResourceId[definition.Id] = spec.Note;
        }

        RegisterNotesCombatUi(registry);
        RegisterNotesCardUi(registry);

        StsLogger.InfoDebug("Secondary Resource Notes Registered");
    }

    /// <summary>按音符种类取已注册定义（供 UI 取图标、悬停提示等；未注册时抛异常）。</summary>
    public static SecondaryResourceDefinition GetDefinition(NoteType note)
    {
        var definition = Definitions[(int)note];
        if (definition is null)
        {
            throw new InvalidOperationException(
                $"Note {note} has not been registered. ResNotes.Register() must be called before GetDefinition.");
        }

        return definition;
    }

    /// <summary>按完整资源 Id 反查音符种类；Id 不属于本模组注册的 13 种音符时返回 false。</summary>
    public static bool TryGetNoteType(string resourceId, out NoteType note)
        => NoteByResourceId.TryGetValue(resourceId, out note);

    /// <summary>
    /// 为指定音符生成本地化悬停提示（标题/描述读 static_hover_tips 表的
    /// <c>{Id}.title/.description</c>，含图标）；音符尚未注册时返回 null。
    /// </summary>
    public static IHoverTip? GetNoteHoverTip(NoteType note)
    {
        var definition = Definitions[(int)note];
        return definition is null ? null : ModSecondaryResourceRegistry.CreateHoverTip(definition.Id);
    }

    /// <summary>获取玩家当前持有的指定音符数量并记录日志。</summary>
    public static int GetCount(Player player, NoteType note)
    {
        var count = SecondaryResourceCmd.Get(player, GetDefinition(note).Id);
        StsLogger.InfoDebug($"Get player {Functions.GetPlayerName(player)} {note} Count :{count}");
        return count;
    }

    /// <summary>为玩家增加 count 个指定音符，成功返回 true。</summary>
    public static async Task<bool> GainNote(Player player, NoteType note, int count)
    {
        var id = GetDefinition(note).Id;
        try
        {
            await SecondaryResourceCmd.Gain(player, id, count);
            StsLogger.InfoDebug($"Gain {note} Note for player {Functions.GetPlayerName(player)} Count :{count}");
            return true;
        }
        catch (Exception ex)
        {
            StsLogger.Warn(
                $"Gain {note.ToString().ToLowerInvariant()} notes for player {Functions.GetPlayerName(player)} Failed! Message: {ex.Message}");
            StsLogger.Error(ex.ToString());
            return false;
        }
    }

    /// <summary>消耗玩家 count 个指定音符，成功返回 true。</summary>
    public static async Task<bool> SpendNote(Player player, NoteType note, int count)
    {
        var id = GetDefinition(note).Id;
        StsLogger.InfoDebug($"Player {Functions.GetPlayerName(player)} spend {note} Note Count :{count}");
        return await SecondaryResourceCmd.Spend(player, id, count);
    }

    /// <summary>获取玩家当前持有的全部音符数量快照（按注册顺序 Pummel → Umbra），可选记录一条汇总日志。</summary>
    public static NoteCount GetAllNoteCounts(Player player, bool logDebug = false)
    {
        var counts = new int[Specs.Length];
        for (var i = 0; i < Specs.Length; i++)
        {
            counts[i] = SecondaryResourceCmd.Get(player, Definitions[i].Id);
        }

        var snapshot = new NoteCount(counts);

        if (logDebug)
        {
            StsLogger.InfoDebug(
                $"Player {Functions.GetPlayerName(player)} current notes count: " +
                string.Join(", ", Specs.Select(spec => $"{spec.Note}:{snapshot.GetCount(spec.Note)}")));
        }

        return snapshot;
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
