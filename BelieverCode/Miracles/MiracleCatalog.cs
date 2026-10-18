using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Miracles;

/// <summary>
/// The set of miracle ids that actually exist in this mod build.
///
/// It is built from every concrete <see cref="MiracleCard"/> subclass in the model database
/// (<c>ModelDb.AllAbstractModelSubtypes</c>), so the future 12 miracle cards automatically become
/// collectible the moment they exist - no list in this file needs updating. The current build only
/// contains the M2 development test miracle.
///
/// This is the "does the id exist?" half of the safe-handling requirement: adding an unknown id to a
/// collection is rejected instead of corrupting the save, and loading a save that mentions a miracle
/// from a removed/renamed card skips that id with a warning.
/// </summary>
public static class MiracleCatalog
{
    private static Dictionary<string, MiracleCard>? _cardsByEntry;
    private static IReadOnlyList<MiracleId>? _knownIds;

    /// <summary>All miracle ids that currently exist, sorted ordinal for deterministic output.</summary>
    public static IReadOnlyList<MiracleId> KnownIds => _knownIds ??= BuildKnownIds();

    /// <summary>True when a miracle with this id exists in this build.</summary>
    public static bool IsKnown(MiracleId id) => !id.IsEmpty && CardsByEntry.ContainsKey(id.Value);

    /// <summary>Resolves a miracle id to its canonical card model.</summary>
    public static bool TryResolve(MiracleId id, [NotNullWhen(true)] out MiracleCard? card)
    {
        card = null;
        return !id.IsEmpty && CardsByEntry.TryGetValue(id.Value, out card);
    }

    private static Dictionary<string, MiracleCard> CardsByEntry =>
        _cardsByEntry ??= BuildCardsByEntry();

    private static Dictionary<string, MiracleCard> BuildCardsByEntry()
    {
        var cardsByEntry = new Dictionary<string, MiracleCard>(StringComparer.Ordinal);

        foreach (var type in ModelDb.AllAbstractModelSubtypes)
        {
            if (type.IsAbstract || !typeof(MiracleCard).IsAssignableFrom(type)) continue;

            try
            {
                var model = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(type));
                if (model is MiracleCard miracleCard)
                {
                    cardsByEntry[miracleCard.Id.Entry] = miracleCard;
                }
            }
            catch (Exception exception)
            {
                MainFile.Logger.Error($"Could not resolve MiracleCard type '{type.FullName}': {exception}");
            }
        }

        MainFile.Logger.Info(
            $"[Miracles] Catalog contains {cardsByEntry.Count} miracle id(s): {string.Join(", ", cardsByEntry.Keys.OrderBy(key => key, StringComparer.Ordinal))}");
        return cardsByEntry;
    }

    private static IReadOnlyList<MiracleId> BuildKnownIds()
    {
        var ids = new List<MiracleId>(CardsByEntry.Count);
        foreach (var entry in CardsByEntry.Keys.OrderBy(entry => entry, StringComparer.Ordinal))
        {
            if (MiracleId.TryCreate(entry, out var id)) ids.Add(id);
        }

        return ids;
    }
}
