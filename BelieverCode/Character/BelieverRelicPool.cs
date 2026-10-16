using BaseLib.Abstracts;
using Believer.BelieverCode.Extensions;
using Godot;

namespace Believer.BelieverCode.Character;

public class BelieverRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => Believer.Color;

    public override string BigEnergyIconPath => "charui/big_energy.png".ImagePath();
    public override string TextEnergyIconPath => "charui/text_energy.png".ImagePath();
}