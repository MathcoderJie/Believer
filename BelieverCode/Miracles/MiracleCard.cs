using Believer.BelieverCode.Cards;
using Believer.BelieverCode.Keywords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Believer.BelieverCode.Miracles;

/// <summary>
/// Base class for the 12 神迹 / Miracle cards.
///
/// Normal rule (M10.4): a Miracle is a regular collectible Rare card in
/// <see cref="Character.BelieverCardPool"/> by inheriting <see cref="BelieverCard"/>. It can be
/// obtained from ordinary rare-card rewards, can be played outside Omniscience, and is no longer
/// auto-Exhausted by this base class. Its only automatic keyword is the stable
/// <see cref="BelieverKeywords.Miracle"/> marker; individual cards may add their own keywords.
///
/// Omniscience does not grant these canonical cards directly: it creates temporary enhanced copies
/// (see EnhancedMiracleCard) so the base card's effect and deck state are never overwritten.
/// </summary>
public abstract class MiracleCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : BelieverCard(cost, type, rarity, target)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [BelieverKeywords.Miracle];

    /// <summary>
    /// The 12 base Miracles have no upgrade design yet; they remain normal playable Rare cards but
    /// do not offer a misleading no-op smith upgrade.
    /// </summary>
    public override int MaxUpgradeLevel => 0;

    /// <summary>
    /// Allows the enhanced-card wrapper to invoke this card's exact base effect without copying the
    /// effect implementation. The wrapper owns the temporary combat instance; this method runs on a
    /// normal combat card model with the same owner.
    /// </summary>
    internal Task ExecuteBaseEffect(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => OnPlay(choiceContext, cardPlay);
}
