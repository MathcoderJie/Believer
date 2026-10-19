using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using HarmonyLib;

namespace Believer.BelieverCode.Character;

/// <summary>
/// Lightweight animation for the direct-texture Believer. The character remains a Sprite2D;
/// idle float and short attack/cast/hurt/death overlays are applied to the Sprite2D's local
/// position/scale/rotation/modulate. Every overlay is based on the captured base transform, so
/// repeated or interrupted triggers cannot accumulate drift.
/// </summary>
internal partial class BelieverDirectAnimator : Node
{
    private enum AnimKind
    {
        None,
        Attack,
        Cast,
        Hurt,
        Dead,
    }

    private const float IdlePeriod = 1.6f;
    private const float IdleAmplitude = 3.0f;
    private const float AttackDuration = 0.24f;
    private const float CastDuration = 0.42f;
    private const float HurtDuration = 0.15f;
    private const float DeadDuration = 0.65f;

    private Sprite2D? _body;
    private Sprite2D? _shadow;
    private Vector2 _basePosition;
    private Vector2 _baseScale;
    private Vector2 _baseShadowScale = Vector2.One;
    private float _baseRotation;
    private Color _baseModulate = Colors.White;

    private float _idleTime;
    private float _actionTime;
    private AnimKind _action;
    private bool _dead;

    public override void _Ready()
    {
        // The animator is a child of NCreatureVisuals; %Visuals is unique to the visual root,
        // not to this child, so resolve the sibling by name as well.
        _body = GetNodeOrNull<Sprite2D>("%Visuals")
            ?? GetParent()?.GetNodeOrNull<Sprite2D>("Visuals")
            ?? FindChild("Visuals", true, false) as Sprite2D;
        if (_body == null)
        {
            MainFile.Logger.Error("[BelieverDirectAnim] %Visuals Sprite2D not found; animation disabled.");
            SetProcess(false);
            return;
        }
        _basePosition = _body.Position;
        _baseScale = _body.Scale;
        _baseRotation = _body.Rotation;
        _baseModulate = _body.Modulate;
        _shadow = GetParent()?.GetNodeOrNull<Sprite2D>("BelieverGroundShadow");
        if (_shadow != null)
        {
            _baseShadowScale = _shadow.Scale;
        }
        MainFile.Logger.Info(
            $"[BelieverDirectAnim] animator ready basePos={_basePosition} baseScale={_baseScale}");
    }

    public void Trigger(string trigger)
    {
        if (_body == null)
        {
            return;
        }
        var kind = trigger switch
        {
            "Attack" => AnimKind.Attack,
            "Cast" or "PowerUp" => AnimKind.Cast,
            "Hit" => AnimKind.Hurt,
            "Dead" or "die" => AnimKind.Dead,
            _ => AnimKind.None,
        };
        if (kind == AnimKind.None)
        {
            return;
        }
        if (_dead && kind != AnimKind.Dead)
        {
            return;
        }
        if (kind == AnimKind.Dead)
        {
            _dead = true;
        }
        _action = kind;
        _actionTime = 0f;
        MainFile.Logger.Info($"[BelieverDirectAnim] trigger={trigger} action={kind}");
    }

    public override void _Process(double delta)
    {
        if (_body == null)
        {
            return;
        }
        var dt = (float)delta;
        if (!_dead)
        {
            _idleTime += dt;
        }
        if (_action != AnimKind.None)
        {
            _actionTime += dt;
        }

        var idleOffset = _dead ? 0f : Mathf.Sin(_idleTime * Mathf.Tau / IdlePeriod) * IdleAmplitude;
        var breath = _dead ? 0f : Mathf.Sin(_idleTime * Mathf.Tau / IdlePeriod + 1.1f) * 0.006f;
        var x = 0f;
        var y = idleOffset;
        var scale = 1f + breath;
        var rotation = 0f;
        var flash = 0f;
        var alpha = 1f;

        switch (_action)
        {
            case AnimKind.Attack:
            {
                var p = Mathf.Clamp(_actionTime / AttackDuration, 0f, 1f);
                // Quick forward lunge, then a soft return.
                var lunge = p < 0.42f ? Mathf.Sin((p / 0.42f) * Mathf.Pi * 0.5f)
                    : Mathf.Cos(((p - 0.42f) / 0.58f) * Mathf.Pi * 0.5f);
                x = 8f * lunge;
                y -= 1.5f * lunge;
                scale += 0.018f * lunge;
                flash = 0.035f * lunge;
                break;
            }
            case AnimKind.Cast:
            {
                var p = Mathf.Clamp(_actionTime / CastDuration, 0f, 1f);
                var lift = Mathf.Sin(p * Mathf.Pi);
                y -= 9f * lift;
                scale += 0.028f * lift;
                flash = 0.025f * lift;
                break;
            }
            case AnimKind.Hurt:
            {
                var p = Mathf.Clamp(_actionTime / HurtDuration, 0f, 1f);
                x = -5.5f * Mathf.Sin(p * Mathf.Pi) + 2.0f * Mathf.Sin(p * Mathf.Pi * 8f) * (1f - p);
                scale += 0.018f * Mathf.Sin(p * Mathf.Pi);
                flash = 0.22f * (1f - p);
                break;
            }
            case AnimKind.Dead:
            {
                var p = Mathf.Clamp(_actionTime / DeadDuration, 0f, 1f);
                y += 13f * p;
                scale *= 1f - 0.10f * p;
                alpha = 1f - 0.82f * p;
                break;
            }
        }

        if (_action != AnimKind.None)
        {
            var done = _action switch
            {
                AnimKind.Attack => _actionTime >= AttackDuration,
                AnimKind.Cast => _actionTime >= CastDuration,
                AnimKind.Hurt => _actionTime >= HurtDuration,
                AnimKind.Dead => _actionTime >= DeadDuration,
                _ => true,
            };
            if (done && _action != AnimKind.Dead)
            {
                _action = AnimKind.None;
                _actionTime = 0f;
            }
        }

        _body.Position = _basePosition + new Vector2(x, y);
        _body.Scale = _baseScale * scale;
        _body.Rotation = _baseRotation + rotation;
        var tint = _baseModulate * (1f + flash);
        tint.A = alpha;
        _body.Modulate = tint;

        if (_shadow != null)
        {
            // Sync only very slightly: a lifted body gets a marginally smaller contact shadow.
            var lift = Mathf.Abs(y);
            var shadowScale = _baseShadowScale * (1f - Mathf.Min(0.06f, lift * 0.004f));
            _shadow.Scale = shadowScale;
        }
    }
}

/// <summary>
/// Forwards the game's existing animation triggers to the direct-texture animator. The vanilla
/// CreatureAnimator only exists for Spine visuals; this keeps attack/cast/hit/death feedback
/// working for the Believer's Sprite2D.
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class BelieverDirectAnimationTriggerPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance, string trigger)
    {
        var visuals = __instance.Visuals;
        var animator = visuals?.GetNodeOrNull<BelieverDirectAnimator>("BelieverDirectAnimator");
        if (animator != null)
        {
            animator.Trigger(trigger);
        }
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.AnimShake))]
internal static class BelieverDirectHurtPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        var visuals = __instance.Visuals;
        var animator = visuals?.GetNodeOrNull<BelieverDirectAnimator>("BelieverDirectAnimator");
        animator?.Trigger("Hit");
    }
}
