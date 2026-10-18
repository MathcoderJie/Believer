using BaseLib.Extensions;
using Believer.BelieverCode.Mechanics;
using Believer.BelieverCode.Powers;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Powers.Blue;

/// <summary>
/// 圣约 / Covenant power card: the first time the owner gains Divinity each turn, gain 1 additional
/// Divinity.
///
/// The extra gain is counted by <see cref="DivinityGainTracker"/> but never dispatched to listeners
/// again, so Covenant cannot retrigger itself. The upgraded version is
/// <see cref="CovenantPowerBlessed"/>.
/// </summary>
public class CovenantPower : BelieverPower
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

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

        player.GetResource<BelieverDivinityResource>()?.Gain(1);
    }
}
