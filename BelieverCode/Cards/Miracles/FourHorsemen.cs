using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Believer.BelieverCode.Cards.Miracles;

/// <summary>
/// 3/12 天启四骑士 / Four Horsemen - four sequential effects:
/// Conquest (15 to all + 2 Vulnerable), War (20 to all), Famine (15 to all + 2 Weak),
/// Death (45 to the lowest-HP enemy).
/// </summary>
[Pool(typeof(BelieverSignatureCardPool))]
public sealed class FourHorsemen() : MiracleCard(0, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = CombatState;
        if (combatState == null) return;

        // 征服 / Conquest
        await DamageCmd.Attack(15m).FromCard(this).TargetingAllOpponents(combatState).Execute(choiceContext);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, combatState.HittableEnemies, 2m, Owner.Creature, this);

        // 战争 / War
        await DamageCmd.Attack(20m).FromCard(this).TargetingAllOpponents(combatState).Execute(choiceContext);

        // 饥荒 / Famine
        await DamageCmd.Attack(15m).FromCard(this).TargetingAllOpponents(combatState).Execute(choiceContext);
        await PowerCmd.Apply<WeakPower>(choiceContext, combatState.HittableEnemies, 2m, Owner.Creature, this);

        // 死亡 / Death - lowest current HP enemy
        var lowest = combatState.HittableEnemies.OrderBy(creature => creature.CurrentHp).FirstOrDefault();
        if (lowest != null)
        {
            await DamageCmd.Attack(45m).FromCard(this).Targeting(lowest).Execute(choiceContext);
        }
    }
}
