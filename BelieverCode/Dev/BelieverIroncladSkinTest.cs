using System;
using System.IO;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// M9-A3.2 dev-only test: native Ironclad scene + Believer-owned Ironclad atlas pages.
/// Bones/meshes/weights/animation timelines and the native scene are not modified; only the
/// texture atlas is replaced at runtime. Activated only by user://believer_ironclad_skin_test.flag.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Preload))]
internal static class BelieverIroncladSkinTest
{
    private const string MarkerPath = "user://believer_ironclad_skin_test.flag";
    private const string TestDir = "user://believer_ironclad_skin_test/";
    private const string NativeIroncladScene = "res://scenes/creature_visuals/ironclad.tscn";
    private const string NativeIroncladData = "res://animations/characters/ironclad/ironclad_skel_data.tres";
    private const string EmbedPrefix = "Believer.BelieverCode.ReskinTest.";

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
            MainFile.Logger.Error($"[BelieverIroncladSkin] failed: {exception}");
        }
    }

    private static void Run()
    {
        var dir = ProjectSettings.GlobalizePath(TestDir);
        Directory.CreateDirectory(dir);
        WriteEmbedded("believer_ironclad.atlas", Path.Combine(dir, "believer_ironclad.atlas"));
        for (var i = 1; i <= 4; i++)
        {
            var name = i == 1 ? "believer_ironclad.png" : $"believer_ironclad_{i}.png";
            WriteEmbedded(name, Path.Combine(dir, name));
        }
        MainFile.Logger.Info($"[BelieverIroncladSkin] skin assets written to {dir}");

        // Native Ironclad skeleton file; only the atlas is replaced.
        var nativeData = ResourceLoader.Load(NativeIroncladData);
        var nativeSkeleton = nativeData?.Get("skeleton_file_res").AsGodotObject();
        MainFile.Logger.Info(
            $"[BelieverIroncladSkin] native data={nativeData?.GetClass()} " +
            $"skeleton_file_res={nativeSkeleton?.GetClass()}");
        if (nativeSkeleton == null)
        {
            MainFile.Logger.Error("[BelieverIroncladSkin] Could not load Ironclad skeleton file.");
            return;
        }

        var atlas = ClassDB.Instantiate("SpineAtlasResource").AsGodotObject();
        if (atlas == null)
        {
            MainFile.Logger.Error("[BelieverIroncladSkin] Could not instantiate SpineAtlasResource.");
            return;
        }
        var atlasError = atlas.Call("load_from_atlas_file", Path.Combine(dir, "believer_ironclad.atlas"));
        MainFile.Logger.Info($"[BelieverIroncladSkin] custom atlas load err={atlasError}");
        if (atlasError.AsInt32() != 0)
        {
            return;
        }

        var skinData = ClassDB.Instantiate("SpineSkeletonDataResource").AsGodotObject();
        if (skinData == null)
        {
            MainFile.Logger.Error("[BelieverIroncladSkin] Could not instantiate SpineSkeletonDataResource.");
            return;
        }
        skinData.Set("skeleton_file_res", nativeSkeleton);
        skinData.Set("atlas_res", atlas);
        skinData.Set("default_mix", 0.05f);

        var animationArray = skinData.Call("get_animations").AsGodotArray();
        var names = new string[animationArray?.Count ?? 0];
        for (var i = 0; i < names.Length; i++)
        {
            var animation = animationArray![i].AsGodotObject();
            names[i] = animation?.Call("get_name").ToString() ?? "?";
        }
        MainFile.Logger.Info($"[BelieverIroncladSkin] custom data animations: {string.Join(", ", names)}");

        var nativeScene = PreloadManager.Cache.GetScene(NativeIroncladScene);
        if (nativeScene == null)
        {
            MainFile.Logger.Error("[BelieverIroncladSkin] Could not load native Ironclad scene.");
            return;
        }

        var tree = (SceneTree)Engine.GetMainLoop();
        var overlay = new Node2D { Name = "BelieverIroncladSkinTestOverlay" };
        tree.Root.AddChild(overlay);
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
                MainFile.Logger.Error("[BelieverIroncladSkin] native scene has no Visuals node.");
                continue;
            }
            spine.Set("skeleton_data_res", skinData);
            var holder = new Node2D { Name = $"BelieverIroncladSkin_{animationName}", Position = new Vector2(x, 380f) };
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
            MainFile.Logger.Info($"[BelieverIroncladSkin] {animationName}: current={currentName}");
        }
        MainFile.Logger.Info("[BelieverIroncladSkin] overlay added (remove marker to disable).");
    }

    private static void WriteEmbedded(string name, string target)
    {
        var assembly = typeof(BelieverIroncladSkinTest).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbedPrefix + name);
        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded skin asset not found: {EmbedPrefix + name}");
        }
        using var file = File.Create(target);
        stream.CopyTo(file);
    }
}
