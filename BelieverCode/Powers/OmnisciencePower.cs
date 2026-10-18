using BaseLib.Hooks;
using Believer.BelieverCode.Mechanics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
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
/// <item><b>Hand space for the form's miracles (M5)</b> - implements BaseLib's
/// <see cref="IMaxHandSizeModifier"/>. The game's hand limit (10) is too small for a hand plus up to
/// 12 collected miracles, and the engine silently moves cards added to a full hand into the discard
/// pile. BaseLib's max-hand-size patches route all hand-limit checks (including
/// <c>CardPileCmd.Add</c>'s full-hand fallback and draws) through this interface, so the form raises
/// the owner's limit by exactly the number of miracles it grants. That keeps every generated miracle
/// usable instead of dropping some into the discard pile, and the limit returns to normal when the
/// form ends.</item>
/// <item><b>Cleanup at the end of the owner's turn</b> - <see cref="BeforeSideTurnEnd"/> removes any
/// unplayed Miracle cards while the hand still exists (turn-end hand discard happens after this
/// hook, so cleaning later would strand them in the discard pile).</item>
/// <item><b>Exit</b> - <see cref="AfterSideTurnEnd"/> for the enemy side removes the power, which
/// restores card costs, HP loss and the hand limit, and puts the Omniscience ability card back into
/// the player's hand. <see cref="AfterRemoved"/> sweeps any remaining miracle cards (including
/// already-played/exhausted ones) out of every combat pile, so the form can never leave temporary
/// miracles behind or leak them into the permanent deck.</item>
/// </list>
/// </summary>
public sealed class OmnisciencePower : BelieverPower, IMaxHandSizeModifier
{
    private int _miraclesToGrant;

    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// Single (not Counter): the form has no stack count and <c>NPower</c> then shows no amount
    /// label.
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// True once this form generated its miracle cards. Guarantees that a repeated event can never
    /// generate the same miracle twice within one form.
    /// </summary>
    public bool MiraclesGranted { get; private set; }

    /// <summary>How many miracles this form will grant; also the extra hand size the form provides.</summary>
    public int MiraclesToGrant => _miraclesToGrant;

    /// <summary>
    /// Tells the power how many miracles the activation is about to generate, so
    /// <see cref="ModifyMaxHandSize"/> can reserve exactly that much extra hand space before the
    /// generation happens.
    /// </summary>
    public void SetMiraclesToGrant(int count)
    {
        _miraclesToGrant = Math.Max(0, count);
    }

    /// <summary>Marks that this form has generated its miracle cards (called once per form).</summary>
    public void MarkMiraclesGranted() => MiraclesGranted = true;

    /// <summary>
    /// M5: raise the form owner's max hand size by the number of miracles the form grants, using
    /// BaseLib's supported hand-size extension point (<see cref="IMaxHandSizeModifier"/>). Other
    /// players, and the owner after the form ends, keep the normal limit.
    /// </summary>
    public int ModifyMaxHandSize(Player player, int currentMaxHandSize)
        => Owner != null && player == Owner.Player
            ? currentMaxHandSize + _miraclesToGrant
            : currentMaxHandSize;

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
    /// End of the owner's turn, before the hand is flushed: remove every Miracle card (unplayed ones
    /// and anything that already exhausted itself). This keeps temporary miracles from being flushed
    /// into the discard pile and out of the player's usable window.
    /// </summary>
    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner))
        {
            var player = Owner.Player;
            if (player != null) return Omniscience.CleanUpMiracles(player);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// End of the enemy's turn: leave the form. Removal is what restores normal card costs, HP loss
    /// and max hand size (all provided by this power), and the ability card is returned to the hand so
    /// the player can start the next Omniscience once Divinity is full again.
    /// </summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || !CombatManager.Instance.IsInProgress) return;

        var player = Owner.Player;
        await PowerCmd.Remove(this);

        if (player != null) await Omniscience.RestoreActivationCard(player);
    }

    /// <summary>
    /// Runs whenever the form is removed, whatever removed it. Sweeps every remaining miracle card out
    /// of all combat piles (hand/draw/discard/exhaust) so a form that ends - even by an external buff
    /// removal - cannot leave temporary miracles behind or leak them into the deck.
    /// </summary>
    public override async Task AfterRemoved(Creature oldOwner)
    {
        var player = oldOwner?.Player;
        if (player != null) await Omniscience.CleanUpMiracles(player);
    }
}
