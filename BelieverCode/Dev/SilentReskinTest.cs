using System;
using System.IO;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Models;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// Dev-only visual harness for the Believer reskin.
///
/// Route A (user://believer_reskin_test.flag): native Silent binary skeleton + Believer atlas.
/// Route B (user://believer_hood_test.flag): a generated Spine JSON skeleton + Believer atlas, used to
/// validate the binary-to-JSON conversion and the reshaped hood. The JSON test reads its skeleton from
/// user://believer_hood_test/{base,hood}.json (use_hood.flag selects the hood variant).
///
/// It never modifies Silent's resources or the Believer's formal character visuals.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Preload))]
internal static class SilentReskinTest
{
    private const string MarkerPath = "user://believer_reskin_test.flag";
    private const string HoodMarkerPath = "user://believer_hood_test.flag";
    private const string TestDir = "user://believer_reskin_test/";
    private const string HoodTestDir = "user://believer_hood_test/";
    private const string ScenePath = "res://Believer/reskin_test/believer_test_visuals.tscn";
    private const string NativeSilentData = "res://animations/characters/silent/silent_skel_data.tres";
    private const string EmbedPrefix = "Believer.BelieverCode.ReskinTest.";

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (Godot.FileAccess.FileExists(HoodMarkerPath))
        {
            try
            {
                RunJsonSkeletonTest();
            }
            catch (Exception exception)
            {
                MainFile.Logger.Error($"[BelieverReskin] hood JSON test failed: {exception}");
            }
            return;
        }

        if (!Godot.FileAccess.FileExists(MarkerPath))
        {
            return;
        }

