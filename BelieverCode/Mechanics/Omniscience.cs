using BaseLib.Extensions;
using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Miracles;
using Believer.BelieverCode.Powers;
using Believer.BelieverCode.Powers.Blue;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Mechanics;

/// <summary>
/// The 鍏ㄧ煡鍏ㄨ兘 / Omniscience activation sequence and its combat-side helpers.
///
/// Everything here runs inside a native card play (<see cref="OmniscienceCard.OnPlay"/>), so every
/// peer executes the same sequence deterministically - including the Divinity spend, which is local
/// per-combat state in M1 and would desync if it were mutated outside of a synchronised action.
///
/// M5: the form now grants the miracles collected in this run (M4 <c>MiracleCollectionStore</c>,
/// reached through the M2 <see cref="MiracleRegistry"/> provider seam) instead of the M2 development
/// placeholder. Generated miracles are temporary combat instances, are never written to the deck, and
/// are removed again when the form ends.
/// </summary>
public static class Omniscience
{
    /// <summary>Cards drawn the moment Omniscience starts.</summary>
    public const int CardsDrawnOnEnter = 3;

    /// <summary>True when the player currently has the Omniscience form.</summary>
    public static bool IsActive(Player player) => player.Creature != null && player.Creature.HasPower<OmnisciencePower>();

    /// <summary>
    /// Runs the full activation, in this order:
    /// <list type="number">
    /// <item>Spend 7 Divinity (the whole bar). Re-validated here so auto-play effects that bypass the
    /// card's playability check cannot trigger a free activation.</item>
    /// <item>Apply <see cref="OmnisciencePower"/>, which immediately makes cards cost 0 and HP loss 0
    /// - before drawing, so nothing drawn can be hurt by the old costs.</item>
    /// <item>Remove all removable debuffs. This deliberately happens before the draw so a
    /// draw-blocking debuff cannot swallow the 3 cards.</item>
    /// <item>Reserve hand space for the collected miracles (see
    /// <see cref="OmnisciencePower.ModifyMaxHandSize"/>), then draw 3 cards.</item>
    /// <item>Generate this run's collected miracles into the hand (M5).</item>
    /// </list>
    /// The card's own cost-0 and damage immunity, plus all cleanup/exit work, live in
    /// <see cref="OmnisciencePower"/>.
    /// </summary>
    public static async Task Activate(PlayerChoiceContext choiceContext, Player? player, CardModel source)
    {
        if (player?.Creature == null) return;
        if (player.PlayerCombatState == null) return;
        if (!CombatManager.Instance.IsInProgress) return;
        if (IsActive(player)) return;

        var divinity = player.GetResource<BelieverDivinityResource>();
        if (divinity == null || !divinity.TrySpend(BelieverDivinityResource.MaxAmount)) return;

        var power = await PowerCmd.Apply<OmnisciencePower>(
            choiceContext, player.Creature, 1m, player.Creature, source);

        if (power == null)
        {
            MainFile.Logger.Error("[Miracles] OmnisciencePower could not be applied; skipping form setup.");
            return;
        }

        await RemoveDebuffs(player);

        // Collect before drawing so the max hand size raised for the form is already in place when the
        // draw and the miracle generation add cards. Miracles collected in this run are unique by id.
        var collectedMiracles = CollectCollectedMiracles(player);
        power.SetMiraclesToGrant(collectedMiracles.Count);

        // M7 钃濆崱 路 鏄惧湥 / Manifestation: extra cards on entering the form. Extending the form with
        // Genesis never re-runs this activation, so this can only happen once per form entry.
        var manifestationExtraDraw = player.Creature.GetPower<ManifestationPower>()?.Amount ?? 0;

        await CardPileCmd.Draw(choiceContext, CardsDrawnOnEnter + manifestationExtraDraw, player);
        await GrantMiracles(player, power, collectedMiracles);
    }

