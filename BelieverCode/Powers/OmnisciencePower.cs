using Believer.BelieverCode.Mechanics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Powers;

/// <summary>
/// 全知全能 / Omniscience form. Applied by <see cref="Cards.OmniscienceCard"/> when the player spends
/// 7 Divinity, and removed when the enemy's turn ends (the design decision for M2 is that the form -
/// and therefore its damage immunity - survives through the enemy turn so multi-hit enemy attacks are
/// actually covered).
///
/// Implemented entirely with game-native hooks, so nothing is faked by editing max HP or by changing
/// a card's printed/canonical cost:
///
/// <list type="bullet">
/// <item><b>Cards cost 0</b> - <see cref="TryModifyEnergyCostInCombat"/> is the global in-combat cost
/// hook. It is dynamic: cards drawn later in the turn are free too, and normal costs come straight
/// back when this power is removed. <c>CardEnergyCost.Canonical</c> / <c>_base</c> are never
/// written.</item>
/// <item><b>No HP loss</b> - <c>Hook.ModifyHpLost</c> routes all attack, self-damage and poison HP
/// loss through the <c>ModifyHpLostAfterOsty</c> hooks. Returning 0 for the owner zeroes every hit
/// of a multi-hit attack (each hit is a separate damage instance) while block, block-break events
/// and the normal damage events still happen - the same mechanism the vanilla Buffer/Intangible
/// powers use. Note: like Buffer, a fully prevented hit shows no damage number.</item>
/// <item><b>Cleanup at the end of the owner's turn</b> - <see cref="BeforeSideTurnEnd"/> removes any
/// unplayed Miracle cards while the hand still exists (turn-end hand discard happens after this
/// hook, so cleaning later would strand them in the discard pile).</item>
/// <item><b>Exit</b> - <see cref="AfterSideTurnEnd"/> for the enemy side removes the power, which
/// restores card costs and HP loss, and puts the Omniscience ability card back into the player's
/// hand.</item>
/// </list>
/// </summary>
public sealed class OmnisciencePower : BelieverPower
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// Single (not Counter): the form has no stack count and <c>NPower</c> then shows no amount
    /// label.
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// "Cards cost 0 this form" is a dynamic, global cost modifier rather than a per-card
    /// <c>SetToFreeThisTurn()</c> stamp, so it also covers cards drawn or generated later in the
    /// turn. It only ever affects cards owned by this power's owner.
    /// </summary>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (card.IsMutable && card.Owner?.Creature == Owner)
        {
            modifiedCost = 0m;
            return true;
        }

        modifiedCost = originalCost;
        return false;
    }

    public override decimal ModifyHpLostAfterOsty(
        Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
        => PreventedHpLoss(target, amount);

    /// <summary>
    /// Also implemented in the Late phase so a later modifier cannot undo the prevention. Both
    /// overrides return the value unchanged for every creature that is not this power's owner; the
    /// HP-loss hook asks every listener about every creature.
    /// </summary>
    public override decimal ModifyHpLostAfterOstyLate(
        Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
        => PreventedHpLoss(target, amount);

    private decimal PreventedHpLoss(Creature target, decimal amount)
        => target == Owner && CombatManager.Instance.IsInProgress ? 0m : amount;

    /// <summary>
    /// End of the owner's turn, before the hand is flushed: remove every unplayed Miracle (including
    /// any that overflowed into the discard pile because the hand was full when they were granted).
    /// Played Miracles already exhausted themselves.
    /// </summary>
    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner))
        {
            var player = Owner.Player;
            if (player != null) return Omniscience.CleanUpUnusedMiracles(player);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// End of the enemy's turn: leave the form. Removal is what restores normal card costs and HP
    /// loss (both are provided by this power), and the ability card is returned to the hand so the
    /// player can start the next Omniscience once Divinity is full again.
    /// </summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || !CombatManager.Instance.IsInProgress) return;

        var player = Owner.Player;
        await PowerCmd.Remove(this);

        if (player != null) await Omniscience.RestoreActivationCard(player);
    }
}
