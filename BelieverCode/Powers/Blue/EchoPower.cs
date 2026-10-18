using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using Believer.BelieverCode.Powers;

namespace Believer.BelieverCode.Powers.Blue;

/// <summary>
/// 回响 / Echo power card: the next Attack card played this turn deals extra damage equal to
/// <see cref="PowerModel.Amount"/>% of its original (printed) damage.
///
/// The extra damage is dealt as a separate damage instance, and the attack card's OnPlay is never
/// re-run, so the card's other effects cannot repeat or loop. For single-target attacks the extra
/// damage goes to the same target; for AoE attacks (<c>CardPlay.Target == null</c>) it hits every
/// living enemy, mirroring the attack.
/// </summary>
public sealed class EchoPower : BelieverPower
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner.Player;
        if (player == null) return;
        if (cardPlay.Card.Owner != player) return;
        if (cardPlay.Card.Type != CardType.Attack) return;
        if (Amount <= 0) return;

        var combatState = Owner.CombatState;
        if (combatState == null) return;

        var percent = Amount;
        var hasPrintedDamage = cardPlay.Card.DynamicVars.ContainsKey("Damage");
        var extra = hasPrintedDamage
            ? cardPlay.Card.DynamicVars.Damage.BaseValue * percent / 100m
            : 0m;

        if (extra > 0m)
        {
            if (cardPlay.Target != null)
            {
                if (cardPlay.Target.IsAlive)
                {
                    await CreatureCmd.Damage(
                        choiceContext, cardPlay.Target, extra, ValueProp.Unpowered, Owner, null);
                }
            }
            else
            {
                await CreatureCmd.Damage(
                    choiceContext, combatState.HittableEnemies, extra, ValueProp.Unpowered, Owner, null);
            }
        }

        await PowerCmd.Remove(this);
    }

    /// <summary>Unused at the end of the turn: expire instead of carrying over.</summary>
    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner)) return PowerCmd.Remove(this);

        return Task.CompletedTask;
    }
}
