using BaseLib.Extensions;
using Believer.BelieverCode.Resources;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// DEVELOPMENT-ONLY console command for M5 testing: read or edit the Believer's Divinity in the
/// current combat, so the tester can fill the 7/7 bar and enter Omniscience without grinding turns.
///
/// It only touches M1's per-combat <see cref="BelieverDivinityResource"/> (through its clamped
/// <c>Amount</c>/<c>Gain</c> API) and is a debug command like the M4 "miracle" command; it uses the
/// game's networked console command path so the change is shared by all peers in multiplayer.
/// </summary>
public sealed class DivinityConsoleCmd : AbstractConsoleCmd
{
    static DivinityConsoleCmd()
    {
        MainFile.Logger.Info("[Miracles] Dev console command 'divinity' registered.");
    }

    public override string CmdName => "divinity";

    public override string Args => "<show|add|set|full|empty> [amount:int]";

    public override string Description =>
        "[Dev] Read or edit the Believer's Divinity in the current combat (M5 testing).";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer == null) return new CmdResult(false, "This command only works during a run.");
        if (!CombatManager.Instance.IsInProgress) return new CmdResult(false, "This command only works during combat.");

        var divinity = issuingPlayer.GetResource<BelieverDivinityResource>();
        if (divinity == null)
        {
            return new CmdResult(false, "This player has no Believer Divinity resource (wrong character?).");
        }

        if (args.Length == 0) return Usage();

        switch (args[0].ToLowerInvariant())
        {
            case "show":
                return new CmdResult(true, $"Divinity: {divinity.Amount}/{BelieverDivinityResource.MaxAmount}");

            case "add":
            {
                if (!TryParseAmount(args, out var amount) || amount < 0)
                {
                    return new CmdResult(false, "Usage: divinity add <amount >= 0>");
                }

                divinity.Gain(amount);
                return new CmdResult(true, $"Divinity: {divinity.Amount}/{BelieverDivinityResource.MaxAmount}");
            }

            case "set":
            {
                if (!TryParseAmount(args, out var amount))
                {
                    return new CmdResult(false, "Usage: divinity set <amount>");
                }

                divinity.Amount = amount;
                return new CmdResult(true, $"Divinity: {divinity.Amount}/{BelieverDivinityResource.MaxAmount}");
            }

            case "full":
                divinity.Amount = BelieverDivinityResource.MaxAmount;
                return new CmdResult(true, $"Divinity: {divinity.Amount}/{BelieverDivinityResource.MaxAmount}");

            case "empty":
                divinity.Amount = 0;
                return new CmdResult(true, $"Divinity: {divinity.Amount}/{BelieverDivinityResource.MaxAmount}");

            default:
                return Usage();
        }
    }

    private static bool TryParseAmount(string[] args, out int amount)
    {
        amount = 0;
        return args.Length >= 2 && int.TryParse(args[1], out amount);
    }

    private static CmdResult Usage() => new(false,
        "Usage: divinity <show|add|set|full|empty> [amount]\n" +
        "  show  - print current Divinity\n" +
        "  add   - add N Divinity (clamped to 0..7)\n" +
        "  set   - set Divinity to N (clamped to 0..7)\n" +
        "  full  - fill Divinity to 7/7\n" +
        "  empty - set Divinity to 0");
}
