using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using Believer.BelieverCode.Character;
using Believer.BelieverCode.Extensions;

namespace Believer.BelieverCode.Potions;

[Pool(typeof(BelieverPotionPool))]
public abstract class BelieverPotion : CustomPotionModel
{
	public override string? CustomPackedImagePath =>
		$"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".PotionImagePath();
	public override string? CustomPackedOutlinePath =>
		$"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".PotionOutlineImagePath();
}