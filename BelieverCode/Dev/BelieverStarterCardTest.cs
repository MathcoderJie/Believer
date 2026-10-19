using System;
using Believer.BelieverCode.Cards.Basic;
using Believer.BelieverCode.Character;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// Dev-only starter-card diagnostic. Uses an isolated test RunState, same construction path as the
/// reward pipeline test, and reports actual upgrade/transform eligibility and values.
/// Marker: user://believer_starter_card_test.flag.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Preload))]
internal static class BelieverStarterCardTest
{
    private const string MarkerPath = "user://believer_starter_card_test.flag";

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
            MainFile.Logger.Error($"[StarterCardTest] failed: {exception}");
        }
    }

    private static void Run()
    {
        var player = Player.CreateForNewRun<global::Believer.BelieverCode.Character.Believer>(UnlockState.all, 1uL);
        var runState = RunState.CreateForTest(new[] { player });
        MainFile.Logger.Info($"[StarterCardTest] deck={player.Deck.Cards.Count}");

        var strike = player.Deck.Cards.FirstOrDefault(c => c is StrikeBeliever);
        var defend = player.Deck.Cards.FirstOrDefault(c => c is DefendBeliever);
        LogCard("STRIKE", strike, player.PlayerRng.Transformations);
        LogCard("DEFEND", defend, player.PlayerRng.Transformations);

        if (strike != null)
        {
            strike.UpgradeInternal();
            strike.FinalizeUpgradeInternal();
            MainFile.Logger.Info(
                $"[StarterCardTest] upgraded STRIKE level={strike.CurrentUpgradeLevel} " +
                $"upgraded={strike.IsUpgraded} damage={strike.DynamicVars.Damage.BaseValue} " +
                $"description={strike.GetDescriptionForPile(PileType.Deck)}");
        }
        if (defend != null)
        {
            defend.UpgradeInternal();
            defend.FinalizeUpgradeInternal();
            MainFile.Logger.Info(
                $"[StarterCardTest] upgraded DEFEND level={defend.CurrentUpgradeLevel} " +
                $"upgraded={defend.IsUpgraded} block={defend.DynamicVars.Block.BaseValue} " +
                $"description={defend.GetDescriptionForPile(PileType.Deck)}");
        }

        var transformSource = player.Deck.Cards.FirstOrDefault(c =>
            c is StrikeBeliever && !c.IsUpgraded);
        if (transformSource != null)
        {
            MainFile.Logger.Info(
                $"[StarterCardTest] transform source={transformSource.Id.Entry} " +
                $"transformable={transformSource.IsTransformable} pool={transformSource.Pool?.Id}");
            try
            {
                var replacement = CardFactory.CreateRandomCardForTransform(
                    transformSource, false, player.PlayerRng.Transformations);
                MainFile.Logger.Info(
                    $"[StarterCardTest] transform preview -> {replacement.Id.Entry} " +
                    $"rarity={replacement.Rarity} pool={replacement.Pool?.Id}");

                var actual = CardCmd.TransformToRandom(
                    transformSource,
                    player.PlayerRng.Transformations,
                    MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.None)
                    .GetAwaiter().GetResult();
                MainFile.Logger.Info(
                    $"[StarterCardTest] actual transform success={actual.success} " +
                    $"-> {actual.cardAdded?.Id.Entry ?? "none"} deck={player.Deck.Cards.Count}");
            }
            catch (Exception exception)
            {
                MainFile.Logger.Error($"[StarterCardTest] transform preview failed: {exception}");
            }
        }

        MainFile.Logger.Info("[StarterCardTest] completed.");
    }

    private static void LogCard(string label, CardModel? card, MegaCrit.Sts2.Core.Random.Rng rng)
    {
        if (card == null)
        {
            MainFile.Logger.Error($"[StarterCardTest] {label} NOT FOUND");
            return;
        }
        MainFile.Logger.Info(
            $"[StarterCardTest] {label} id={card.Id.Entry} rarity={card.Rarity} pool={card.Pool?.Id} " +
            $"mutable={card.IsMutable} pile={card.Pile?.Type} maxUpgrade={card.MaxUpgradeLevel} " +
            $"upgradable={card.IsUpgradable} transformable={card.IsTransformable} " +
            $"removable={card.IsRemovable} keywords={string.Join(",", card.Keywords)}");
    }
}
