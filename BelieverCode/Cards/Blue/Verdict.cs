using BaseLib.Extensions;
using Believer.BelieverCode.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Cards.Blue;

/// <summary>
/// 11/24 蓝卡 · 裁决 / Verdict - 1 cost: deal 12 damage; the target's number of debuffs adds
/// 4 damage each, capped at 8 (15 base / 5 each / cap 10 upgraded).
///
/// Debuffs are counted the same way the game classifies them: visible powers whose current type is
/// <see cref="PowerType.Debuff"/>.
/// </summary>
public sealed class Verdict() : BelieverCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(12m, ValueProp.Move),
        new DynamicVar("PerDebuff", 4m),
        new DynamicVar("MaxExtra", 8m),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        var debuffCount = cardPlay.Target.Powers.Count(power =>
            power != null && power.IsVisible && power.TypeForCurrentAmount == PowerType.Debuff);

        if (debuffCount <= 0) return;

        var extra = Math.Min(
            DynamicVars["PerDebuff"].BaseValue * debuffCount,
            DynamicVars["MaxExtra"].BaseValue);

        if (extra <= 0m) return;

        await DamageCmd.Attack(extra)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["PerDebuff"].UpgradeValueBy(1m);
        DynamicVars["MaxExtra"].UpgradeValueBy(2m);
    }
}
