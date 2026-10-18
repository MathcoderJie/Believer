using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Powers.Blue;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Believer.BelieverCode.Cards.Blue;

/// <summary>
/// 19/24 蓝卡 · 回响 / Echo - 1 cost: this turn, the next Attack card deals extra damage equal to
/// 50% of its original damage (75% upgraded).
///
/// The extra damage is dealt by <see cref="EchoPower"/> as a separate damage instance; the attack
/// card's own effects are not re-run, so nothing can repeat or loop.
/// </summary>
public sealed class Echo() : BelieverCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Percent", 50m),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<EchoPower>(
            choiceContext, Owner.Creature, DynamicVars["Percent"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Percent"].UpgradeValueBy(25m);
    }
}
