using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Powers.Basic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Believer.BelieverCode.Cards.Basic;

/// <summary>24/40 专注 / Concentration - 1 cost power: first Skill played each turn grants 1 Divinity (2 upgraded).</summary>
public sealed class Concentration() : BelieverCard(1, CardType.Power, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Divinity", 1m),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ConcentrationPower>(
            choiceContext, Owner.Creature, DynamicVars["Divinity"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Divinity"].UpgradeValueBy(1m);
    }
}
