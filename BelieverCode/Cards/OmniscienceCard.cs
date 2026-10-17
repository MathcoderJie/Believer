using BaseLib.Extensions;
using BaseLib.Utils;
using Believer.BelieverCode.Cards.Signature;
using Believer.BelieverCode.Mechanics;
using Believer.BelieverCode.Powers;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Cards;

/// <summary>
/// 全知全能 / Omniscience - the Believer's active ability, implemented as a real card.
///
/// Why a card instead of a custom HUD button: playing a card goes through the game's native
/// <c>PlayCardAction</c> pipeline, which already handles targeting, animation, the player choice
/// context, and - critically - multiplayer determinism and checksums. A custom clickable HUD control
/// would need a hand-rolled <c>GameAction</c> + <c>INetAction</c> pair just to be multiplayer-safe,
/// so the card is the most compatible activation entry the game offers. The card is code-built from
/// <see cref="BelieverSignatureCard"/> so it is not collectible.
///
/// Wiring:
/// <list type="bullet">
/// <item><see cref="CardKeyword.Innate"/> puts it in the opening hand of every combat.</item>
/// <item><see cref="CardKeyword.Retain"/> keeps it there instead of discarding it at end of turn.</item>
/// <item><see cref="CardKeyword.Eternal"/> is the engine's 永恒 keyword: while in the deck the card
/// cannot be removed or transformed (<c>CardModel.IsRemovable</c>/<c>IsTransformable</c> are false).</item>
/// <item>"Cannot be discarded" in general (discard effects and discard selections) is enforced by
/// <see cref="Patches.OmniscienceCardPatches"/>.</item>
/// <item><see cref="IsPlayable"/> hides it behind a full 7/7 Divinity bar, and
/// <see cref="Mechanics.Omniscience.Activate"/> spends the 7 Divinity.</item>
/// </list>
/// </summary>
[Pool(typeof(BelieverSignatureCardPool))]
public sealed class OmniscienceCard() : BelieverSignatureCard(0, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Innate, CardKeyword.Retain, CardKeyword.Eternal];

    /// <summary>
    /// Playable only when the owner has a full 7/7 Divinity bar and is not already in the form.
    /// <c>CanPlay</c> turns a false result into <c>UnplayableReason.BlockedByCardLogic</c>, which is
    /// what greys the card out in hand.
    /// </summary>
    protected override bool IsPlayable
    {
        get
        {
            // Canonical instances are only used by previews/library screens and have no owner.
            if (!IsMutable) return false;

            var owner = Owner;
            if (owner == null || owner.Creature == null) return false;
            if (owner.Creature.HasPower<OmnisciencePower>()) return false;

            return owner.GetResource<BelieverDivinityResource>()?.IsFull == true;
        }
    }

    /// <summary>
    /// Gold-glows the card in hand once it is actually usable, so "Divinity is full, you may activate
    /// now" is visible without opening the tooltip.
    /// </summary>
    protected override bool ShouldGlowGoldInternal => IsPlayable;

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => Omniscience.Activate(choiceContext, Owner, this);

    /// <summary>
    /// The ability card is never consumed: after every play it goes straight back to the hand (this
    /// also covers forced/auto-plays, and an activation that could not be paid for can therefore
    /// never lose the card). If the hand happens to be full it lands in the discard pile instead;
    /// <see cref="Omniscience.RestoreActivationCard"/> recovers it when the form ends.
    /// </summary>
    protected override PileType GetResultPileTypeForCardPlay() => PileType.Hand;
}
