using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using Believer.BelieverCode.Character;
using Believer.BelieverCode.Resources;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// Dev-only visualization proof for M10: four Believer sprites with a test resource at 0/1/4/7
/// Divinity, plus an off-screen dynamic resource that cycles values and logs the orb count.
/// This is NOT the real combat resource; the production attachment uses NCreature._Ready.
/// Marker: user://believer_orb_test.flag.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Preload))]
internal static class BelieverOrbTest
{
    private const string MarkerPath = "user://believer_orb_test.flag";

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
            MainFile.Logger.Error($"[BelieverOrbTest] failed: {exception}");
        }
    }

    private static void Run()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var overlay = new Node2D { Name = "BelieverOrbTestOverlay" };
        tree.Root.AddChild(overlay);
        // Dev-only clean backdrop so the orb count readout is not mixed with the main-menu art.
        var backdrop = new ColorRect
        {
            Name = "BelieverOrbTestBackdrop",
            Color = new Color(0.075f, 0.08f, 0.105f),
            Size = new Vector2(1920f, 1080f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        overlay.AddChild(backdrop);
        overlay.MoveChild(backdrop, 0);

        var amounts = new[] { 0, 1, 4, 7 };
        var xs = new[] { 190f, 500f, 810f, 1120f };
        for (var i = 0; i < amounts.Length; i++)
        {
            var visuals = BelieverDirectVisuals.Create();
            if (visuals == null)
            {
                MainFile.Logger.Error("[BelieverOrbTest] Could not create direct visuals.");
                return;
            }
            var holder = new Node2D { Name = $"OrbShowcase_{i}", Position = new Vector2(xs[i], 540f) };
            holder.AddChild(visuals);
            holder.AddChild(new Label
            {
                Text = $"Divinity {amounts[i]}/7",
                Position = new Vector2(-70, -300),
                Modulate = new Color(1f, 0.92f, 0.72f, 1f),
            });
            overlay.AddChild(holder);

            var effectLayer = visuals.GetNodeOrNull<Node2D>("BelieverEffectLayer");
            if (effectLayer == null)
            {
                MainFile.Logger.Error("[BelieverOrbTest] Missing EffectLayer.");
                continue;
            }
            var orbLayer = new BelieverDivinityOrbLayer { Name = $"OrbTestLayer_{i}" };
            effectLayer.AddChild(orbLayer);
            var resource = new BelieverDivinityResource();
            orbLayer.BindForTest(resource);
            resource.Amount = amounts[i];
            MainFile.Logger.Info(
                $"[BelieverOrbTest] showcase {i}: divinity={resource.Amount} active={orbLayer.ActiveCount} desired={orbLayer.DesiredCount}");
        }

        // Dynamic synchronization test: 0 -> 1 -> 4 -> 7 -> 3 -> 0 -> 7 -> 1.
        var dynamicVisuals = BelieverDirectVisuals.Create();
        if (dynamicVisuals == null)
        {
            return;
        }
        var dynamicHolder = new Node2D { Name = "OrbDynamicHolder", Position = new Vector2(-1200f, -800f) };
        dynamicHolder.AddChild(dynamicVisuals);
        overlay.AddChild(dynamicHolder);
        var dynamicEffect = dynamicVisuals.GetNodeOrNull<Node2D>("BelieverEffectLayer");
        if (dynamicEffect == null)
        {
            return;
        }
        var dynamicLayer = new BelieverDivinityOrbLayer { Name = "OrbDynamicLayer" };
        dynamicEffect.AddChild(dynamicLayer);
        var dynamicResource = new BelieverDivinityResource();
        dynamicLayer.BindForTest(dynamicResource);

        var sequence = new[] { 0, 1, 4, 7, 3, 0, 7, 1 };
        var index = 0;
        var timer = new Godot.Timer { WaitTime = 0.7, Autostart = true, OneShot = false };
        timer.Timeout += () =>
        {
            var value = sequence[index++ % sequence.Length];
            dynamicResource.Amount = value;
            MainFile.Logger.Info(
                $"[BelieverOrbTest] dynamic amount={dynamicResource.Amount} active={dynamicLayer.ActiveCount} desired={dynamicLayer.DesiredCount}");
        };
        dynamicHolder.AddChild(timer);
        MainFile.Logger.Info("[BelieverOrbTest] orb showcase + dynamic sync test added");
    }
}
