using Believer.BelieverCode.Cards.Miracles;
using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Powers.Miracles;

/// <summary>
/// 万军降临 / Lord of Hosts (M6): for the rest of the turn, each ordinary card the owner plays deals
/// 5 damage to all enemies. Starts with 8 charges, so it triggers at most 8 times.
///
/// The game's <c>AfterCardPlayed</c> hook runs once per card play, which means multi-hit cards and
/// effects inside the played card cannot count more than once. The hook also fires for each play of a
/// replayed card, so only the first play of a series is counted.
/// </summary>
public sealed class ArmyOfTheLordPower : BelieverPower
{
    public const int Triggers = 8;
    public const decimal DamagePerTrigger = 5m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner.Player;
        if (player == null) return;
        if (cardPlay.Card.Owner != player) return;
        if (!MiracleRules.IsOrdinaryCard(cardPlay.Card)) return;
        if (!cardPlay.IsFirstInSeries) return;
        if (Amount <= 0) return;

        var combatState = Owner.CombatState;
        if (combatState == null) return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext, combatState.HittableEnemies, DamagePerTrigger, ValueProp.Move, Owner, null);
        await PowerCmd.Decrement(this);
    }

    /// <summary>"This turn" - the power expires at the end of the owner's turn even with charges left.</summary>
    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner)) return PowerCmd.Remove(this);

        return Task.CompletedTask;
    }
}
