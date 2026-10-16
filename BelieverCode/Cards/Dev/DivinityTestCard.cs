using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Cards.Dev;

/// <summary>
/// DEVELOPMENT-ONLY test card for the Divinity resource. It exists purely so the resource can be
/// driven from the developer console; it must never appear in normal play.
///
/// It is registered into <see cref="BelieverDevCardPool"/>, a development-only pool that is neither
/// a character pool nor a shared pool, and therefore never reaches <c>ModelDb.AllCardPools</c>.
/// That gives the card a real pool - which the game requires, and which the developer console needs
/// in order to spawn it - while keeping it out of the starting deck, card rewards, shops and random
/// card generation.
///
/// The card is additionally hidden from the card library via <c>showInCardLibrary: false</c>.
///
/// Note: this deliberately does NOT inherit the mod's <c>BelieverCard</c> base class. That class
/// carries <c>[Pool(typeof(BelieverCardPool))]</c> - the real Believer pool - and BaseLib declares
/// <c>PoolAttribute</c> with <c>Inherited = true</c>, so inheriting it would put this test card into
/// the official pool.
/// </summary>
[Pool(typeof(BelieverDevCardPool))]
public sealed class DivinityTestCard : CustomCardModel
{
    public DivinityTestCard()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self, showInCardLibrary: false)
    {
    }

    /// <summary>Gain 1 Divinity.</summary>
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Owner?.GetResource<BelieverDivinityResource>()?.Gain(1);
        return Task.CompletedTask;
    }
}
