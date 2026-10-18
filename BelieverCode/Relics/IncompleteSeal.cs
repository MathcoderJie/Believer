using BaseLib.Extensions;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Relics;

/// <summary>
/// 残缺圣印 / Incomplete Seal - the Believer's starting relic.
///
/// Effect: at the start of each of the owner's turns in combat, gain 1 Divinity.
///
/// Implementation notes:
/// <list type="bullet">
/// <item><c>AfterPlayerTurnStart</c> is a combat-only hook that runs once per player turn (after the
/// turn's setup: energy reset and hand draw). It cannot fire on the map, in reward screens, or on the
/// enemy's turn, and the <c>player != Owner</c> guard keeps it owner-only in multiplayer.</item>
/// <item>The gain goes through M1's <see cref="BelieverDivinityResource.Gain"/>, so the existing
/// per-combat instance, its <c>AmountChanged</c> event and the HUD counter are reused as-is; the
/// 0..7 clamp in <see cref="BelieverDivinityResource.Amount"/> caps it automatically.</item>
/// <item>Relic rarity is <c>Starter</c>: the relic is granted as a starting relic and never appears
/// in the random relic reward pool (rewards only roll Common/Uncommon/Rare).</item>
/// </list>
/// </summary>
public sealed class IncompleteSeal : BelieverRelic
{
    /// <summary>Divinity granted at the start of each turn.</summary>
    private const int DivinityPerTurn = 1;

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner) return Task.CompletedTask;

        var divinity = player.GetResource<BelieverDivinityResource>();
        if (divinity == null) return Task.CompletedTask;

        var amountBefore = divinity.Amount;
        divinity.Gain(DivinityPerTurn);

        // Only flash when Divinity actually went up: at the 7/7 cap Gain() clamps to the same value,
        // and flashing then would wrongly suggest the relic did something.
        if (divinity.Amount > amountBefore) Flash();

        return Task.CompletedTask;
    }
}
