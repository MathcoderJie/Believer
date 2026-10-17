using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace Believer.BelieverCode.Miracles;

/// <summary>
/// Base class for 神迹 / Miracle cards: temporary cards that Omniscience grants from the run's
/// unlocked Miracles and that may only be used while Omniscience is active.
///
/// Contract (all enforced by the M2 core, none of it needs to be repeated by a concrete Miracle):
/// <list type="bullet">
/// <item><see cref="CardKeyword.Exhaust"/> - a played Miracle always leaves combat, so it can never
/// be shuffled back into the draw pile or played outside Omniscience.</item>
/// <item><see cref="IsPlayable"/> - a Miracle is only playable while its owner has
/// <see cref="OmnisciencePower"/>, even if a card somehow outlives the form.</item>
/// <item>Cleanup - any Miracle still in a combat pile when the owner's turn ends is removed from
/// combat by the Omniscience power, so unplayed Miracles never persist.</item>
/// </list>
///
/// M2 intentionally ships no complete set of the 12 Miracles - only this base class and one
/// development-only test Miracle.
/// </summary>
public abstract class MiracleCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : BelieverSignatureCard(cost, type, rarity, target)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override bool IsPlayable
    {
        get
        {
            // Canonical instances have no owner; they are only used for previews.
            if (!IsMutable) return false;

            var owner = Owner;
            return owner != null && owner.Creature != null && owner.Creature.HasPower<OmnisciencePower>();
        }
    }
}
