using BaseLib.Abstracts;
using BaseLib.Extensions;
using Believer.BelieverCode.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Cards.Signature;

/// <summary>
/// Base class for the Believer's non-collectible cards (signature ability cards and 神迹 / Miracle
/// cards).
///
/// This deliberately does NOT inherit <c>BelieverCard</c>: that class carries
/// <c>[Pool(typeof(BelieverCardPool))]</c> and BaseLib declares <c>PoolAttribute</c> with
/// <c>Inherited = true</c>, so inheriting it would silently put these cards into the real,
/// collectible Believer pool. Concrete subclasses therefore declare
/// <c>[Pool(typeof(BelieverSignatureCardPool))]</c> themselves.
///
/// <see cref="Pool"/> is overridden to return the signature pool directly. The stock implementation
/// searches <c>ModelDb.AllCardPools</c>, which only contains character pools and shared pools; the
/// signature pool is neither (so its cards cannot leak into rewards, shops or random generation),
/// so the stock lookup would throw <c>"Card ... is not in any card pool!"</c> and every visual that
/// needs the pool (frame material, energy icon, banner) would fail.
///
/// Portraits reuse the mod's normal card art lookup with the same fallbacks as
/// <c>BelieverCard</c>, so a signature card can ship art under
/// <c>Believer/images/card_portraits/&lt;id&gt;.png</c> or fall back to the placeholder art.
/// </summary>
public abstract class BelieverSignatureCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : CustomCardModel(cost, type, rarity, target, showInCardLibrary: false)
{
    public override CardPoolModel Pool => ModelDb.CardPool<BelieverSignatureCardPool>();

    /// <summary>
    /// Signature cards are not deck-building material, so they cannot be upgraded (this also keeps
    /// rest-site Smithing from wasting an upgrade on the ability card). Vanilla status/curse cards do
    /// the same.
    /// </summary>
    public override int MaxUpgradeLevel => 0;

    public override string CustomPortraitPath =>
        $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".BigCardImagePath();

    public override string PortraitPath =>
        $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();

    public override string BetaPortraitPath =>
        $"beta/{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();
}
