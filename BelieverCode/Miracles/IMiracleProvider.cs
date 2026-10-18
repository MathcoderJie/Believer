using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Miracles;

/// <summary>
/// Read-only source of the 神迹 / Miracle cards a player has unlocked in the current run.
///
/// M2 defines this interface; M5 registers <see cref="MiracleCollectionProvider"/>, which answers
/// from the M4 神迹收藏 (run Miracle collection). Additional sources can register more providers later
/// (M6) without changing the Omniscience code - the registry merges and deduplicates them by model id.
///
/// Providers are queried every time Omniscience is entered, so implementations MUST be
/// deterministic - the same player, run and combat state must produce the same cards on every
/// peer, otherwise multiplayer checksums would diverge.
/// </summary>
public interface IMiracleProvider
{
    /// <summary>
    /// Returns the canonical (non-mutable) Miracle card models unlocked for
    /// <paramref name="player"/>. Return an empty sequence when nothing is unlocked.
    /// </summary>
    IEnumerable<CardModel> GetUnlockedMiracles(Player player);
}
