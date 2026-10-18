using BaseLib.Extensions;
using Believer.BelieverCode.Powers;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace Believer.BelieverCode.Powers.Basic;

/// <summary>
/// 守护 / Guardian power card: at the end of each of the owner's turns, if the owner still has at
/// least 1 Divinity, gain <see cref="PowerModel.Amount"/> Block.
/// </summary>
public sealed class GuardianPower : BelieverPower
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner)) return Task.CompletedTask;

        var player = Owner.Player;
        if (player == null) return Task.CompletedTask;

        var divinity = player.GetResource<BelieverDivinityResource>();
        if (divinity == null || divinity.Amount < 1) return Task.CompletedTask;

        return GrantBlockAsync();
    }

    private async Task GrantBlockAsync()
    {
        if (Owner.CombatState == null) return;

        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null, fast: true);
    }
}
