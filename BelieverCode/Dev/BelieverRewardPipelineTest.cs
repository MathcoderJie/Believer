using System;
using System.Linq;
using Believer.BelieverCode.Keywords;
using Believer.BelieverCode.Miracles;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// Dev-only M10.4 test for normal Rare Miracle acquisition. It uses an isolated test RunState and
/// the real CardFactory / CardPileCmd paths. It does not touch the player's save.
/// Marker: user://believer_reward_test.flag.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Preload))]
internal static class BelieverRewardPipelineTest
{
    private const string MarkerPath = "user://believer_reward_test.flag";

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!Godot.FileAccess.FileExists(MarkerPath))
        {
            return;
        }
        try
        {
            Run();
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"[MiracleRewardTest] failed: {exception}");
        }
    }

    private static void Run()
    {
        var player = Player.CreateForNewRun<global::Believer.BelieverCode.Character.Believer>(
            UnlockState.all, 1uL);
        var runState = RunState.CreateForTest(new[] { player });
        MainFile.Logger.Info(
            $"[MiracleRewardTest] test run created; character={player.Character.Id}, deck={player.Deck.Cards.Count}");

        var bossOptions = CardCreationOptions.ForRoom(player, RoomType.Boss);
        var bossCards = CardFactory.CreateForReward(player, 3, bossOptions).ToList();
        MainFile.Logger.Info(
            $"[MiracleRewardTest] BOSS candidates: {string.Join(", ", bossCards.Select(c => $"{c.Card.Id.Entry}[{c.Card.Rarity}]"))}");
        var bossMiracles = bossCards.Where(c => c.Card is MiracleCard).ToList();
        var allHaveKeyword = bossMiracles.All(c => c.Card.Keywords.Contains(BelieverKeywords.Miracle));
        MainFile.Logger.Info(
            $"[MiracleRewardTest] BOSS miracle candidates={bossMiracles.Count}/{bossCards.Count}; " +
            $"allKeyword={allHaveKeyword}; distinct={bossMiracles.Select(c => c.Card.Id).Distinct().Count()}");
        if (bossMiracles.Count == 0 || !allHaveKeyword)
        {
            MainFile.Logger.Error("[MiracleRewardTest] Boss rare reward did not expose keyworded Miracles.");
            return;
        }

        var selected = (MiracleCard)bossMiracles[0].Card;
        var selectedId = MiracleId.FromCard(selected);
        MainFile.Logger.Info(
            $"[MiracleRewardTest] selected description={selected.GetDescriptionForPile(PileType.Deck)}");
        MainFile.Logger.Info(
            $"[MiracleRewardTest] keyword value={(int)BelieverKeywords.Miracle} " +
            $"after={CardKeywordOrder.afterDescription.Contains(BelieverKeywords.Miracle)} " +
            $"before={CardKeywordOrder.beforeDescription.Contains(BelieverKeywords.Miracle)} " +
            $"title={new MegaCrit.Sts2.Core.Localization.LocString("card_keywords", "BELIEVER-MIRACLE.title").GetFormattedText()}");
        var deckBefore = player.Deck.Cards.Count;
        var addResult = CardPileCmd.Add(selected, PileType.Deck).GetAwaiter().GetResult();
        var deckAfter = player.Deck.Cards.Count;
        var owned = MiracleCollectionStore.Has(player, selectedId);
        MainFile.Logger.Info(
            $"[MiracleRewardTest] select result={addResult.success} deck {deckBefore}->{deckAfter} " +
            $"owned={owned} selected={selectedId} inDeck={player.Deck.Cards.Contains(selected)}");

        if (!owned || deckAfter != deckBefore + 1 || !player.Deck.Cards.Contains(selected))
        {
            MainFile.Logger.Error("[MiracleRewardTest] normal Miracle was not added to the deck/collection correctly.");
        }

        // Normal Miracle card is no longer auto-Exhaust in its base class.
        MainFile.Logger.Info(
            $"[MiracleRewardTest] base keywords={string.Join(",", selected.Keywords)}; " +
            $"maxUpgrade={selected.MaxUpgradeLevel}");

        // Duplicate collection protection (a second deck instance must not duplicate the id in the collection).
        var copy = player.RunState.CreateCard(ModelDb.Card<Cards.Miracles.Genesis>(), player);
        CardPileCmd.Add(copy, PileType.Deck).GetAwaiter().GetResult();
        CardPileCmd.Add(copy, PileType.Deck).GetAwaiter().GetResult();
        MainFile.Logger.Info(
            $"[MiracleRewardTest] duplicate deck instance collectionCount={MiracleCollectionStore.GetOwnedIds(runState).Count}");

        // Second boss reward must avoid already-owned Miracles.
        var secondBoss = CardFactory.CreateForReward(player, 3, bossOptions).ToList();
        var secondIds = secondBoss.Where(c => c.Card is MiracleCard)
            .Select(c => c.Card.Id.Entry).ToList();
        MainFile.Logger.Info(
            $"[MiracleRewardTest] SECOND boss ids={string.Join(", ", secondIds)}; " +
            $"containsSelected={secondIds.Contains(selectedId.Value)}");

        // Ordinary monster rewards must keep ordinary cards and only use Miracles through normal rare odds.
        var normal = CardFactory.CreateForReward(
            player, 3, CardCreationOptions.ForRoom(player, RoomType.Monster)).ToList();
        MainFile.Logger.Info(
            $"[MiracleRewardTest] NORMAL candidates: {string.Join(", ", normal.Select(c => $"{c.Card.Id.Entry}[{c.Card.Rarity}]"))}");

        // Omniscience data source sees the acquired Miracle, and every normal Miracle has an enhanced definition.
        var providerCards = new MiracleCollectionProvider()
            .GetUnlockedMiracles(player)
            .ToList();
        MainFile.Logger.Info(
            $"[MiracleRewardTest] Omniscience provider sees {providerCards.Count}: " +
            string.Join(", ", providerCards.Select(c => c.Id.Entry)));
        if (!providerCards.Any(c => c.Id == selected.Id))
        {
            MainFile.Logger.Error("[MiracleRewardTest] Omniscience provider cannot see the selected Miracle.");
        }

        foreach (var baseMiracle in providerCards.OfType<MiracleCard>())
        {
            var enhanced = MiracleEnhancements.GetEnhancedCanonical(baseMiracle);
            MainFile.Logger.Info(
                $"[MiracleRewardTest] enhanced map {baseMiracle.Id.Entry} -> {enhanced?.Id.Entry ?? "NULL"}");
            if (enhanced == null)
            {
                MainFile.Logger.Error($"[MiracleRewardTest] missing enhanced form for {baseMiracle.Id.Entry}");
            }
        }

        MainFile.Logger.Info("[MiracleRewardTest] reward pipeline test completed.");
    }
}
