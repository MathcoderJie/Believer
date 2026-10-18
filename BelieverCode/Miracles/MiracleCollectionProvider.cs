using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Miracles;

/// <summary>
/// M5 bridge between the M4 神迹收藏 (run Miracle collection) and M2's <see cref="IMiracleProvider"/>
/// seam.
///
/// Omniscience asks <see cref="MiracleRegistry"/> which miracles to grant; this provider answers with
/// exactly the miracles the player's run has collected (<see cref="MiracleCollectionStore"/>), so
/// uncollected miracles are never generated and a stale/unimplemented id is skipped safely by the
/// collection layer. It replaces the M2 development placeholder provider, which granted its test
/// miracle regardless of the collection.
///
/// M6 formal miracles: implementing a new <see cref="MiracleCard"/> subclass is enough for it to be
/// grantable once collected (the collection's catalog discovers MiracleCard types automatically);
/// acquisition code only needs to call <see cref="MiracleCollectionStore.TryAdd"/>. Additional unlock
/// sources can also register their own <see cref="IMiracleProvider"/> without touching this class.
/// </summary>
public sealed class MiracleCollectionProvider : IMiracleProvider
{
    public IEnumerable<CardModel> GetUnlockedMiracles(Player player)
    {
        foreach (var miracle in MiracleCollectionStore.GetOwnedMiracleCards(player))
        {
            yield return miracle;
        }
    }
}
