using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using Believer.BelieverCode.Character;
using Believer.BelieverCode.Resources;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// Dev-only preview for the custom Divinity HUD at 0/3/7 points.
/// Marker: user://believer_divinity_ui_test.flag.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Preload))]
internal static class BelieverDivinityUiTest
{
    private const string MarkerPath = "user://believer_divinity_ui_test.flag";

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!Godot.FileAccess.FileExists(MarkerPath)) return;
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.CreateTimer(3.0).Timeout += () =>
        {
            try { Run(tree); }
            catch (Exception exception) { MainFile.Logger.Error($"[DivinityUiTest] failed: {exception}"); }
        };
    }

    private static void Run(SceneTree tree)
    {
        var overlay = new Node2D { Name = "DivinityUiTestOverlay" };
        tree.Root.AddChild(overlay);
        overlay.AddChild(new ColorRect
        {
            Color = new Color(0.04f, 0.045f, 0.06f),
            Size = new Vector2(1920f, 1080f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        var amounts = new[] { 0, 3, 7 };
        var xs = new[] { 420f, 820f, 1220f };
        for (var i = 0; i < amounts.Length; i++)
        {
            var resource = new BelieverDivinityResource { Amount = amounts[i] };
            var display = new BelieverDivinityDisplay(resource)
            {
                Position = new Vector2(xs[i], 440f),
            };
            overlay.AddChild(display);
            var label = new Label
            {
                Text = $"Divinity {amounts[i]}/7",
                Position = new Vector2(xs[i] - 10f, 600f),
            };
            label.AddThemeFontSizeOverride("font_size", 20);
            overlay.AddChild(label);
            MainFile.Logger.Info($"[DivinityUiTest] showcase amount={resource.Amount} display={display.Size}");
        }
    }
}
