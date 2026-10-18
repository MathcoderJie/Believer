using BaseLib.Extensions;
using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Believer.BelieverCode.Cards.Blue;

/// <summary>
/// 20/24 蓝卡 · 破限 / Limit Break - 1 cost: gain 1 temporary Energy; if current Divinity is at
/// least 5, draw 2 cards (3 upgraded).
/// </summary>
public sealed class LimitBreak() : BelieverCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const int DivinityThreshold = 5;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CardsVar(2),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(1m, Owner);

        var divinity = Owner.GetResource<BelieverDivinityResource>();
        if (divinity == null || divinity.Amount < DivinityThreshold) return;

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
