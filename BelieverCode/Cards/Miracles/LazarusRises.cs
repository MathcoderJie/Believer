using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Mechanics;
using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Cards.Miracles;

/// <summary>
/// 10/12 拉撒复生 / Lazarus Rises - choose up to 3 ordinary cards from the exhaust pile, return them
/// to hand and make them cost 0 this turn.
///
/// This moves the existing card instances (no copies are created), and the selector returns each card
/// at most once, so no duplicate recovery or infinite copy loop is possible. Permanent card costs are
/// never modified: <c>SetToFreeThisTurn</c> only adds a turn-scoped modifier.
/// </summary>
public sealed class LazarusRises() : MiracleCard(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const int MaxCards = 3;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = Owner;
        if (owner == null) return;

        var exhaustPile = PileType.Exhaust.GetPile(owner);
        if (exhaustPile.Cards.Count == 0) return;

        var selected = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            exhaustPile,
            owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 0, MaxCards),
            MiracleRules.IsOrdinaryCard)).ToList();

        // Reserve hand space so the recovered cards really go to hand even if it is full.
        Omniscience.ReserveHandSpace(owner, selected.Count);

        foreach (var card in selected)
        {
            await CardPileCmd.Add(card, PileType.Hand);
            card.SetToFreeThisTurn();

            if (card.Pile?.Type != PileType.Hand)
            {
                MainFile.Logger.Warn(
                    $"[Miracles] Lazarus Rises: '{card.Id.Entry}' could not be returned to hand (pile: {card.Pile?.Type.ToString() ?? "none"}).");
            }
        }
    }
}
