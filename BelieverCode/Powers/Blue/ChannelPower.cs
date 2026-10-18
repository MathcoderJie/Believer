using Believer.BelieverCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Powers.Blue;

/// <summary>
/// 聚能 / Channel power card: every <see cref="Configure">threshold</see> cards played in a turn,
/// gain 1 Block and draw 1 card, up to 3 triggers per turn (threshold 3 normally, 2 upgraded).
///
/// The threshold is configured by the card so this power can stay a Single stack (no amount label).
/// The card that applied the power is not counted. Counters reset by comparing
/// <c>PlayerCombatState.TurnNumber</c>.
/// </summary>
public sealed class ChannelPower : BelieverPower
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    private const int MaxTriggersPerTurn = 3;

    private int _threshold = 3;
    private int _lastTurnNumber = -1;
    private int _playedThisTurn;
    private int _triggersThisTurn;
    private bool _skipNextCardPlay;

    /// <summary>Called by the card after the power is applied: sets the threshold and ignores the applying card.</summary>
    public void Configure(int threshold, Player player)
    {
        _threshold = Math.Max(1, threshold);
        _lastTurnNumber = player.PlayerCombatState?.TurnNumber ?? -1;
        _playedThisTurn = 0;
        _triggersThisTurn = 0;
        _skipNextCardPlay = true;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner.Player;
        var playerCombatState = player?.PlayerCombatState;
        if (player == null || playerCombatState == null) return;
        if (cardPlay.Card.Owner != player) return;

        if (_skipNextCardPlay)
        {
            _skipNextCardPlay = false;
            return;
        }

        if (playerCombatState.TurnNumber != _lastTurnNumber)
        {
            _lastTurnNumber = playerCombatState.TurnNumber;
            _playedThisTurn = 0;
            _triggersThisTurn = 0;
        }

        if (_triggersThisTurn >= MaxTriggersPerTurn) return;

        _playedThisTurn++;
        if (_playedThisTurn < _threshold) return;

        _playedThisTurn = 0;
        _triggersThisTurn++;

        await CreatureCmd.GainBlock(Owner, 1m, ValueProp.Unpowered, null, fast: true);
        await CardPileCmd.Draw(choiceContext, 1, player);
    }
}
