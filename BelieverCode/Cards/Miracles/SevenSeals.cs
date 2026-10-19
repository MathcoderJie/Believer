using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Cards.Miracles;

/// <summary>
/// 8/12 羔羊七印 / Seven Seals of the Lamb - 7 hits of 7 damage to all enemies, then one extra
/// 15 damage hit to all enemies.
/// </summary>
public sealed class SevenSeals() : MiracleCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const int HitCount = 7;
    private const decimal DamagePerHit = 7m;
    private const decimal FinalDamage = 15m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = CombatState;
        if (combatState == null) return;

        await DamageCmd.Attack(DamagePerHit)
            .WithHitCount(HitCount)
            .FromCard(this)
            .TargetingAllOpponents(combatState)
            .Execute(choiceContext);

        await DamageCmd.Attack(FinalDamage)
            .FromCard(this)
            .TargetingAllOpponents(combatState)
            .Execute(choiceContext);
    }
}