    /// <summary>
    /// Removes every removable debuff from the player.
    ///
    /// The game has no "clear debuffs" API and no debuff-immunity flag on powers, so this follows the
    /// game's own idiom (used by e.g. Misery/Rend when they copy or count debuffs): a power is a
    /// removable debuff when <see cref="PowerModel.TypeForCurrentAmount"/> is
    /// <see cref="PowerType.Debuff"/>. Powers are removed through <c>PowerCmd.Remove</c>, which is
    /// the only removal path that also runs <c>AfterRemoved</c>; a snapshot is taken first because
    /// removal mutates the owner's power list.
    /// </summary>
    private static async Task RemoveDebuffs(Player player)
    {
        if (!CombatManager.Instance.IsInProgress) return;

        var debuffs = player.Creature.Powers
            .Where(power => power != null && power.TypeForCurrentAmount == PowerType.Debuff)
            .ToList();

        foreach (var debuff in debuffs)
        {
            await PowerCmd.Remove(debuff);
        }
    }

    /// <summary>
    /// The run's collected miracles (M4), resolved to canonical <see cref="MiracleCard"/> models and
    /// deduplicated by <see cref="ModelId"/>. Unowned miracles are not included, and ids that no
    /// longer resolve (removed/unimplemented miracle) are skipped by
    /// <c>MiracleCollectionStore.GetOwnedMiracleCards</c> with a warning, so a stale save cannot crash
    /// the form.
    /// </summary>
    private static List<MiracleCard> CollectCollectedMiracles(Player player)
    {
        var collected = new List<MiracleCard>();
        var seen = new HashSet<ModelId>();

        foreach (var card in MiracleRegistry.GetUnlockedMiracles(player))
        {
            if (card is not MiracleCard miracle) continue;
            if (!seen.Add(miracle.Id)) continue;
            collected.Add(miracle);
        }

        return collected;
    }

    /// <summary>
    /// Generates every collected miracle as a temporary combat card in the player's hand.
    ///
    /// Guarantees:
    /// <list type="bullet">
    /// <item>Once per form: the power records that generation already happened, so a repeated event
    /// cannot generate a second copy.</item>
    /// <item>No duplicates: the canonical list is deduplicated by id, and any stale MiracleCard left in
    /// combat is cleaned before generating.</item>
    /// <item>Never in the deck: <c>ICombatState.CreateCard</c> only creates combat instances, and
    /// generation goes through <c>CardPileCmd.AddGeneratedCardsToCombat</c>.</item>
    /// <item>Nothing silently lost: after adding, the hand is verified and any card that did not land
    /// in the hand is reported (and retried) instead of being silently discarded.</item>
    /// </list>
    /// </summary>
    private static async Task GrantMiracles(
        Player player, OmnisciencePower power, IReadOnlyList<MiracleCard> collectedMiracles)
    {
        if (power.MiraclesGranted) return;
        power.MarkMiraclesGranted();

        if (collectedMiracles.Count == 0) return;

        var combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            MainFile.Logger.Error("[Miracles] Cannot generate miracles: no combat state.");
            return;
        }

        // Safety net: leftovers from a previous form (should never exist) must not duplicate this form's
        // miracles.
        await CleanUpMiracles(player);

        var generated = new List<CardModel>(collectedMiracles.Count);
        foreach (var canonical in collectedMiracles)
        {
            var enhanced = MiracleEnhancements.GetEnhancedCanonical(canonical);
            if (enhanced == null)
            {
                MainFile.Logger.Warn(
                    $"[Miracles] No enhanced form is defined for '{canonical.Id.Entry}'; skipping it.");
                continue;
            }
            generated.Add(combatState.CreateCard(enhanced, player));
        }

        await CardPileCmd.AddGeneratedCardsToCombat(generated, PileType.Hand, player);
        await EnsureMiraclesInHand(generated);

