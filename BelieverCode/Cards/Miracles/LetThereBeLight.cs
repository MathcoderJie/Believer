using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Believer.BelieverCode.Cards.Miracles;

/// <summary>1/12 要有光 / Let There Be Light - 30 damage to all enemies + 2 Vulnerable.</summary>
public sealed class LetThereBeLight() : MiracleCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = CombatState;
        if (combatState == null) return;

        await DamageCmd.Attack(30m).FromCard(this).TargetingAllOpponents(combatState).Execute(choiceContext);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, combatState.HittableEnemies, 2m, Owner.Creature, this);
    }
}
