using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Mechanics;
using Believer.BelieverCode.Miracles;
using Believer.BelieverCode.Powers.Miracles;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Cards.Miracles;

/// <summary>
/// 9/12 五饼二鱼 / Five Loaves and Two Fish - choose an ordinary card in hand, add 3 temporary copies
/// of it. The copies cost 0 this turn and disappear when the turn ends.
///
/// The engine's <c>CardModel.CreateClone</c> creates real, per-combat copies (never deck cards).
/// Copies are tracked by <see cref="LoavesAndFishesPower"/>, which removes them from every combat pile
/// at the end of the turn. Only ordinary cards can be copied: 神迹 cards and existing temporary copies
/// (clones) are filtered out, so copies cannot be chained.
/// </summary>
public sealed class FiveLoavesAndTwoFish() : MiracleCard(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const int CopyCount = 3;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = Owner;
        if (owner == null) return;

        var selection = (await CardSelectCmd.FromHand(
            choiceContext,
            owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1),
            MiracleRules.IsOrdinaryCard,
            this)).FirstOrDefault();

        if (selection == null) return;

        var power = await PowerCmd.Apply<LoavesAndFishesPower>(
            choiceContext, owner.Creature, 1m, owner.Creature, this);

        if (power == null)
        {
            MainFile.Logger.Error("[Miracles] Five Loaves and Two Fish: could not apply its tracking power; copies skipped.");
            return;
        }

        // Reserve hand space for the copies so a full hand cannot silently push them into the discard
        // pile (the engine's hand-full fallback).
        Omniscience.ReserveHandSpace(owner, CopyCount);

        for (var i = 0; i < CopyCount; i++)
        {
            var copy = selection.CreateClone();
            copy.SetToFreeThisTurn();
            power.Track(copy);
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, owner);
        }
    }
}