        try
        {
            RunNativeTest();
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"[BelieverReskin] failed: {exception}");
        }
    }

    private static void RunNativeTest()
    {
        var dir = EnsureAtlas();
        var native = ResourceLoader.Load(NativeSilentData);
        MainFile.Logger.Info($"[BelieverReskin] native silent data: {native?.GetClass()}");
        var skeletonFile = native?.Get("skeleton_file_res").AsGodotObject();
        var nativeAtlas = native?.Get("atlas_res").AsGodotObject();
        MainFile.Logger.Info(
            $"[BelieverReskin] native skeleton_file_res={skeletonFile?.GetClass()}, " +
            $"atlas_res={nativeAtlas?.GetClass()}");
        if (skeletonFile == null)
        {
            MainFile.Logger.Error("[BelieverReskin] Could not get native Silent skeleton_file_res.");
            return;
        }

        var data = BuildData(skeletonFile);
        if (data == null)
        {
            return;
        }
        ShowFive(data, "native");
        MainFile.Logger.Info($"[BelieverReskin] native test assets in {dir}");
    }

    private static void RunJsonSkeletonTest()
    {
        var dir = EnsureAtlas();
        var useHood = Godot.FileAccess.FileExists(HoodTestDir + "use_hood.flag");
        var godotPath = useHood ? HoodTestDir + "believer_hood.spjson" : HoodTestDir + "base.spjson";
        if (useHood)
        {
            var hoodDir = ProjectSettings.GlobalizePath(HoodTestDir);
            Directory.CreateDirectory(hoodDir);
            WriteEmbedded("believer_hood.spjson", Path.Combine(hoodDir, "believer_hood.spjson"));
        }
        var osPath = ProjectSettings.GlobalizePath(godotPath);
        MainFile.Logger.Info($"[BelieverReskin] JSON test: {godotPath} (useHood={useHood}), osPath={osPath}");
        if (!Godot.FileAccess.FileExists(godotPath))
        {
            MainFile.Logger.Error($"[BelieverReskin] Missing JSON skeleton: {osPath}");
            return;
        }

        var skeletonFile = ClassDB.Instantiate("SpineSkeletonFileResource").AsGodotObject();
        if (skeletonFile == null)
        {
            MainFile.Logger.Error("[BelieverReskin] Could not instantiate SpineSkeletonFileResource.");
            return;
        }
        try
        {
            var methods = ClassDB.ClassGetMethodList("SpineSkeletonFileResource");
            MainFile.Logger.Info($"[BelieverReskin] SpineSkeletonFileResource methods: {string.Join(", ", DescribeMethods(methods))}");
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"[BelieverReskin] Could not list methods: {exception.Message}");
        }

        var loadError = skeletonFile.Call("load_from_file", osPath);
        MainFile.Logger.Info($"[BelieverReskin] JSON load_from_file err={loadError}");
        if (loadError.AsInt32() != 0)
        {
            return;
        }

        var data = BuildData(skeletonFile);
        if (data == null)
        {
            return;
        }
        ShowFive(data, useHood ? "hood" : "base-roundtrip");
        MainFile.Logger.Info("[BelieverReskin] JSON overlay added (remove user://believer_hood_test.flag to disable).");
    }

    private static GodotObject? BuildData(GodotObject skeletonFile)
    {
        var atlas = LoadBelieverAtlas();
        if (atlas == null)
        {
            return null;
        }
        var data = ClassDB.Instantiate("SpineSkeletonDataResource").AsGodotObject();
        if (data == null)
        {
            MainFile.Logger.Error("[BelieverReskin] Could not instantiate SpineSkeletonDataResource.");
            return null;
        }
        data.Set("skeleton_file_res", skeletonFile);
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
        return data;
    }

    private static GodotObject? LoadBelieverAtlas()
    {
        var dir = ProjectSettings.GlobalizePath(TestDir);
        var path = Path.Combine(dir, "believer_skin.atlas");
        // Head-prototype mode uses the Believer-owned hood atlas embedded in the DLL.
        if (Godot.FileAccess.FileExists(HoodTestDir + "use_hood.flag"))
        {
            var hoodDir = ProjectSettings.GlobalizePath(HoodTestDir);
            Directory.CreateDirectory(hoodDir);
            WriteEmbedded("believer_hood.atlas", Path.Combine(hoodDir, "believer_hood.atlas"));
            WriteEmbedded("believer_hood.atlas.png", Path.Combine(hoodDir, "believer_hood.atlas.png"));
            path = Path.Combine(hoodDir, "believer_hood.atlas");
            MainFile.Logger.Info($"[BelieverReskin] using embedded hood atlas: {path}");
        }
        var atlas = ClassDB.Instantiate("SpineAtlasResource").AsGodotObject();
        if (atlas == null)
        {
            MainFile.Logger.Error("[BelieverReskin] Could not instantiate SpineAtlasResource.");
            return null;
        }
        var error = atlas.Call("load_from_atlas_file", path);
        MainFile.Logger.Info($"[BelieverReskin] custom atlas load err={error}");
        return error.AsInt32() == 0 ? atlas : null;
    }

    private static string EnsureAtlas()
    {
        var dir = ProjectSettings.GlobalizePath(TestDir);
        Directory.CreateDirectory(dir);
        WriteEmbedded("believer_skin.atlas", Path.Combine(dir, "believer_skin.atlas"));
        WriteEmbedded("believer_skin.png", Path.Combine(dir, "believer_skin.png"));
        Directory.CreateDirectory(ProjectSettings.GlobalizePath(HoodTestDir));
        MainFile.Logger.Info($"[BelieverReskin] test assets written to {dir}");
        return dir;
    }

    private static void ShowFive(GodotObject data, string tag)
    {
        var scene = PreloadManager.Cache.GetScene(ScenePath);
        var tree = (SceneTree)Engine.GetMainLoop();
        var overlay = new Node2D { Name = $"BelieverReskinTestOverlay_{tag}" };
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
            var holder = new Node2D { Name = $"Holder_{tag}_{animationName}", Position = new Vector2(x, 380f) };
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
            MainFile.Logger.Info($"[BelieverReskin] {tag}/{animationName}: current={currentName}");
        }
    }

    private static string[] DescribeMethods(Godot.Collections.Array<Godot.Collections.Dictionary> methods)
    {
        var result = new string[methods.Count];
        for (var i = 0; i < methods.Count; i++)
        {
            result[i] = methods[i]["name"].ToString() ?? "?";
        }
        return result;
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
