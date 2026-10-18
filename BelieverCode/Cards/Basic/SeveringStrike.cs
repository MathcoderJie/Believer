using Believer.BelieverCode.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Cards.Basic;

/// <summary>
/// 20/40 斩落 / Severing Strike - 1 cost, deal 8 damage; if the target is at or below 50% of its max
/// HP when this is played, deal 5 additional damage (11 / 7 upgraded).
///
/// The HP condition is evaluated before any damage from this card is dealt.
/// </summary>
public sealed class SeveringStrike() : BelieverCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("ExtraDamage", 5m),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        // Integer-safe check: currentHP * 2 <= maxHP is exactly "currentHP <= 50% of maxHP".
        var targetIsLow = cardPlay.Target.MaxHp > 0 && cardPlay.Target.CurrentHp * 2 <= cardPlay.Target.MaxHp;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        if (!targetIsLow) return;

        await DamageCmd.Attack(DynamicVars["ExtraDamage"].BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["ExtraDamage"].UpgradeValueBy(2m);
    }
}
