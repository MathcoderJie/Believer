using Believer.BelieverCode.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Cards.Blue;

/// <summary>
/// 13/24 蓝卡 · 终结 / Finality - 2 cost: deal 24 damage; if the target is at or below 50% of its max
/// HP when this is played, deal 12 additional damage (30 / 15 upgraded).
/// </summary>
public sealed class Finality() : BelieverCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(24m, ValueProp.Move),
        new DynamicVar("ExtraDamage", 12m),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

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
        DynamicVars.Damage.UpgradeValueBy(6m);
        DynamicVars["ExtraDamage"].UpgradeValueBy(3m);
    }
}
