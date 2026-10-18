using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Believer.BelieverCode.Cards;

namespace Believer.BelieverCode.Cards.Basic;

/// <summary>
/// 12/16 净化 / Purification - 1 cost, remove 1 removable debuff from yourself, then gain 5 Block
/// (8 Block upgraded).
///
/// The base game has no "unremovable power" flag and no debuff-cleanse API; its own idiom (also used
/// by M2 Omniscience) is a power whose <c>TypeForCurrentAmount</c> is <see cref="PowerType.Debuff"/>,
/// removed through <c>PowerCmd.Remove</c> (the only removal path that runs <c>AfterRemoved</c>). Only
/// VISIBLE debuffs are considered, so internal/hidden bookkeeping powers are never touched. When there
/// are several, the one with the highest stack count is removed (ties broken by id for deterministic
/// multiplayer behaviour).
/// </summary>
public sealed class Purification() : BelieverCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(5m, ValueProp.Move),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var candidates = Owner.Creature.Powers
            .Where(power => power != null && power.IsVisible && power.TypeForCurrentAmount == PowerType.Debuff)
            .OrderByDescending(power => power.Amount)
            .ThenBy(power => power.Id.Entry, StringComparer.Ordinal)
            .ToList();

        if (candidates.Count > 0)
        {
            await PowerCmd.Remove(candidates[0]);
        }

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
