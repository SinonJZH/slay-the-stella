using MegaCrit.Sts2.Core.Entities.Relics;
using SlayTheStella.Scripts.MoWang.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheStella.Scripts.MoWang.Relics;

[RegisterCharacterStarterRelic(typeof(MoWang))]
public class Vita() : MwRelicModel(RelicRarity.Common)
{
}