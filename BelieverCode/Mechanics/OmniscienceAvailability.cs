using Believer.BelieverCode.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;

namespace Believer.BelieverCode.Mechanics;

/// <summary>
/// Grants the 全知全能 / Omniscience ability card at the start of every combat for Believer players.
///
/// The ability card is intentionally not part of the starting deck (the deck is exactly the 10 basic
/// cards: 5 打击, 4 防御, 1 凝神), but the core mechanic must still work. This provider adds one
/// <see cref="OmniscienceCard"/> to the player's hand when their first turn of a combat starts; the
/// card is Innate + Retain, so it stays available for the whole combat and is recreated automatically
/// in the next combat.
///
/// It never writes to the deck: the card is created as a combat card via
/// <c>CombatState.CreateCard</c> + <c>CardPileCmd.AddGeneratedCardsToCombat</c>. Old saves are not
/// touched, and other characters are ignored.
/// </summary>
public static class OmniscienceAvailability
{
    /// <summary>Subscribes to the game's combat turn-start event. Called once from mod initialization.</summary>
    public static void Initialize()
    {
        var combatManager = CombatManager.Instance;
        if (combatManager == null)
        {
            MainFile.Logger.Warn("[Omniscience] CombatManager was not available; the ability card provider is not installed.");
            return;
        }

        combatManager.TurnStarted += OnTurnStarted;
        MainFile.Logger.Info("[Omniscience] Ability card provider installed (granted at each combat's first player turn).");

        var runManager = RunManager.Instance;
        if (runManager != null) runManager.RunStarted += LogBelieverStartingDeck;
    }

    /// <summary>
    /// Diagnostic for acceptance: logs the Believer's actual starting deck composition when a run starts.
    /// Expected: 10 cards (5 Strike / 4 Defend / 1 Focus, ids BELIEVER-...).
    /// </summary>
    private static void LogBelieverStartingDeck(RunState runState)
    {
        foreach (var player in runState.Players)
        {
            if (player.Character is not Character.Believer) continue;

            var deck = player.Deck.Cards;
            var composition = string.Join(", ", deck
                .GroupBy(card => card.Id.Entry)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key} x{group.Count()}"));

            MainFile.Logger.Info($"[Deck] Believer starting deck ({deck.Count} cards): {composition}");
        }
    }

    private static void OnTurnStarted(CombatState state)
    {
        if (!CombatManager.Instance.IsInProgress) return;
        if (state.CurrentSide != CombatSide.Player) return;

        foreach (var player in state.Players)
        {
            if (player.Character is not Character.Believer) continue;

            var playerCombatState = player.PlayerCombatState;
            if (playerCombatState == null) continue;

            // The ability card has Retain, so once granted it stays in the player's combat piles for
            // the rest of the combat. Only grant it when it is genuinely missing.
            if (playerCombatState.AllCards.Any(card => card is OmniscienceCard)) continue;

            var abilityCard = state.CreateCard<OmniscienceCard>(player);
            TaskHelper.RunSafely(CardPileCmd.AddGeneratedCardsToCombat(
                new[] { abilityCard }, PileType.Hand, player));

            MainFile.Logger.Info("[Omniscience] Granted the Omniscience ability card for the new combat.");
        }
    }
}
