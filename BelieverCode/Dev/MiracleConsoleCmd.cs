using Believer.BelieverCode.Miracles;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// DEVELOPMENT-ONLY console command for the M4 Miracle collection.
///
/// M4 deliberately has no acquisition flow (no reward UI, no temporary miracle cards), so this command
/// is the way to exercise the collection in game: add/has/list/clear the current run's collection,
/// then save and reload the run to verify persistence.
///
/// It is a debug command (<c>DebugOnly</c> defaults to true, and modded runs allow debug commands) and
/// it uses the game's own networked console command path, so in a multiplayer game the change is
/// applied through the action queue on every peer.
/// </summary>
public sealed class MiracleConsoleCmd : AbstractConsoleCmd
{
    /// <summary>
    /// Logged when the game's dev console instantiates every discovered command at startup. It makes
    /// it obvious in the log whether the M4 test command was registered for this build.
    /// </summary>
    static MiracleConsoleCmd()
    {
        MainFile.Logger.Info("[Miracles] Dev console command 'miracle' registered.");
    }

    public override string CmdName => "miracle";

    public override string Args => "<list|known|has|add|clear> [id:string]";

    public override string Description =>
        "[Dev] Inspect or edit the current run's Believer Miracle collection (M4).";

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

                return new CmdResult(true, $"Known miracle ids ({known.Count}): {string.Join(", ", known.Select(id => id.Value))}");
            }

            case "has":
            {
                if (!TryParseId(args, out var id)) return new CmdResult(false, "Usage: miracle has <id>");

                var owned = collection?.Has(id) ?? false;
                return new CmdResult(true, owned ? $"Owned: {id}" : $"Not owned: {id}");
            }

            case "add":
            {
                if (!TryParseId(args, out var id)) return new CmdResult(false, "Usage: miracle add <id>");

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

            case "clear":
            {
                var cleared = MiracleCollectionStore.Clear(issuingPlayer);
                return new CmdResult(true, cleared ? "Miracle collection cleared." : "Miracle collection was already empty.");
            }

            default:
                return Usage();
        }
    }

    private static bool TryParseId(string[] args, out MiracleId id)
    {
        id = default;
        return args.Length >= 2 && MiracleId.TryCreate(args[1], out id);
    }

    private static CmdResult Usage() => new(false,
        "Usage: miracle <list|known|has|add|clear> [id]\n" +
        "  list   - list the current run's collected miracle ids\n" +
        "  known  - list every miracle id that exists in this build\n" +
        "  has    - check whether an id is collected\n" +
        "  add    - add an id to the current run's collection\n" +
        "  clear  - clear the current run's collection");
}
