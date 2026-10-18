using BaseLib.Extensions;
using Believer.BelieverCode.Powers;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Powers.Basic;

/// <summary>
/// 专注 / Concentration power card: the first Skill card played each turn grants
/// <see cref="PowerModel.Amount"/> Divinity.
///
/// The "once per turn" flag is reset lazily by comparing <c>PlayerCombatState.TurnNumber</c>, which
/// keeps it correct across turn boundaries without extra hooks. The Divinity gain goes through M1's
/// resource API (clamped to 7).
/// </summary>
public sealed class ConcentrationPower : BelieverPower
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    private int _lastTurnNumber = -1;
    private bool _triggeredThisTurn;

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner.Player;
        var playerCombatState = player?.PlayerCombatState;
        if (player == null || playerCombatState == null) return Task.CompletedTask;

        if (playerCombatState.TurnNumber != _lastTurnNumber)
        {
            _lastTurnNumber = playerCombatState.TurnNumber;
            _triggeredThisTurn = false;
        }

        if (_triggeredThisTurn) return Task.CompletedTask;
        if (cardPlay.Card.Owner != player) return Task.CompletedTask;
        if (cardPlay.Card.Type != CardType.Skill) return Task.CompletedTask;
        if (!cardPlay.IsFirstInSeries) return Task.CompletedTask;

        _triggeredThisTurn = true;
        player.GetResource<BelieverDivinityResource>()?.Gain(Amount);
        return Task.CompletedTask;
    }
}
