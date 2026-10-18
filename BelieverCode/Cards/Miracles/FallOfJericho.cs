using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Believer.BelieverCode.Cards.Miracles;

/// <summary>
/// 6/12 耶城崩塌 / Fall of Jericho - remove all enemy Block, then 25 damage to all enemies and
/// 2 Vulnerable.
/// </summary>
[Pool(typeof(BelieverSignatureCardPool))]
public sealed class FallOfJericho() : MiracleCard(0, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = CombatState;
        if (combatState == null) return;

        // Remove block before the damage so the "walls" actually come down.
        foreach (var enemy in combatState.HittableEnemies.ToList())
        {
            if (enemy.Block > 0m)
            {
                await CreatureCmd.LoseBlock(enemy, enemy.Block);
            }
        }

        await DamageCmd.Attack(25m).FromCard(this).TargetingAllOpponents(combatState).Execute(choiceContext);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, combatState.HittableEnemies, 2m, Owner.Creature, this);
    }
}
