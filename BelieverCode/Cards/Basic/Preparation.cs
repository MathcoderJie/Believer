using Believer.BelieverCode.Cards;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Believer.BelieverCode.Cards.Basic;

/// <summary>
/// 13/16 预备 / Preparation - 0 cost, draw 1 card then discard 1 card (draw 2 upgraded, discard
/// stays 1).
///
/// Follows the base game's "Prepared" card: draw first, then show the discard selection and discard
/// the chosen card. <c>CardsVar</c> only tracks the number of cards drawn.
/// </summary>
public sealed class Preparation() : BelieverCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    /// <summary>Cards discarded; never changes with upgrades (only the draw amount is upgraded).</summary>
    private const int DiscardCount = 1;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CardsVar(1),
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
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
