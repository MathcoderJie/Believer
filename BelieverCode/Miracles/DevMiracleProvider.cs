using Believer.BelieverCode.Cards.Dev;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Miracles;

/// <summary>
/// DEVELOPMENT-ONLY placeholder provider. It stands in for the future 神迹收藏 (Miracle collection)
/// system so the Omniscience "gain all unlocked Miracles" path can be verified before that system is
/// built.
///
/// It reports exactly one card - <see cref="DevMiracleTestCard"/> - for the Believer. It must be
/// replaced by a real provider when the collection system exists; until then, production runs will
/// grant this single test Miracle while Omniscience is active.
/// </summary>
public sealed class DevMiracleProvider : IMiracleProvider
{
    public IEnumerable<CardModel> GetUnlockedMiracles(Player player)
    {
        if (player.Character is not global::Believer.BelieverCode.Character.Believer) yield break;

        yield return ModelDb.Card<DevMiracleTestCard>();
    }
}
