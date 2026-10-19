using System.Reflection;
using Believer.BelieverCode.Miracles;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace Believer.BelieverCode;

//You're recommended but not required to keep all your code in this package and all your assets in the Believer folder.
[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "Believer"; //Used for resource filepath
    public const string ResPath = $"res://{ModId}";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        // M5: Omniscience grants exactly the miracles collected in the current run (M4 collection),
        // bridged through M2's IMiracleProvider seam. This replaces the M2 development placeholder
        // provider, which granted its test miracle regardless of the collection.
        MiracleRegistry.Register(new MiracleCollectionProvider());

        // M4 miracle collection: store the run's collected miracle ids in the game's own run save
        // (BaseLib extended save data on SerializableRun). This must happen during mod init.
        MiracleCollectionStore.RegisterSave();

        var assembly = Assembly.GetExecutingAssembly();

        //If you want to use scripts defined in your mod for Godot scenes, uncomment the following line.
        //Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(assembly);
     
        Harmony harmony = new(ModId);

        harmony.PatchAll(assembly);

        LogDivinityObserverPatchStatus();
        LogMiracleRewardPatchStatus();

        // M7 追加: the 全知全能 ability card is no longer part of the starting deck (the deck is the
        // 10 basic cards), so it is granted into the hand at the start of each combat instead.
        Mechanics.OmniscienceAvailability.Initialize();
    }

    /// <summary>
    /// Logs whether the normal-Miracle acquisition observer attached. A missed Harmony target would
    /// otherwise leave Omniscience unaware of acquired rare Miracles while the build still succeeds.
    /// </summary>
    private static void LogMiracleRewardPatchStatus()
    {
        var add = AccessTools.Method(
            typeof(MegaCrit.Sts2.Core.Commands.CardPileCmd),
            "Add",
            new[]
            {
                typeof(MegaCrit.Sts2.Core.Models.CardModel),
                typeof(MegaCrit.Sts2.Core.Entities.Cards.PileType),
                typeof(MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition),
                typeof(MegaCrit.Sts2.Core.Models.AbstractModel),
                typeof(bool),
            });
        var addInfo = add == null ? null : Harmony.GetPatchInfo(add);
        Logger.Info(
            $"[Patches] Miracle acquisition observer: deck-add prefix={(addInfo?.Prefixes?.Count ?? 0)}, " +
            $"targetFound={add != null}.");
    }

    private static void LogDivinityObserverPatchStatus()
    {
        var gainMethod = AccessTools.Method(
            typeof(Resources.BelieverDivinityResource),
            nameof(Resources.BelieverDivinityResource.Gain));

        var patchInfo = gainMethod == null ? null : Harmony.GetPatchInfo(gainMethod);
        var prefixes = patchInfo?.Prefixes?.Count ?? 0;
        var postfixes = patchInfo?.Postfixes?.Count ?? 0;

        Logger.Info($"[Patches] Divinity gain observer: prefix={prefixes}, postfix={postfixes}.");
    }
}
