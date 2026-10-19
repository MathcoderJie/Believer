using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Believer.BelieverCode.Miracles;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace Believer.BelieverCode.Patches;

/// <summary>
/// Deduplicates already-owned Miracles in a normal card reward. It does NOT create Miracles: the
/// normal rare pool now contains all 12 Miracles, and the base CardFactory rolls them as ordinary
/// Rare cards. This only swaps an already-owned Miracle candidate for a not-yet-owned Miracle so a
/// run does not waste rare rewards on duplicates; if all are owned, the original reward is kept.
/// </summary>
[HarmonyPatch(typeof(CardFactory), "CreateForReward", new[] { typeof(Player), typeof(int), typeof(CardCreationOptions) })]
internal static class MiracleRewardDuplicateFilterPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player player, ref IEnumerable<CardCreationResult> __result)
    {
        if (player?.Character is not global::Believer.BelieverCode.Character.Believer)
        {
            return;
        }

        var cards = __result?.ToList();
        if (cards == null || cards.Count == 0) return;

        var available = GetUnownedMiracleCanonicals(player);
        if (available.Count == 0) return;

        var replaced = 0;
        for (var i = 0; i < cards.Count; i++)
        {
            if (cards[i].Card is not MiracleCard miracle) continue;
            if (!MiracleCollectionStore.Has(player, MiracleId.FromCard(miracle))) continue;
            if (available.Count == 0) break;

            var replacement = available[0];
            available.RemoveAt(0);
            var rewardCard = player.RunState.CreateCard(replacement, player);
            cards[i] = new CardCreationResult(rewardCard);
            replaced++;
        }

        if (replaced > 0)
        {
            __result = cards;
            MainFile.Logger.Info($"[Miracles] Replaced {replaced} already-owned Miracle reward candidate(s).");
        }
    }

    private static List<MiracleCard> GetUnownedMiracleCanonicals(Player player)
    {
        var result = new List<MiracleCard>();
        foreach (var id in MiracleCatalog.KnownIds)
        {
            if (MiracleCollectionStore.Has(player, id)) continue;
            if (!MiracleCatalog.TryResolve(id, out var card)) continue;
            if (card is Cards.Dev.DevMiracleTestCard) continue;
            result.Add(card);
        }
        return result;
    }
}

/// <summary>
/// Records a Miracle as acquired whenever the normal game flow adds it to the permanent deck.
///
/// M10.4 removes the old post-generation replacement and the old "never add the Miracle to the
/// deck" interception: Miracles are now ordinary Rare cards in the Believer pool. This prefix only
/// observes the deck add, updates the run's Miracle collection (used by Omniscience), and then lets
/// the vanilla deck add run unchanged. Normal cards are never touched.
/// </summary>
[HarmonyPatch(
    typeof(CardPileCmd),
    "Add",
    new[] { typeof(CardModel), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool) })]
internal static class MiracleAcquisitionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(CardModel card, PileType newPileType)
    {
        if (newPileType != PileType.Deck || card is not MiracleCard miracle || card.Owner == null)
        {
            return true;
        }

        var player = card.Owner;
        var id = MiracleId.FromCard(miracle);
        var result = MiracleCollectionStore.TryAdd(player, id);
        MainFile.Logger.Info(
            $"[Miracles] Acquired normal Miracle '{id}' from deck add -> {result}, " +
            $"owned={MiracleCollectionStore.GetOwnedIds(player.RunState).Count}");

        // Let the original deck add proceed: normal Miracles are legal collectible Rare cards now.
        return true;
    }
}
