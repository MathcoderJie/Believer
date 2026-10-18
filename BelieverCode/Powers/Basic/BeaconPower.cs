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
/// 信标 / Beacon power card: the first time the owner gains Divinity each turn, gain
/// <see cref="PowerModel.Amount"/> Block. Stacking two copies adds their amounts together.
///
/// Gains are observed through <see cref="DivinityGainTracker"/> (per-turn count, per-combat state
/// listeners). Block from a power uses <c>ValueProp.Unpowered</c> like the vanilla After Image power.
/// </summary>
public sealed class BeaconPower : BelieverPower
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

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
        if (gainCountThisTurn != 1) return;
        if (player != Owner.Player) return;
        if (Owner.CombatState == null) return;

        TaskHelper.RunSafely(CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null, fast: true));
    }
}
