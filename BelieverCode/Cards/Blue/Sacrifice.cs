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
/// 16/24 蓝卡 · 献祭 / Sacrifice - 1 cost: deal 12 damage to ALL enemies; if current Divinity is at
/// least 5, deal 12 additional damage to ALL enemies (15 / 15 upgraded).
/// </summary>
public sealed class Sacrifice() : BelieverCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const int DivinityThreshold = 5;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(12m, ValueProp.Move),
        new DynamicVar("ExtraDamage", 12m),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = CombatState;
        if (combatState == null) return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .TargetingAllOpponents(combatState)
            .Execute(choiceContext);

        var divinity = Owner.GetResource<BelieverDivinityResource>();
        if (divinity == null || divinity.Amount < DivinityThreshold) return;

        await DamageCmd.Attack(DynamicVars["ExtraDamage"].BaseValue)
            .FromCard(this)
            .TargetingAllOpponents(combatState)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["ExtraDamage"].UpgradeValueBy(3m);
    }
}
