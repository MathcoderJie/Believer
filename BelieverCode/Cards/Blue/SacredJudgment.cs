using BaseLib.Extensions;
using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Cards.Blue;

/// <summary>
/// 10/24 蓝卡 · 圣裁 / Sacred Judgment - 2 cost: deal 18 damage; if current Divinity is at least 5,
/// deal 15 additional damage (22 / 18 upgraded).
/// </summary>
public sealed class SacredJudgment() : BelieverCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    private const int DivinityThreshold = 5;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(18m, ValueProp.Move),
        new DynamicVar("ExtraDamage", 15m),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);

        var divinity = Owner.GetResource<BelieverDivinityResource>();
        if (divinity == null || divinity.Amount < DivinityThreshold) return;

        await DamageCmd.Attack(DynamicVars["ExtraDamage"].BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
        DynamicVars["ExtraDamage"].UpgradeValueBy(3m);
    }
}
