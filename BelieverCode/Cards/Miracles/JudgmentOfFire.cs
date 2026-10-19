using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Cards.Miracles;

/// <summary>
/// 7/12 天火审判 / Judgment of Fire - 70 damage to one enemy; if that kills it, 25 damage to all
/// other enemies.
/// </summary>
public sealed class JudgmentOfFire() : MiracleCard(0, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;

        var combatState = CombatState;
        if (combatState == null) return;

        await DamageCmd.Attack(70m).FromCard(this).Targeting(target).Execute(choiceContext);

        if (!target.IsDead) return;

        // "Other enemies": the dead target is no longer hittable, so a full-board attack hits exactly
        // the remaining enemies.
        await DamageCmd.Attack(25m).FromCard(this).TargetingAllOpponents(combatState).Execute(choiceContext);
    }
}
