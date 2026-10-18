using System.Runtime.CompilerServices;
using BaseLib.Patches.Saves;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;

namespace Believer.BelieverCode.Miracles;

/// <summary>
/// Owns the per-run <see cref="MiracleCollection"/> instances and persists them with the run save.
///
/// Storage: one collection per <see cref="IRunState"/>, keyed by a <see cref="ConditionalWeakTable{TKey,TValue}"/>.
/// A new run creates a new <c>RunState</c>, so a new run always starts with an empty collection and
/// can never inherit the previous run's miracles; entries of finished runs are collected with the
/// run state itself.
///
/// Persistence: BaseLib's <c>ExtendedSaveTypes.RegisterSavedValue&lt;IRunState, T&gt;</c> attaches the
/// data to the game's own <c>SerializableRun</c>. That covers all the run save paths the game has
/// (writing a save, loading/continuing a run, and sending the run to other players when joining a
/// multiplayer game) and means M4 stores its data inside the game's run save instead of inventing a
/// second save file.
///
/// Interface for later milestones: <see cref="GetOwnedIds"/> and <see cref="GetOwnedMiracleCards"/>
/// expose the run's collected miracles, which is what the Omniscience module will use to grant them
/// during the form (M5). M4 itself never creates or moves cards.
/// </summary>
public static class MiracleCollectionStore
{
    /// <summary>
    /// Save/network id of the collection. Includes the mod id so it cannot collide with another mod's
    /// extended run data.
    /// </summary>
    public const string SaveKey = "Believer-miracle-collection";

    /// <summary>Hard cap used when reading serialized ids, so corrupt data cannot allocate wildly.</summary>
    private const int MaxSerializedIds = 64;

    private static readonly ConditionalWeakTable<IRunState, MiracleCollection> Collections = new();

    private static bool _saveRegistrationAttempted;

    /// <summary>
    /// The collection of a run, or null when the run state is missing / a placeholder. Never creates
    /// an entry for placeholder run states such as <c>NullRunState</c>.
    /// </summary>
    public static MiracleCollection? Find(IRunState? runState)
    {
        if (runState == null || runState is NullRunState) return null;

        return Collections.TryGetValue(runState, out var collection) ? collection : null;
    }

    /// <summary>The collection of a run, creating the (empty) collection on first use.</summary>
    public static MiracleCollection GetOrCreate(IRunState runState)
    {
        ArgumentNullException.ThrowIfNull(runState);
        if (runState is NullRunState)
        {
            throw new InvalidOperationException("Cannot create a Miracle collection for NullRunState.");
        }

        return Collections.GetValue(runState, _ => new MiracleCollection());
    }

    /// <summary>The current run's collection, or null outside of a run.</summary>
    public static MiracleCollection? Find(Player? player) => player == null ? null : Find(player.RunState);

    /// <summary>The current run's collection, creating it on first use.</summary>
    public static MiracleCollection GetOrCreate(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return GetOrCreate(player.RunState);
    }

    /// <summary>Adds a miracle to the player's run collection.</summary>
    public static MiracleAddResult TryAdd(Player? player, MiracleId id)
    {
        var collection = Find(player);
        return collection == null ? MiracleAddResult.NoActiveRun : collection.TryAdd(id);
    }

    /// <summary>True when the player's run has collected the given miracle.</summary>
    public static bool Has(Player? player, MiracleId id) => Find(player)?.Has(id) ?? false;

    /// <summary>Clears the player's run collection.</summary>
    public static bool Clear(Player? player) => Find(player)?.Clear() ?? false;

    /// <summary>Collected miracle ids of a run, for the Omniscience integration in M5.</summary>
    public static IReadOnlyList<MiracleId> GetOwnedIds(IRunState? runState)
        => Find(runState)?.OwnedIds ?? Array.Empty<MiracleId>();

    /// <summary>
    /// The collected miracles as canonical card models, for the Omniscience integration in M5.
    /// Ids that no longer resolve (card removed/renamed by a mod update) are skipped with a warning.
    /// </summary>
    public static IReadOnlyList<MiracleCard> GetOwnedMiracleCards(IRunState? runState)
    {
        var collection = Find(runState);
        if (collection == null || collection.Count == 0) return Array.Empty<MiracleCard>();

        var cards = new List<MiracleCard>(collection.Count);
        foreach (var id in collection.OwnedIds)
        {
            if (MiracleCatalog.TryResolve(id, out var card))
            {
                cards.Add(card);
            }
            else
            {
                MainFile.Logger.Warn($"[Miracles] Collected miracle id '{id}' no longer resolves to a card; skipping it.");
            }
        }

        return cards;
    }

    /// <summary>Convenience overload of <see cref="GetOwnedMiracleCards(IRunState?)"/> for a player.</summary>
    public static IReadOnlyList<MiracleCard> GetOwnedMiracleCards(Player? player)
        => player == null ? Array.Empty<MiracleCard>() : GetOwnedMiracleCards(player.RunState);

    /// <summary>
    /// Registers the collection with BaseLib's extended run save. Must run during mod initialization
    /// (before the game first builds its serializer metadata) and must only run once.
    /// </summary>
    public static void RegisterSave()
    {
        if (_saveRegistrationAttempted) return;
        _saveRegistrationAttempted = true;

        try
        {
            // Diagnostic: the game's serializer already uses List<string> in its own save models, so
            // this should be true even before we register anything. Logged so a failed/removed game
            // API shows up in the log instead of silently breaking run saves.
            var listAlreadySupported = ExtendedSaveTypes.IsSaveTypeSupported(typeof(List<string>));
            MainFile.Logger.Info(
                $"[Miracles] List<string> already supported by the run serializer: {listAlreadySupported}");

            // The saved payload is List<string>; make sure the game's serializer has type metadata
            // for it before registering the dictionary property that holds it.
            ExtendedSaveTypes.RegisterListSaveType<string>();

            var registered = ExtendedSaveTypes.RegisterSavedValue<IRunState, List<string>>(
                SaveKey,
                GetSavedIds,
                ApplySavedIds,
                WriteSavedIds,
                ReadSavedIds);

            if (registered)
            {
                MainFile.Logger.Info($"[Miracles] Registered run-save data '{SaveKey}'.");
            }
            else
            {
                MainFile.Logger.Error($"[Miracles] Failed to register run-save data '{SaveKey}'; the collection will not persist.");
            }
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"[Miracles] Exception while registering run-save data '{SaveKey}': {exception}");
        }
    }

    /// <summary>Serialization getter: null when empty so empty collections are not written at all.</summary>
    private static List<string>? GetSavedIds(IRunState runState)
    {
        var collection = Find(runState);
        return collection == null || collection.Count == 0 ? null : collection.ToSerializedIds();
    }

    /// <summary>Deserialization setter: replaces the collection contents for the loaded run state.</summary>
    private static void ApplySavedIds(IRunState runState, List<string>? ids)
    {
        GetOrCreate(runState).Restore(ids);
    }

    private static void WriteSavedIds(List<string> ids, PacketWriter writer)
    {
        writer.WriteInt(ids.Count);
        foreach (var id in ids) writer.WriteString(id);
    }

    private static List<string> ReadSavedIds(PacketReader reader)
    {
        var count = reader.ReadInt();
        if (count < 0) count = 0;
        if (count > MaxSerializedIds) count = MaxSerializedIds;

        var ids = new List<string>(count);
        for (var i = 0; i < count; i++) ids.Add(reader.ReadString());
        return ids;
    }
}
