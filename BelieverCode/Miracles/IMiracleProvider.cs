using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Miracles;

/// <summary>
/// Read-only source of the 神迹 / Miracle cards a player has unlocked in the current run.
///
/// M2 only defines this interface. The real 神迹收藏 (Miracle collection) system does not exist yet;
/// when it is implemented it will register one or more providers and decide which Miracles the
/// current run has unlocked. Until then the registry only contains
/// <see cref="DevMiracleProvider"/>, a development placeholder.
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
