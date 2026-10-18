using BaseLib.Extensions;
using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Believer.BelieverCode.Cards.Blue;

/// <summary>
/// 18/24 蓝卡 · 预言 / Prophecy - 1 cost: draw 3 cards, discard 1 card, gain 1 Divinity
/// (draw 4 upgraded; discard and Divinity gain unchanged).
/// </summary>
public sealed class Prophecy() : BelieverCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const int DiscardCount = 1;
    private const int DivinityGain = 1;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CardsVar(3),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

        var discarded = await CardSelectCmd.FromHandForDiscard(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, DiscardCount),
            null,
            this);

        await CardCmd.Discard(choiceContext, discarded);

        Owner.GetResource<BelieverDivinityResource>()?.Gain(DivinityGain);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
