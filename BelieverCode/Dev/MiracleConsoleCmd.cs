using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// DEVELOPMENT-ONLY console command for the Miracle collection.
///
/// The collection has no acquisition flow yet (that is M11), so this command is how the M4/M5/M6
/// pipeline is exercised in game: add/list/clear the current run's collection, then enter Omniscience
/// to have the collected miracles generated into the hand.
///
/// The id argument is forgiving: the full id (<c>BELIEVER-LET_THERE_BE_LIGHT</c>), the entry alone
/// (<c>LET_THERE_BE_LIGHT</c>), the class name (<c>LetThereBeLight</c>) and the display name
/// (<c>"Let There Be Light"</c>) all resolve to the same miracle.
///
/// It is a debug command (<c>DebugOnly</c> defaults to true, and modded runs allow debug commands) and
/// it uses the game's own networked console command path, so in multiplayer the change is applied
/// through the action queue on every peer.
/// </summary>
public sealed class MiracleConsoleCmd : AbstractConsoleCmd
{
    private const string ModPrefix = "BELIEVER-";

    /// <summary>
    /// Logged when the game's dev console instantiates every discovered command at startup. It makes
    /// it obvious in the log whether the test command was registered for this build.
    /// </summary>
    static MiracleConsoleCmd()
    {
        MainFile.Logger.Info("[Miracles] Dev console command 'miracle' registered.");
    }

    public override string CmdName => "miracle";

    public override string Args => "<list|known|has|add|addall|clear> [id]";

    public override string Description =>
        "[Dev] Inspect or edit the current run's Believer Miracle collection (M4/M6).";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer == null) return new CmdResult(false, "This command only works during a run.");
        if (args.Length == 0) return Usage();

        var collection = MiracleCollectionStore.Find(issuingPlayer);

        switch (args[0].ToLowerInvariant())
        {
            case "list":
            {
                if (collection == null || collection.Count == 0)
                {
                    return new CmdResult(true, "Miracle collection is empty.");
                }

                var ids = string.Join(", ", collection.OwnedIds.Select(id => id.Value));
                return new CmdResult(true, $"Owned miracles ({collection.Count}): {ids}");
            }

            case "known":
            {
                var known = MiracleCatalog.KnownIds;
                if (known.Count == 0) return new CmdResult(true, "No MiracleCard ids exist in this build.");

                return new CmdResult(true,
                    $"Known miracle ids ({known.Count}):\n{string.Join("\n", known.Select(id => "  " + id.Value))}");
            }

            case "has":
            {
                if (!TryResolveId(args, out var id)) return UnknownId(args);

                var owned = collection?.Has(id) ?? false;
                return new CmdResult(true, owned ? $"Owned: {id}" : $"Not owned: {id}");
            }

            case "add":
            {
                if (!TryResolveId(args, out var id)) return UnknownId(args);

                var result = MiracleCollectionStore.TryAdd(issuingPlayer, id);
                return result switch
                {
                    MiracleAddResult.Added => new CmdResult(true, $"Added miracle '{id}'."),
                    MiracleAddResult.AlreadyOwned => new CmdResult(false, $"Already owned: {id}"),
                    MiracleAddResult.UnknownMiracle => new CmdResult(false,
                        $"No MiracleCard with id '{id}'. Use 'miracle known' to list valid ids."),
                    MiracleAddResult.InvalidId => new CmdResult(false, "Invalid miracle id."),
                    MiracleAddResult.NoActiveRun => new CmdResult(false, "There is no active run."),
                    _ => new CmdResult(false, "Unexpected result."),
                };
            }

            case "addall":
            {
                if (MiracleCollectionStore.Find(issuingPlayer) == null)
                {
                    return new CmdResult(false, "There is no active run.");
                }

                var added = 0;
                var alreadyOwned = 0;
                foreach (var knownId in MiracleCatalog.KnownIds)
                {
                    switch (MiracleCollectionStore.TryAdd(issuingPlayer, knownId))
                    {
                        case MiracleAddResult.Added:
                            added++;
                            break;
                        case MiracleAddResult.AlreadyOwned:
                            alreadyOwned++;
                            break;
                    }
                }

                return new CmdResult(true,
                    $"Added {added} miracle(s) to this run's collection ({alreadyOwned} already owned, {MiracleCatalog.KnownIds.Count} known in total).");
            }

            case "clear":
            {
                var cleared = MiracleCollectionStore.Clear(issuingPlayer);
                return new CmdResult(true, cleared ? "Miracle collection cleared." : "Miracle collection was already empty.");
            }

            default:
                return Usage();
        }
    }

    private static CmdResult UnknownId(string[] args)
    {
        var typed = args.Length >= 2 ? string.Join(" ", args.Skip(1)) : "<id>";
        return new CmdResult(false,
            $"No MiracleCard matches '{typed}'.\n" +
            "Usage: miracle <list|known|has|add|addall|clear> [id]\n" +
            "Tip: run 'miracle known' to see the exact ids; the short entry (e.g. LET_THERE_BE_LIGHT) works too.");
    }

    /// <summary>
    /// Resolves the id argument against the ids that actually exist in this build. Accepts the full id,
    /// the entry without the mod prefix, the class name, and the display name (spaces become '_').
    /// </summary>
    private static bool TryResolveId(string[] args, out MiracleId id)
    {
        id = default;
        if (args.Length < 2) return false;

        var typed = string.Join(" ", args.Skip(1)).Trim();
        if (typed.Length == 0) return false;

        var normalized = Normalize(typed);
        foreach (var known in MiracleCatalog.KnownIds)
        {
            var candidate = Normalize(known.Value);
            var candidateWithoutPrefix = Normalize(known.Value.Replace(ModPrefix, string.Empty));
            var typedWithoutPrefix = Normalize(typed.Replace(ModPrefix, string.Empty));

            if (candidate.Equals(normalized, StringComparison.Ordinal) ||
                candidateWithoutPrefix.Equals(normalized, StringComparison.Ordinal) ||
                candidate.Equals(typedWithoutPrefix, StringComparison.Ordinal) ||
                candidateWithoutPrefix.Equals(typedWithoutPrefix, StringComparison.Ordinal))
            {
                id = known;
                return true;
            }
        }

        return false;
    }

    /// <summary>Case/separator-insensitive form used for matching: "Let There Be Light" == "LET_THERE_BE_LIGHT".</summary>
    private static string Normalize(string value)
        => value.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();

    private static CmdResult Usage() => new(false,
        "Usage: miracle <list|known|has|add|addall|clear> [id]\n" +
        "  list   - list the current run's collected miracle ids\n" +
        "  known  - list every miracle id that exists in this build\n" +
        "  has    - check whether an id is collected\n" +
        "  add    - add an id to the current run's collection (full id or short entry)\n" +
        "  addall - add every existing miracle id to the current run's collection\n" +
        "  clear  - clear the current run's collection\n" +
        "Only works during a run/combat. Collected miracles are generated when you enter Omniscience.");
}
