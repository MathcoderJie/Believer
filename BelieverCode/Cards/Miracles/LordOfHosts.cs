using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Miracles;
using Believer.BelieverCode.Powers.Miracles;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Cards.Miracles;

/// <summary>
/// 11/12 万军降临 / Lord of Hosts - this turn, whenever an ordinary card is played, deal 5 damage to
/// all enemies, up to 8 times.
///
/// Implemented as a power with 8 charges. The game's <c>AfterCardPlayed</c> hook fires once per card
/// play (not per damage instance), so multi-hit cards and internal effects cannot double-count; the
/// card-play hook only counts the first play of a replayed card series, and 神迹 cards are ignored.
/// </summary>
public sealed class LordOfHosts() : MiracleCard(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ArmyOfTheLordPower>(
            choiceContext, Owner.Creature, ArmyOfTheLordPower.Triggers, Owner.Creature, this);
    }
}
