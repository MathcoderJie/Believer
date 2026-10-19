using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// M9-A3.1 dev-only: load the native Ironclad creature visual scene, without touching the
/// native scene/resources or the formal Believer character logic. Shows five copies playing
/// Ironclad's actual animations so the model鎺ュ叆 can be checked in-game.
///
/// Activated only by user://believer_ironclad_test.flag.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Preload))]
internal static class BelieverIroncladTest
{
    private const string MarkerPath = "user://believer_ironclad_test.flag";
    private const string NativeIroncladScene = "res://scenes/creature_visuals/ironclad.tscn";
    private const string NativeIroncladData = "res://animations/characters/ironclad/ironclad_skel_data.tres";

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!Godot.FileAccess.FileExists(MarkerPath))
        {
            return;
        }

        try
        {
            Run();
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"[BelieverIronclad] failed: {exception}");
        }
    }

    private static void Run()
    {
        // 1) native Ironclad skeleton/atlas resources
        var nativeData = ResourceLoader.Load(NativeIroncladData);
        MainFile.Logger.Info($"[BelieverIronclad] native data={nativeData?.GetClass()} path={NativeIroncladData}");
        var skeletonFile = nativeData?.Get("skeleton_file_res").AsGodotObject();
        var atlas = nativeData?.Get("atlas_res").AsGodotObject();
        MainFile.Logger.Info(
            $"[BelieverIronclad] skeleton_file_res={skeletonFile?.GetClass()} atlas_res={atlas?.GetClass()}");

        var animationArray = nativeData?.Call("get_animations").AsGodotArray();
        if (animationArray != null)
        {
            var names = new string[animationArray.Count];
            for (var i = 0; i < animationArray.Count; i++)
            {
                var animation = animationArray[i].AsGodotObject();
                names[i] = animation?.Call("get_name").ToString() ?? "?";
            }
            MainFile.Logger.Info($"[BelieverIronclad] native animations: {string.Join(", ", names)}");
        }

        // 2) native Ironclad visual scene
        var nativeScene = PreloadManager.Cache.GetScene(NativeIroncladScene);
        if (nativeScene == null)
        {
            MainFile.Logger.Error($"[BelieverIronclad] Could not load native scene: {NativeIroncladScene}");
            return;
        }
        MainFile.Logger.Info($"[BelieverIronclad] native scene loaded: {NativeIroncladScene}");

        var tree = (SceneTree)Engine.GetMainLoop();
        var overlay = new Node2D { Name = "BelieverIroncladTestOverlay" };
        tree.Root.AddChild(overlay);

        // Ironclad's actual names (not Silent's): attack/attack_heavy/cast/die/hurt/idle_loop/relaxed_loop/weak_loop.
        var layers = new[]
        {
            ("idle_loop", 0.00f, 140.0f),
            ("attack", 0.18f, 400.0f),
            ("cast", 0.40f, 660.0f),
            ("hurt", 0.06f, 920.0f),
            ("die", 0.55f, 1180.0f),
        };
        foreach (var (animationName, startOffset, x) in layers)
        {
            var instance = nativeScene.Instantiate();
            var spine = instance.GetNodeOrNull<Node>("Visuals");
            if (spine == null)
            {
                MainFile.Logger.Error("[BelieverIronclad] native scene has no Visuals node.");
                continue;
            }
            var holder = new Node2D { Name = $"IroncladHolder_{animationName}", Position = new Vector2(x, 380f) };
            holder.AddChild(instance);
            holder.AddChild(new Label
            {
                Text = animationName,
                Position = new Vector2(-70, -330),
                Modulate = new Color(1f, 0.92f, 0.72f, 1f),
            });
            overlay.AddChild(holder);

            var state = spine.Call("get_animation_state").AsGodotObject();
            state?.Call("set_animation", animationName, true, 0);
            state?.Call("update", startOffset);
            var track = state?.Call("get_current", 0).AsGodotObject();
            var animation = track?.Call("get_animation").AsGodotObject();
            var currentName = animation?.Call("get_name").ToString() ?? "(none)";
            MainFile.Logger.Info($"[BelieverIronclad] {animationName}: current={currentName}");
        }

        MainFile.Logger.Info("[BelieverIronclad] overlay added (remove user://believer_ironclad_test.flag to disable).");
    }
}
