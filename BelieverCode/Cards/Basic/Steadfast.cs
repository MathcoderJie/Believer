using BaseLib.Extensions;
using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Cards.Basic;

/// <summary>
/// 6/8 坚守 / Steadfast - 1 cost, gain 8 Block; if the player currently has at least 3 Divinity,
/// gain 4 additional Block (10 / 5 upgraded).
///
/// The check uses M1's own resource (<see cref="BelieverDivinityResource.Amount"/>) at play time and
/// never spends Divinity, so the card only reads the existing state.
/// </summary>
public sealed class Steadfast() : BelieverCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    /// <summary>Divinity needed for the bonus Block.</summary>
    private const int DivinityThreshold = 3;

    public override bool GainsBlock => true;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(8m, ValueProp.Move),
        new DynamicVar("ExtraBlock", 4m),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        var divinity = Owner.GetResource<BelieverDivinityResource>();
        if (divinity == null || divinity.Amount < DivinityThreshold) return;

        await CreatureCmd.GainBlock(
            Owner.Creature, DynamicVars["ExtraBlock"].BaseValue, ValueProp.Move, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["ExtraBlock"].UpgradeValueBy(1m);
    }
}
