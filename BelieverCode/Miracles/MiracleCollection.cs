using System.Collections.ObjectModel;

namespace Believer.BelieverCode.Miracles;

/// <summary>Result of trying to add a miracle to a collection.</summary>
public enum MiracleAddResult
{
    /// <summary>The miracle was added.</summary>
    Added,

    /// <summary>The collection already contains this miracle (no duplicates are allowed).</summary>
    AlreadyOwned,

    /// <summary>No <see cref="MiracleCard"/> with that id exists in this build.</summary>
    UnknownMiracle,

    /// <summary>The supplied id was empty or malformed.</summary>
    InvalidId,

    /// <summary>There is no active run to own the miracle.</summary>
    NoActiveRun,
}

/// <summary>
/// The 神迹收藏 / Miracle collection of one run.
///
/// <list type="bullet">
/// <item>Only stores stable <see cref="MiracleId"/>s; it never creates cards and never touches the
/// deck, draw pile, discard pile or any other pile, so collected miracles stay separate from the
/// main deck (M4 requirement).</item>
/// <item>Duplicates are rejected, unknown ids are rejected (see <see cref="MiracleCatalog"/>), and
/// enumeration order is deterministic (ordinal by id) so saves are stable.</item>
/// <item>One instance exists per <c>IRunState</c>, owned by <see cref="MiracleCollectionStore"/>;
/// a brand new run therefore starts with an empty collection.</item>
/// </list>
/// </summary>
public sealed class MiracleCollection
{
    private readonly List<MiracleId> _owned = [];
    private readonly HashSet<MiracleId> _lookup = [];
    private readonly ReadOnlyCollection<MiracleId> _ownedView;

    public MiracleCollection()
    {
        _ownedView = _owned.AsReadOnly();
    }

    /// <summary>Raised whenever the collection changes (add/clear/restore).</summary>
    public event Action<MiracleCollection>? Changed;

    /// <summary>Number of collected miracles.</summary>
    public int Count => _owned.Count;

    /// <summary>Collected miracle ids, in deterministic order.</summary>
    public IReadOnlyList<MiracleId> OwnedIds => _ownedView;

    /// <summary>True when this run has collected the miracle with the given id.</summary>
    public bool Has(MiracleId id) => !id.IsEmpty && _lookup.Contains(id);

    /// <summary>
    /// Adds a miracle. Ids must be well-formed and must exist in <see cref="MiracleCatalog"/>;
    /// duplicates and unknown ids are rejected without throwing (safe handling).
    /// </summary>
    public MiracleAddResult TryAdd(MiracleId id)
    {
        if (id.IsEmpty) return MiracleAddResult.InvalidId;
        if (!MiracleCatalog.IsKnown(id)) return MiracleAddResult.UnknownMiracle;
        if (!_lookup.Add(id)) return MiracleAddResult.AlreadyOwned;

        _owned.Add(id);
        _owned.Sort();
        Changed?.Invoke(this);
        return MiracleAddResult.Added;
    }

    /// <summary>Removes every collected miracle. Returns true when something was removed.</summary>
    public bool Clear()
    {
        if (_owned.Count == 0) return false;

        _owned.Clear();
        _lookup.Clear();
        Changed?.Invoke(this);
        return true;
    }

    /// <summary>
    /// Replaces the contents with ids restored from a save or received over the network. Malformed
    /// or unknown ids are skipped with a warning instead of failing the whole load.
    /// </summary>
    internal void Restore(IEnumerable<string>? serializedIds)
    {
        _owned.Clear();
        _lookup.Clear();

        if (serializedIds != null)
        {
            foreach (var raw in serializedIds)
            {
                if (!MiracleId.TryCreate(raw, out var id))
                {
                    MainFile.Logger.Warn($"[Miracles] Ignoring malformed saved miracle id '{raw}'.");
                    continue;
                }

                if (!MiracleCatalog.IsKnown(id))
                {
                    MainFile.Logger.Warn($"[Miracles] Ignoring saved miracle id '{id}'; no such MiracleCard exists.");
                    continue;
                }

                if (!_lookup.Add(id)) continue;
                _owned.Add(id);
            }
        }

        _owned.Sort();
        Changed?.Invoke(this);
    }

    /// <summary>Serializable snapshot: deterministic, ordinal-sorted list of id strings.</summary>
    internal List<string> ToSerializedIds()
    {
        var ids = new List<string>(_owned.Count);
        foreach (var id in _owned) ids.Add(id.Value);
        return ids;
    }
}
