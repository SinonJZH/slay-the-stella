using Godot;
using System;
using MegaCrit.Sts2.Core.Models;
using SlayTheStella.Scripts.Shared.Models;

public partial class NoteCardUi : Control
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void OnUpdate(CardModel card)
	{
		switch (card)
		{
			case DiscCardModel discCard:
			{
				var notesList = discCard.GetDiscNote();
				for (var i = 0; i < 4; i++)
				{
					if (i < notesList.Count)
					{
						var def = notesList[i];
						var node = GetNode<TextureRect>(new NodePath($"NoteIconContainer/Note{i}"));
						node.Texture = GD.Load<Texture2D>(def.LargeIconPath);
						node.Visible = true;
					}
					else
					{
						GetNode<TextureRect>(new NodePath($"NoteIconContainer/Note{i}")).Visible = false;
					}
				}

				break;
			}
			case HarmonyCardModel harmonyCard:
			{
				var notesList = harmonyCard.GetNoteRequire();
				for (var i = 0; i < 4; i++)
				{
					if (i < notesList.Count)
					{
						var req = notesList[i];
						var node = GetNode<TextureRect>(new NodePath($"NoteIconContainer/Note{i}"));
						node.Texture = GD.Load<Texture2D>(req.Item1.LargeIconPath);
						node.Visible = true;
						var nodeCount = GetNode<Label>(new NodePath($"NoteIconContainer/Note{i}/NoteCount"));
						nodeCount.Text = req.Item2.ToString();
						nodeCount.Visible = true;
					}
					else
					{
						GetNode<TextureRect>(new NodePath($"NoteIconContainer/Note{i}")).Visible = false;
					}
				}

				break;
			}
			default:
				Visible = false;
				break;
		}
	}
}