        MainFile.Logger.Info(
            $"[Miracles] Generated {generated.Count} enhanced miracle(s) for Omniscience; hand is now {PileType.Hand.GetPile(player).Cards.Count} card(s).");
    }

    /// <summary>
    /// Verifies that generated miracles actually ended up in the hand. With the form's raised max hand
    /// size (BaseLib <c>IMaxHandSizeModifier</c>) the engine's hand-full fallback should never trigger;
    /// if it ever does, the card is moved back and any remaining failure is logged loudly instead of
    /// being silently lost.
    /// </summary>
    private static async Task EnsureMiraclesInHand(IReadOnlyList<CardModel> generated)
    {
        foreach (var card in generated)
        {
            if (card.Pile?.Type == PileType.Hand) continue;

            MainFile.Logger.Warn(
                $"[Miracles] Generated miracle '{card.Id.Entry}' did not land in hand (pile: {card.Pile?.Type.ToString() ?? "none"}); retrying.");

            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Top);

            if (card.Pile?.Type != PileType.Hand)
            {
                MainFile.Logger.Error(
                    $"[Miracles] Miracle '{card.Id.Entry}' could not be placed in hand (pile: {card.Pile?.Type.ToString() ?? "none"}).");
            }
        }
    }

    /// <summary>
    /// M6 / 鍐嶅垱涓?(Genesis): asks the active form to last one more player turn. Returns false when
    /// there is no active form or the form was already extended (max once per form).
    /// </summary>
    public static bool RequestFormExtension(Player player)
    {
        if (player.Creature == null) return false;

        var power = player.Creature.GetPower<OmnisciencePower>();
        return power != null && power.RequestExtension();
    }

    /// <summary>
    /// M6: reserves extra hand space while the form is active, so cards generated by a miracle (浜旈ゼ浜岄奔
    /// copies, 鎷夋拻澶嶇敓 recovered cards) are actually placed in the hand even if the hand is full.
    /// No-op outside the form.
    /// </summary>
    public static void ReserveHandSpace(Player player, int amount)
    {
        if (amount <= 0 || player.Creature == null) return;

        player.Creature.GetPower<OmnisciencePower>()?.ReserveHandSpace(amount);
    }

    /// <summary>
    /// Removes every MiracleCard owned by this player from every combat pile: hand, draw, discard,
    /// exhaust and play. Called at the end of the owner's turn (before the hand is flushed) and again
    /// when the form is removed, so neither unplayed nor already-played miracles can survive the form
    /// or reach the permanent deck. It only touches generated miracle instances - never the run's
    /// collection (M4) and never normal cards.
    /// </summary>
    public static async Task CleanUpMiracles(Player player)
    {
        if (!CombatManager.Instance.IsInProgress) return;

        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState == null) return;

        var miracles = playerCombatState.AllCards
            .Where(card => card != null && MiracleRegistry.IsEnhancedMiracle(card))
            .ToList();

        foreach (var miracle in miracles)
        {
            if (miracle.Pile?.Type == PileType.Play)
            {
                // A card that is currently being played is finished by the play pipeline; removing it
                // mid-play would break the card play.
                MainFile.Logger.Warn(
                    $"[Miracles] Miracle '{miracle.Id.Entry}' is being played; leaving it to the play pipeline.");
                continue;
            }

            await CardPileCmd.RemoveFromCombat(miracle);
        }

        var leftover = playerCombatState.AllCards.Count(card =>
            card != null && MiracleRegistry.IsEnhancedMiracle(card) && card.Pile?.Type != PileType.Play);
        if (leftover > 0)
        {
            MainFile.Logger.Error($"[Miracles] {leftover} miracle card(s) still remain in combat after cleanup.");
        }
    }

    /// <summary>
    /// Puts the Omniscience ability card back into the player's hand when the form ends. The card is
    /// normally in the discard pile because playing a card moves it there; it is moved from wherever
    /// it is, or recreated if it is no longer part of the combat at all.
    ///
    /// This runs at the end of the enemy's turn, when the hand has already been flushed and is
    /// empty, so the card always fits.
    /// </summary>
    public static async Task RestoreActivationCard(Player player)
    {
        if (!CombatManager.Instance.IsInProgress) return;

        var playerCombatState = player.PlayerCombatState;
        var combatState = player.Creature.CombatState;
        if (playerCombatState == null || combatState == null) return;

        var existing = playerCombatState.AllCards.FirstOrDefault(card => card is OmniscienceCard);
        if (existing != null)
        {
            if (existing.Pile?.Type != PileType.Hand)
            {
                await CardPileCmd.Add(existing, PileType.Hand, CardPilePosition.Top);
            }

            return;
        }

        var restored = combatState.CreateCard<OmniscienceCard>(player);
        await CardPileCmd.AddGeneratedCardsToCombat([restored], PileType.Hand, player);
    }
}
