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
/// 11/16 奉献 / Dedication - 1 cost, gain 10 Block; if the player currently has at least 3 Divinity,
/// gain 6 additional Block (12 / 8 upgraded). The check only reads Divinity, it never spends it.
/// </summary>
public sealed class Dedication() : BelieverCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    private const int DivinityThreshold = 3;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(10m, ValueProp.Move),
        new DynamicVar("ExtraBlock", 6m),
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
        DynamicVars["ExtraBlock"].UpgradeValueBy(2m);
    }
}
