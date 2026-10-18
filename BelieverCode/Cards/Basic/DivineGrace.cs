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
/// 15/16 神恩 / Divine Grace - 1 cost, gain 7 Block; if the player currently has at least 1 Divinity,
/// gain 3 additional Block (10 / 4 upgraded).
/// </summary>
public sealed class DivineGrace() : BelieverCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    private const int DivinityThreshold = 1;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(7m, ValueProp.Move),
        new DynamicVar("ExtraBlock", 3m),
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
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["ExtraBlock"].UpgradeValueBy(1m);
    }
}
