using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using Believer.BelieverCode.Character;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// Dev-only visual proof for the Omniscience sequence. It reuses the exact BelieverOmniscienceVfxLayer
/// used in combat, but drives it directly so the charge/ascend/burst/sustain phases can be inspected
/// without starting a combat. Marker: user://believer_omniscience_vfx_test.flag.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Preload))]
internal static class BelieverOmniscienceVfxTest
{
    private const string MarkerPath = "user://believer_omniscience_vfx_test.flag";

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!Godot.FileAccess.FileExists(MarkerPath)) return;
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.CreateTimer(3.0).Timeout += () =>
        {
            try { Run(tree); }
            catch (Exception exception) { MainFile.Logger.Error($"[OmniscienceVfxTest] failed: {exception}"); }
        };
    }

    private static void Run(SceneTree tree)
    {
        var overlay = new Node2D { Name = "OmniscienceVfxTestOverlay" };
        tree.Root.AddChild(overlay);
        overlay.AddChild(new ColorRect
        {
            Color = new Color(0.035f, 0.04f, 0.055f),
            Size = new Vector2(1920f, 1080f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        var visuals = BelieverDirectVisuals.Create();
        if (visuals == null)
        {
            MainFile.Logger.Error("[OmniscienceVfxTest] direct visuals creation failed");
            return;
        }
        var holder = new Node2D { Name = "OmniscienceShowcase", Position = new Vector2(760f, 760f) };
        holder.AddChild(visuals);
        overlay.AddChild(holder);

        var effectLayer = visuals.GetNodeOrNull<Node2D>("BelieverEffectLayer");
        if (effectLayer == null)
        {
            MainFile.Logger.Error("[OmniscienceVfxTest] BelieverEffectLayer missing");
            return;
        }
        var vfx = new BelieverOmniscienceVfxLayer { Name = "OmniscienceVfxTestLayer" };
        vfx.Initialize(null, visuals);
        effectLayer.AddChild(vfx);
        vfx.Begin(7);
        MainFile.Logger.Info("[OmniscienceVfxTest] sequence started with 7 orbs");

        tree.CreateTimer(1.8).Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(vfx)) vfx.PlayCardBurst();
        };
        tree.CreateTimer(2.5).Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(vfx)) vfx.ShieldFeedback();
        };
    }
}
