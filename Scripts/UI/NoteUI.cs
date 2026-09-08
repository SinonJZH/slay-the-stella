using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using SlayTheStella.Scripts.Shared.SecondaryRes;
using STS2RitsuLib.Combat.SecondaryResources;

namespace SlayTheStella.Scripts.UI;

/// <summary>
/// 秘纹音符面板（战斗 UI）——NoteUI.tscn 根节点脚本。
///
/// 面板由 <see cref="ResNotes"/> 通过 RitsuLib <c>RegisterCombatUi</c> 注册为次级资源战斗 UI：
/// 战斗开始时挂载到 NCombatUi 并调用 <see cref="Bind"/> 显示，之后每次音符数量变化由
/// RitsuLib 的路由回调 <see cref="OnNoteChanged"/> 刷新计数。
/// 场景内目前布置了 7 个基础音符的图标与计数 Label（元素之音等其余 6 种暂不在面板上显示）。
/// </summary>
public partial class NoteUI : Control
{
	private readonly record struct NoteBinding(string CounterNodeName, Func<NoteCount, int> Getter);

	/// <summary>面板计数 Label（子节点名，来自 NoteUI.tscn）与 <see cref="ResNotes.GetAllNoteCounts"/> 返回的 <see cref="NoteCount"/> 快照属性取值的映射。</summary>
	private static readonly NoteBinding[] NoteBindings =
	[
		new("PummelCounter", counts => counts.Pummel),
		new("LuckCounter", counts => counts.Luck),
		new("BurstCounter", counts => counts.Burst),
		new("StaminaCounter", counts => counts.Stamina),
		new("FocusCounter", counts => counts.Focus),
		new("SkillCounter", counts => counts.Skill),
		new("UltimateCounter", counts => counts.Ultimate),
	];

	private readonly Dictionary<string, Label> _counters = new();
	private Player? _player;

	public override void _Ready()
	{
		foreach (var binding in NoteBindings)
			_counters[binding.CounterNodeName] = GetNode<Label>(binding.CounterNodeName);
	}

	/// <summary>
	/// 绑定当前战斗玩家并显示面板；玩家为 null（战斗外）时隐藏面板。
	/// 由 RitsuLib 战斗 UI 更新路由在进入战斗 / 战斗状态变化时调用。
	/// </summary>
	public void Bind(Player? player)
	{
		_player = player;
		if (player == null)
		{
			Visible = false;
			return;
		}

		Visible = true;
		RefreshCounters();
	}

	/// <summary>
	/// 音符数量变化时的刷新入口，由 RitsuLib 的数量变化路由调用（只处理本面板绑定的玩家）。
	/// </summary>
	public void OnNoteChanged(SecondaryResourceChangeContext change)
	{
		if (_player == null || !ReferenceEquals(change.Player, _player))
			return;

		RefreshCounters();
	}

	private void RefreshCounters()
	{
		var player = _player;
		if (player == null)
			return;

		var counts = ResNotes.GetAllNoteCounts(player);
		foreach (var binding in NoteBindings)
		{
			if (!_counters.TryGetValue(binding.CounterNodeName, out var label))
				continue;

			label.Text = binding.Getter(counts).ToString();
		}
	}
}
