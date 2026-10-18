using Believer.BelieverCode.Mechanics;
using Believer.BelieverCode.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Powers.Basic;

/// <summary>
/// 施予 / Bestow power card: the first two Divinity gains of each turn each grant
/// <see cref="PowerModel.Amount"/> Block.
/// </summary>
public sealed class BestowPower : BelieverPower
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>Only the first two Divinity gains of a turn trigger this power.</summary>
    private const int MaxTriggersPerTurn = 2;

    private PlayerCombatState? _combatState;
    private Action<Player, int, int>? _handler;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        var playerCombatState = Owner.Player?.PlayerCombatState;
        if (playerCombatState != null)
        {
            _combatState = playerCombatState;
            _handler = OnDivinityGained;
            DivinityGainTracker.AddListener(playerCombatState, _handler);
        }

        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        if (_combatState != null && _handler != null)
        {
            DivinityGainTracker.RemoveListener(_combatState, _handler);
        }

        _combatState = null;
        _handler = null;
        return Task.CompletedTask;
    }

    private void OnDivinityGained(Player player, int amount, int gainCountThisTurn)
    {
        if (gainCountThisTurn > MaxTriggersPerTurn) return;
        if (player != Owner.Player) return;
        if (Owner.CombatState == null) return;

        TaskHelper.RunSafely(CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null, fast: true));
    }
}
