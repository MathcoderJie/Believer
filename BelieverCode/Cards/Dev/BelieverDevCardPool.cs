using Believer.BelieverCode.Character;

namespace Believer.BelieverCode.Cards.Dev;

/// <summary>
/// DEVELOPMENT-ONLY card pool. It exists for one reason: the game requires every
/// <c>CardModel</c> to belong to a card pool, so <see cref="DivinityTestCard"/> needs a pool in
/// order to be spawnable from the developer console - but it must not be reachable in normal play.
///
/// This pool contains only <see cref="DivinityTestCard"/> and cannot leak into gameplay:
///
/// <list type="bullet">
/// <item>Character card pools are discovered through <c>CharacterModel.CardPool</c>, which returns
/// the Believer's real pool (<c>BelieverCardPool</c>). This type is not returned by any character,
/// so it is not a character pool.</item>
/// <item>Shared pools are registered only when <c>IsShared</c> is true (BaseLib's
/// <c>ModelDbSharedCardPoolsPatch</c>). <c>IsShared</c> stays <c>false</c>, inherited from
/// <c>CustomCardPoolModel</c>, so it is not a shared pool either.</item>
/// <item>BaseLib's own note on <c>CustomCardPoolModel</c> states that a pool which is neither a
/// character pool nor a shared pool "will not be found", i.e. it never reaches
/// <c>ModelDb.AllCardPools</c> - the collection card rewards and shops roll from.</item>
/// </list>
///
/// Consequently: not in the starting deck, not in rewards, not in shops, not in random card
/// generation, and not in the card library. It is only referenced by
/// <see cref="DivinityTestCard"/>'s <c>PoolAttribute</c>.
///
/// Visual configuration is inherited from <c>BelieverCardPool</c> so the dev card renders exactly
/// like a Believer card (valid energy icons that already ship in this mod's .pck); only the pool
/// title is overridden to keep it distinct from the real pool.
/// </summary>
public class BelieverDevCardPool : BelieverCardPool
{
    public override string Title => "BelieverDev";
}
