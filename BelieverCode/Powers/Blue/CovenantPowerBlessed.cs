using BaseLib.Extensions;
using Believer.BelieverCode.Mechanics;
using Believer.BelieverCode.Powers;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Powers.Blue;

/// <summary>
/// 圣约 / Covenant (upgraded) power card: identical to <see cref="CovenantPower"/>, and the trigger
/// also grants 3 Block. An extra Divinity gain during the trigger is not dispatched to listeners, so
/// this power cannot retrigger itself.
/// </summary>
public sealed class CovenantPowerBlessed : BelieverPower
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    private const decimal BlockOnTrigger = 3m;

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

        player.GetResource<BelieverDivinityResource>()?.Gain(1);
        TaskHelper.RunSafely(CreatureCmd.GainBlock(Owner, BlockOnTrigger, ValueProp.Unpowered, null, fast: true));
    }
}
