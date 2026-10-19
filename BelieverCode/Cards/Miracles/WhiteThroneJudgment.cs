using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Cards.Miracles;

/// <summary>
/// 12/12 白座终审 / White Throne Judgment - 35 damage to all enemies. Enemies in a boss encounter take
/// 40 extra damage. After all damage has resolved, non-boss enemies below 30% of their max HP are
/// killed outright.
///
/// The base game has no per-monster boss flag, so bosses are identified through the encounter's room
/// type (<see cref="MiracleRules.IsBossEnemy"/>).
/// </summary>
public sealed class WhiteThroneJudgment() : MiracleCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const decimal BoardDamage = 35m;
    private const decimal BossExtraDamage = 40m;
    private const decimal ExecuteThreshold = 0.3m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = CombatState;
        if (combatState == null) return;

        await DamageCmd.Attack(BoardDamage)
            .FromCard(this)
            .TargetingAllOpponents(combatState)
            .Execute(choiceContext);

        foreach (var boss in combatState.HittableEnemies.Where(MiracleRules.IsBossEnemy).ToList())
        {
            await DamageCmd.Attack(BossExtraDamage)
                .FromCard(this)
                .Targeting(boss)
                .Execute(choiceContext);
        }

        // Execution is decided only after every damage instance above has resolved.
        foreach (var enemy in combatState.HittableEnemies.Where(enemy => !MiracleRules.IsBossEnemy(enemy)).ToList())
        {
            if (enemy.MaxHp > 0 && enemy.CurrentHp < enemy.MaxHp * ExecuteThreshold)
            {
                await CreatureCmd.Kill(enemy);
            }
        }
    }
}
