using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Miracles;

/// <summary>
/// Registry of <see cref="IMiracleProvider"/>s and the single place the Omniscience mechanic reads
/// the run's unlocked Miracles from.
///
/// This is the seam the future 神迹收藏 (Miracle collection) system plugs into: that system calls
/// <see cref="Register"/> with a provider that reports the current run's unlocked Miracles. M2 ships
/// no such system - only the interface, this registry, the abstract <see cref="MiracleCard"/> base,
/// and one development-only provider.
/// </summary>
public static class MiracleRegistry
{
    private static readonly List<IMiracleProvider> Providers = [];

    /// <summary>Registers a provider. Registering the same instance twice is a no-op.</summary>
    public static void Register(IMiracleProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (!Providers.Contains(provider)) Providers.Add(provider);
    }

    /// <summary>
    /// Returns the distinct canonical Miracle card models unlocked for <paramref name="player"/>.
    /// Order follows provider registration order, then provider order, and duplicates are removed by
    /// model id so the result is stable across calls.
    /// </summary>
    public static IReadOnlyList<CardModel> GetUnlockedMiracles(Player player)
    {
        var unlocked = new List<CardModel>();
        var seen = new HashSet<ModelId>();

        // Snapshot: a provider could theoretically register another provider while being queried.
        foreach (var provider in Providers.ToArray())
        {
            IEnumerable<CardModel>? cards;
            try
            {
                cards = provider.GetUnlockedMiracles(player);
            }
            catch (Exception exception)
            {
                MainFile.Logger.Error(
                    $"Miracle provider '{provider.GetType().Name}' threw while reporting unlocked Miracles: {exception}");
                continue;
            }

            if (cards == null) continue;

            foreach (var card in cards)
            {
                if (card == null) continue;
                if (card is not MiracleCard)
                {
                    MainFile.Logger.Warn(
                        $"Miracle provider '{provider.GetType().Name}' returned '{card.Id}', which is not a MiracleCard; ignoring it.");
                    continue;
                }

                if (seen.Add(card.Id)) unlocked.Add(card);
            }
        }

        return unlocked;
    }

    /// <summary>True when this card instance is a temporary 神迹 / Miracle.</summary>
    public static bool IsMiracle(CardModel card) => card is MiracleCard;
}
