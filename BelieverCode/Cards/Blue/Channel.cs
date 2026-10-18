using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Powers.Blue;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Believer.BelieverCode.Cards.Blue;

/// <summary>
/// 24/24 蓝卡 · 聚能 / Channel - 1 cost power: every 3 cards played each turn, gain 1 Block and draw
/// 1 card, up to 3 triggers per turn (every 2 cards upgraded).
///
/// The threshold is passed to the power so the power can stay a hidden-amount Single stack.
/// </summary>
public sealed class Channel() : BelieverCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CardsVar(3),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var power = await PowerCmd.Apply<ChannelPower>(
            choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        power?.Configure(DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(-1m);
    }
}
