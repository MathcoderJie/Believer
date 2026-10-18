using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Mechanics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Cards.Basic;

/// <summary>
/// 18/40 追击 / Pursuit - 0 cost, deal 4 damage; if the player has gained Divinity at some point
/// during the current turn, deal 4 additional damage (6 base upgraded, extra unchanged).
///
/// "Gained this turn" is provided by <see cref="DivinityGainTracker"/>, which observes every real
/// Divinity increase (including the M3 relic's turn-start gain) without changing the M1 resource.
/// </summary>
public sealed class Pursuit() : BelieverCard(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("ExtraDamage", 4m),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        var gainedDivinity = DivinityGainTracker.HasGainedThisTurn(Owner);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        if (!gainedDivinity) return;

        await DamageCmd.Attack(DynamicVars["ExtraDamage"].BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
