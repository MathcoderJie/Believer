using System;
using System.IO;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// M9-A2 dev-only visual test: native Silent skeleton + the Believer repainted atlas.
///
/// The Silent SpineSkeletonFileResource supplies bones/skins/animation timelines unchanged; only the
/// atlas texture is replaced (BelieverCode/ReskinTest/believer_skin.png). No original Silent resource is
/// modified and the Believer's formal visuals are not replaced.
///
/// Enabled only when user://believer_reskin_test.flag exists. It shows five copies playing
/// idle_loop / attack / cast / hurt / die for screenshots and logs the animation state names.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Preload))]
internal static class SilentReskinTest
{
    private const string MarkerPath = "user://believer_reskin_test.flag";
    private const string TestDir = "user://believer_reskin_test/";
    private const string ScenePath = "res://Believer/reskin_test/believer_test_visuals.tscn";
    private const string NativeSilentData = "res://animations/characters/silent/silent_skel_data.tres";
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
            MainFile.Logger.Error($"[BelieverReskin] failed: {exception}");
        }
    }

    private static void Run()
    {
        var dir = ProjectSettings.GlobalizePath(TestDir);
        Directory.CreateDirectory(dir);
        WriteEmbedded("believer_skin.atlas", Path.Combine(dir, "believer_skin.atlas"));
        WriteEmbedded("believer_skin.png", Path.Combine(dir, "believer_skin.png"));
        MainFile.Logger.Info($"[BelieverReskin] test assets written to {dir}");

        // 1) native Silent skeleton data (bones, skins, animations untouched)
        var native = ResourceLoader.Load(NativeSilentData);
        MainFile.Logger.Info($"[BelieverReskin] native silent data: {native?.GetClass()}");
        var nativeSkeletonFile = native?.Get("skeleton_file_res").AsGodotObject();
        var nativeAtlas = native?.Get("atlas_res").AsGodotObject();
        MainFile.Logger.Info(
            $"[BelieverReskin] native skeleton_file_res={nativeSkeletonFile?.GetClass()}, " +
            $"atlas_res={nativeAtlas?.GetClass()}");
        if (nativeSkeletonFile == null)
        {
            MainFile.Logger.Error("[BelieverReskin] Could not get native Silent skeleton_file_res.");
            return;
        }

        // 2) custom Believer atlas (same layout/metadata as the native Silent atlas)
        var atlas = ClassDB.Instantiate("SpineAtlasResource").AsGodotObject();
        if (atlas == null)
        {
            MainFile.Logger.Error("[BelieverReskin] Could not instantiate SpineAtlasResource.");
            return;
        }
        var atlasError = atlas.Call("load_from_atlas_file", Path.Combine(dir, "believer_skin.atlas"));
        MainFile.Logger.Info($"[BelieverReskin] custom atlas load err={atlasError}");
        if (atlasError.AsInt32() != 0)
        {
            return;
        }

        var data = ClassDB.Instantiate("SpineSkeletonDataResource").AsGodotObject();
        if (data == null)
        {
            MainFile.Logger.Error("[BelieverReskin] Could not instantiate SpineSkeletonDataResource.");
            return;
        }
        data.Set("skeleton_file_res", nativeSkeletonFile);
        data.Set("atlas_res", atlas);
        data.Set("default_mix", 0.05f);

        var animations = data.Call("get_animations").AsGodotArray();
        var names = new string[animations.Count];
        for (var i = 0; i < animations.Count; i++)
        {
            var animation = animations[i].AsGodotObject();
            names[i] = animation?.Call("get_name").ToString() ?? "?";
        }
        MainFile.Logger.Info($"[BelieverReskin] combined data: {animations.Count} animations [{string.Join(", ", names)}]");

        // 3) five test sprites (idle / attack / cast / hurt / die)
        var scene = PreloadManager.Cache.GetScene(ScenePath);
        var tree = (SceneTree)Engine.GetMainLoop();
        var overlay = new Node2D { Name = "BelieverReskinTestOverlay" };
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
            var instance = scene.Instantiate();
            var spine = instance.GetNodeOrNull<Node>("Visuals");
            if (spine == null)
            {
                MainFile.Logger.Error("[BelieverReskin] SpineSprite node missing in test scene.");
                continue;
            }
            spine.Set("skeleton_data_res", data);
            var holder = new Node2D { Name = $"Holder_{animationName}", Position = new Vector2(x, 380f) };
            holder.AddChild(instance);
            var label = new Label
            {
                Text = animationName,
                Position = new Vector2(-70, -330),
                Modulate = new Color(1f, 0.92f, 0.72f, 1f),
            };
            holder.AddChild(label);
            overlay.AddChild(holder);

            var state = spine.Call("get_animation_state").AsGodotObject();
            state?.Call("set_animation", animationName, true, 0);
            state?.Call("update", startOffset);
            var track = state?.Call("get_current", 0).AsGodotObject();
            var animation = track?.Call("get_animation").AsGodotObject();
            var currentName = animation?.Call("get_name").ToString() ?? "(none)";
            MainFile.Logger.Info($"[BelieverReskin] {animationName}: current={currentName}");
        }

        MainFile.Logger.Info("[BelieverReskin] overlay added (remove user://believer_reskin_test.flag to disable).");
    }

    private static void WriteEmbedded(string name, string target)
    {
        var assembly = typeof(SilentReskinTest).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbedPrefix + name);
        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded test asset not found: {EmbedPrefix + name}");
        }
        using var file = File.Create(target);
        stream.CopyTo(file);
    }
}
