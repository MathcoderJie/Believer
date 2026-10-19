using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using Believer.BelieverCode.Character;

namespace Believer.BelieverCode.Dev;

/// <summary>
/// M9-A4 dev-only: validate direct transparent PNG / NCreatureVisuals display without a SpineSprite.
/// Activated only by user://believer_direct_texture_test.flag.
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Preload))]
internal static class BelieverDirectTextureTest
{
    private const string MarkerPath = "user://believer_direct_texture_test.flag";

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
            MainFile.Logger.Error($"[BelieverDirectTest] failed: {exception}");
        }
    }

    private static void Run()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var overlay = new Node2D { Name = "BelieverDirectTextureTestOverlay" };
        tree.Root.AddChild(overlay);

        var positions = new[] { 260f, 600f, 940f };
        for (var i = 0; i < positions.Length; i++)
        {
            var visuals = BelieverDirectVisuals.Create();
            if (visuals == null)
            {
                MainFile.Logger.Error("[BelieverDirectTest] Could not create direct PNG visuals.");
                return;
            }
            var holder = new Node2D { Name = $"BelieverDirect_{i}", Position = new Vector2(positions[i], 520f) };
            holder.AddChild(visuals);
            var label = new Label
            {
                Text = i == 0 ? "direct PNG / static" : $"copy {i + 1}",
                Position = new Vector2(-70, -300),
                Modulate = new Color(1f, 0.92f, 0.72f, 1f),
            };
            holder.AddChild(label);
            overlay.AddChild(holder);

            // Exercise the lightweight overlay animations independently of combat input.
            if (i == 0 && visuals.GetNodeOrNull<BelieverDirectAnimator>("BelieverDirectAnimator") is { } animator)
            {
                var timer = new Godot.Timer { WaitTime = 0.9, Autostart = true, OneShot = false };
                var step = 0;
                var sequence = new[] { "Attack", "Cast", "Hit" };
                timer.Timeout += () =>
                {
                    var trigger = sequence[step++ % sequence.Length];
                    animator.Trigger(trigger);
                    label.Text = $"direct PNG + {trigger}";
                    MainFile.Logger.Info($"[BelieverDirectTest] manual trigger {trigger}");
                };
                holder.AddChild(timer);
            }
            else if (i == 1 && visuals.GetNodeOrNull<BelieverDirectAnimator>("BelieverDirectAnimator") is { } deathAnimator)
            {
                var deathTimer = new Godot.Timer { WaitTime = 2.0, OneShot = true, Autostart = true };
                deathTimer.Timeout += () =>
                {
                    deathAnimator.Trigger("Dead");
                    label.Text = "direct PNG + Dead";
                    MainFile.Logger.Info("[BelieverDirectTest] manual trigger Dead");
                };
                holder.AddChild(deathTimer);
            }
        }
        // Verify the exact formal hook used by the Believer character model, not only the helper.
        try
        {
            var formal = ModelDb.Character<Believer.BelieverCode.Character.Believer>().CreateCustomVisuals();
            if (formal != null)
            {
                var holder = new Node2D { Name = "BelieverFormalVisuals", Position = new Vector2(1280f, 520f) };
                holder.AddChild(formal);
                holder.AddChild(new Label
                {
                    Text = "formal CreateCustomVisuals",
                    Position = new Vector2(-90, -300),
                    Modulate = new Color(1f, 0.92f, 0.72f, 1f),
                });
                overlay.AddChild(holder);
                MainFile.Logger.Info($"[BelieverDirectTest] formal Believer.CreateCustomVisuals => {formal.GetClass()}");
            }
            else
            {
                MainFile.Logger.Warn("[BelieverDirectTest] formal Believer.CreateCustomVisuals returned null");
            }
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"[BelieverDirectTest] formal CreateCustomVisuals failed: {exception}");
        }
        MainFile.Logger.Info("[BelieverDirectTest] direct PNG overlay added (remove marker to disable).");
    }
}
