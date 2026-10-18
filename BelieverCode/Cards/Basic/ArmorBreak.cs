using Believer.BelieverCode.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Cards.Basic;

/// <summary>
/// 17/40 破甲 / Armor Break - 1 cost, deal 8 damage; if the target has Block when this is played,
/// deal 4 additional damage (10 / 5 upgraded).
///
/// The Block check happens before the first damage instance, so a target whose Block is fully consumed
/// by the base hit still counts.
/// </summary>
public sealed class ArmorBreak() : BelieverCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("ExtraDamage", 4m),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        var targetHadBlock = cardPlay.Target.Block > 0m;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        if (!targetHadBlock) return;

        await DamageCmd.Attack(DynamicVars["ExtraDamage"].BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["ExtraDamage"].UpgradeValueBy(1m);
    }
}
