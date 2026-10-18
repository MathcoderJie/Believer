using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Powers.Blue;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Cards.Blue;

/// <summary>
/// 22/24 蓝卡 · 圣约 / Covenant - 1 cost power: the first time you gain Divinity each turn, gain 1
/// additional Divinity. Upgraded, that trigger also grants 3 Block (implemented as a separate
/// upgraded power, see <see cref="CovenantPowerBlessed"/>).
///
/// The extra Divinity is gained through the M1 resource API; the gain tracker counts it but does not
/// dispatch it to listeners again, so Covenant can never retrigger itself.
/// </summary>
public sealed class Covenant() : BelieverCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsUpgraded)
        {
            await PowerCmd.Apply<CovenantPowerBlessed>(
                choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            return;
        }

        await PowerCmd.Apply<CovenantPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }
}
