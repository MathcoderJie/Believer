using BaseLib.Extensions;
using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Believer.BelieverCode.Cards.Basic;

/// <summary>
/// 14/16 祷告 / Prayer - 1 cost, draw 2 cards; if the player currently has at least 3 Divinity, gain
/// 1 Divinity (draw 3 upgraded; the Divinity gain stays 1).
/// </summary>
public sealed class Prayer() : BelieverCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    private const int DivinityThreshold = 3;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CardsVar(2),
        new DynamicVar("Divinity", 1m),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

        var divinity = Owner.GetResource<BelieverDivinityResource>();
        if (divinity == null || divinity.Amount < DivinityThreshold) return;

        divinity.Gain(DynamicVars["Divinity"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
