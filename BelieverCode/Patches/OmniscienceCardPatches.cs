using Believer.BelieverCode.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Patches;

/// <summary>
/// Makes the 全知全能 / <see cref="OmniscienceCard"/> ability card impossible to discard.
///
/// The engine has no "cannot be discarded" flag or hook (only <c>CardKeyword.Retain</c>, which just
/// skips the end-of-turn hand flush - already on the card). Every discard effect in the game funnels
/// through <see cref="CardCmd.DiscardAndDraw"/> (both <c>CardCmd.Discard</c> overloads call it), so
/// two narrow Harmony patches are enough:
///
/// <list type="number">
/// <item><see cref="PreventDiscardAndDraw"/> removes the ability card from the cards being discarded,
/// so it can never actually leave the hand, and no "when discarded" hooks/history fire for it.</item>
/// <item><see cref="ExcludeFromDiscardSelection"/> keeps it out of "select cards to discard" UIs, so
/// the player is never offered a choice that would silently do nothing. <c>CardSelectCmd.FromHand</c>
/// already handles an empty candidate list gracefully, so an all-excluded hand is safe.</item>
/// </list>
///
/// Note that this deliberately does NOT patch <c>CardPileCmd.Add</c>/<c>CardPile.AddInternal</c>: those
/// are also used for internal pile bookkeeping (for example the hand-full fallback that moves a card
/// from the play pile into the discard pile), and skipping those moves would strand the card in no
/// pile at all.
/// </summary>
internal static class OmniscienceCardPatches
{
    private static bool IsAbilityCard(CardModel? card) => card is OmniscienceCard;

    /// <summary>Strips the ability card out of every "discard these cards" command.</summary>
    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.DiscardAndDraw))]
    private static class PreventDiscardAndDraw
    {
        [HarmonyPrefix]
        private static void FilterAbilityCard(ref IEnumerable<CardModel> cardsToDiscard)
        {
            if (cardsToDiscard == null) return;
            if (!cardsToDiscard.Any(IsAbilityCard)) return;

            cardsToDiscard = cardsToDiscard.Where(card => !IsAbilityCard(card)).ToList();
        }
    }

    /// <summary>Keeps the ability card out of "choose a card to discard" selections.</summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHandForDiscard))]
    private static class ExcludeFromDiscardSelection
    {
        [HarmonyPrefix]
        private static void ExcludeAbilityCard(ref Func<CardModel, bool>? filter)
        {
            var originalFilter = filter;

            filter = card => !IsAbilityCard(card) && (originalFilter == null || originalFilter(card));
        }
    }
}
