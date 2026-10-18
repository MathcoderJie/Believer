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
        // M2 omniscience: the 神迹收藏 (Miracle collection) system does not exist yet, so a
        // development-only provider stands in for it until that system registers its own.
        MiracleRegistry.Register(new DevMiracleProvider());

        // M4 miracle collection: store the run's collected miracle ids in the game's own run save
        // (BaseLib extended save data on SerializableRun). This must happen during mod init.
        MiracleCollectionStore.RegisterSave();

        var assembly = Assembly.GetExecutingAssembly();

        //If you want to use scripts defined in your mod for Godot scenes, uncomment the following line.
        //Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(assembly);
     
        Harmony harmony = new(ModId);

        harmony.PatchAll(assembly);
    }
}
