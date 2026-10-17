using BaseLib.Extensions;
using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Miracles;
using Believer.BelieverCode.Powers;
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
/// The 全知全能 / Omniscience activation sequence and its combat-side helpers.
///
/// Everything here runs inside a native card play (<see cref="OmniscienceCard.OnPlay"/>), so every
/// peer executes the same sequence deterministically - including the Divinity spend, which is local
/// per-combat state in M1 and would desync if it were mutated outside of a synchronised action.
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
    /// <item>Draw 3 cards.</item>
    /// <item>Grant every unlocked Miracle from the run's collection into the hand.</item>
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

        await PowerCmd.Apply<OmnisciencePower>(
            choiceContext, player.Creature, 1m, player.Creature, source);

        await RemoveDebuffs(player);
        await CardPileCmd.Draw(choiceContext, CardsDrawnOnEnter, player);
        await GrantMiracles(player);
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
    /// Grants every Miracle the run has unlocked. The registry returns canonical models; each is
    /// created as a mutable combat instance and added to the hand through
    /// <c>CardPileCmd.AddGeneratedCardsToCombat</c>, which is also what records them in combat
    /// history as player-generated cards.
    /// </summary>
    private static async Task GrantMiracles(Player player)
    {
        var unlocked = MiracleRegistry.GetUnlockedMiracles(player);
        if (unlocked.Count == 0) return;

        var combatState = player.Creature.CombatState;
        if (combatState == null) return;

        var cards = new List<CardModel>(unlocked.Count);
        foreach (var canonical in unlocked)
        {
            cards.Add(combatState.CreateCard(canonical, player));
        }

        await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, player);
    }

    /// <summary>
    /// Removes every Miracle card still in a combat pile for this player. Called from
    /// <see cref="OmnisciencePower.BeforeSideTurnEnd"/> (before the hand is flushed) and therefore
    /// also catches Miracles that were pushed into the discard pile because the hand was full when
    /// they were granted.
    /// </summary>
    public static async Task CleanUpUnusedMiracles(Player player)
    {
        if (!CombatManager.Instance.IsInProgress) return;

        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState == null) return;

        var miracles = playerCombatState.AllCards
            .Where(card => card != null && MiracleRegistry.IsMiracle(card))
            .ToList();

        foreach (var miracle in miracles)
        {
            await CardPileCmd.RemoveFromCombat(miracle);
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
