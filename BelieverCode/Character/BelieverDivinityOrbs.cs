using System;
using System.Collections.Generic;
using System.IO;
using BaseLib.Extensions;
using Believer.BelieverCode.Resources;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using HarmonyLib;

namespace Believer.BelieverCode.Character;

/// <summary>
/// One small white-gold divinity orb. Its size is animated manually so no per-orb Tween or
/// particle system is needed; the layer owns its position on the orbit.
/// </summary>
internal partial class BelieverDivinityOrb : Node2D
{
    private const float BaseScale = 0.40f;
    private const float SpawnDuration = 0.28f;
    private const float RetireDuration = 0.22f;

    private Sprite2D? _sprite;
    private float _age;
    private float _retireAge;
    private float _phase;

    public bool Retiring { get; private set; }

    public void Initialize(Texture2D texture, float phase)
    {
        _phase = phase;
        _sprite = new Sprite2D
        {
            Name = "OrbSprite",
            Texture = texture,
            Centered = true,
        };
        AddChild(_sprite);
        Scale = Vector2.One * 0.15f;
        Modulate = new Color(1f, 1f, 1f, 0f);
    }

    public void Spawn()
    {
        Retiring = false;
        _age = 0f;
        _retireAge = 0f;
        Visible = true;
        Scale = Vector2.One * 0.15f;
        Modulate = new Color(1f, 1f, 1f, 0f);
    }

    public void Retire()
    {
        if (Retiring)
        {
            return;
        }
        Retiring = true;
        _retireAge = 0f;
    }

    public void UpdateVisual(float delta)
    {
        if (Retiring)
        {
            _retireAge += delta;
            var p = Mathf.Clamp(_retireAge / RetireDuration, 0f, 1f);
            var eased = p * p;
            Scale = Vector2.One * (BaseScale * (1f - 0.78f * eased));
            Modulate = new Color(1f, 0.88f, 0.55f, 1f - eased);
            if (p >= 1f)
            {
                QueueFree();
            }
            return;
        }

        _age += delta;
        var spawn = Mathf.Clamp(_age / SpawnDuration, 0f, 1f);
        var ease = 1f - (1f - spawn) * (1f - spawn);
        var pulse = 1f + 0.045f * Mathf.Sin(_age * 2.2f + _phase);
        Scale = Vector2.One * (BaseScale * (0.18f + 0.82f * ease) * pulse);
        var alpha = Mathf.Clamp(spawn, 0f, 1f);
        var flash = Mathf.Clamp(1f - spawn, 0f, 1f);
        Modulate = new Color(1f + 0.25f * flash, 1f + 0.18f * flash, 1f + 0.05f * flash, alpha);
    }
}

/// <summary>
/// Per-player, per-combat layer. It binds to the one real <see cref="BelieverDivinityResource"/>
/// (never a copied value) and reflows the orb count on every AmountChanged event.
/// The layer is a child of the Believer's visual root, so it follows the character scene and is
/// freed together with the combat creature.
/// </summary>
internal partial class BelieverDivinityOrbLayer : Node2D
{
    private const string EmbeddedOrb = "Believer.BelieverCode.Character.believer_orb.png";
    private const float OrbitCenterY = -306f;
    private const float OrbitRadiusX = 102f;
    private const float OrbitRadiusY = 28f;
    private const float OrbitRadiansPerSecond = 0.32f;
    private const float BobAmplitude = 2.0f;

    private static ImageTexture? _cachedTexture;

    private readonly List<BelieverDivinityOrb> _orbs = new();
    private Player? _player;
    private BelieverDivinityResource? _resource;
    private float _time;
    private int _bindAttempts;
    private bool _bindScheduled;
    private bool _suppressed;

    public int DesiredCount { get; private set; }

    public int ActiveCount
    {
        get
        {
            var count = 0;
            foreach (var orb in _orbs)
            {
                if (IsInstanceValid(orb) && !orb.Retiring)
                {
                    count++;
                }
            }
            return count;
        }
    }

    public void Initialize(Player player)
    {
        _player = player;
    }

    public void BindForTest(BelieverDivinityResource resource)
    {
        Bind(resource);
    }

    public override void _Ready()
    {
        if (_cachedTexture == null)
        {
            _cachedTexture = LoadOrbTexture();
        }
        if (_resource == null)
        {
            TryBind();
        }
    }

    private static ImageTexture? LoadOrbTexture()
    {
        using var stream = typeof(BelieverDivinityOrbLayer).Assembly.GetManifestResourceStream(EmbeddedOrb);
        if (stream == null)
        {
            MainFile.Logger.Error($"[BelieverOrbs] Missing embedded orb texture: {EmbeddedOrb}");
            return null;
        }
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var image = new Image();
        var error = image.LoadPngFromBuffer(memory.ToArray());
        if (error != Error.Ok)
        {
            MainFile.Logger.Error($"[BelieverOrbs] Orb texture decode failed: {error}");
            return null;
        }
        MainFile.Logger.Info($"[BelieverOrbs] orb texture loaded {image.GetWidth()}x{image.GetHeight()}");
        return ImageTexture.CreateFromImage(image);
    }

    private void TryBind()
    {
        if (_resource != null || _player == null)
        {
            return;
        }
        var resource = _player.GetResource<BelieverDivinityResource>();
        if (resource == null)
        {
            ScheduleBindRetry();
            return;
        }
        Bind(resource);
    }

    private void ScheduleBindRetry()
    {
        if (_bindScheduled || _bindAttempts >= 10 || !IsInsideTree())
        {
            return;
        }
        _bindScheduled = true;
        _bindAttempts++;
        var timer = GetTree().CreateTimer(0.1);
        timer.Timeout += () =>
        {
            _bindScheduled = false;
            if (IsInsideTree())
            {
                TryBind();
            }
        };
    }

    private void Bind(BelieverDivinityResource resource)
    {
        if (_resource == resource)
        {
            return;
        }
        if (_resource != null)
        {
            _resource.AmountChanged -= OnAmountChanged;
        }
        _resource = resource;
        _resource.AmountChanged += OnAmountChanged;
        MainFile.Logger.Info($"[BelieverOrbs] bound to real divinity resource: {resource.Id}");
        Sync(resource.Amount);
    }

    private void OnAmountChanged(int oldAmount, int newAmount)
    {
        if (_suppressed)
        {
            return;
        }
        MainFile.Logger.Info($"[BelieverOrbs] divinity {oldAmount} -> {newAmount}");
        Sync(newAmount);
    }

    private void Sync(int amount)
    {
        var target = Math.Clamp(amount, 0, BelieverDivinityResource.MaxAmount);
        if (_suppressed)
        {
            target = 0;
        }
        DesiredCount = target;

        for (var i = _orbs.Count - 1; i >= 0; i--)
        {
            if (!IsInstanceValid(_orbs[i]))
            {
                _orbs.RemoveAt(i);
            }
        }

        while (_orbs.Count < target)
        {
            var orb = new BelieverDivinityOrb { Name = $"BelieverDivinityOrb_{_orbs.Count}" };
            if (_cachedTexture != null)
            {
                orb.Initialize(_cachedTexture, _orbs.Count * 1.37f);
            }
            AddChild(orb);
            _orbs.Add(orb);
            orb.Spawn();
        }

        for (var i = 0; i < _orbs.Count; i++)
        {
            if (i < target)
            {
                _orbs[i].Spawn();
            }
            else
            {
                _orbs[i].Retire();
            }
        }

        MainFile.Logger.Info(
            $"[BelieverOrbs] sync amount={target} desired={DesiredCount} active={ActiveCount} totalNodes={_orbs.Count}");
    }

    public void BeginDeath()
    {
        if (_suppressed)
        {
            return;
        }
        _suppressed = true;
        DesiredCount = 0;
        foreach (var orb in _orbs)
        {
            if (IsInstanceValid(orb))
            {
                orb.Retire();
            }
        }
        MainFile.Logger.Info("[BelieverOrbs] owner death: retiring all orbs");
    }

    public void EndDeath()
    {
        if (!_suppressed)
        {
            return;
        }
        _suppressed = false;
        if (_resource != null)
        {
            Sync(_resource.Amount);
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _time += dt;

        for (var i = _orbs.Count - 1; i >= 0; i--)
        {
            if (!IsInstanceValid(_orbs[i]))
            {
                _orbs.RemoveAt(i);
            }
        }

        var activeIndex = 0;
        var activeCount = Math.Max(1, ActiveCount);
        foreach (var orb in _orbs)
        {
            if (!IsInstanceValid(orb))
            {
                continue;
            }
            if (orb.Retiring)
            {
                orb.UpdateVisual(dt);
                continue;
            }

            var angle = -Mathf.Pi / 2f
                + _time * OrbitRadiansPerSecond
                + activeIndex * Mathf.Tau / activeCount
                + (activeIndex % 2 == 0 ? -0.035f : 0.035f);
            var x = Mathf.Cos(angle) * OrbitRadiusX;
            var y = Mathf.Sin(angle) * OrbitRadiusY
                + Mathf.Sin(_time * 0.9f + activeIndex * 1.7f) * BobAmplitude
                + (activeIndex % 2 == 0 ? -4f : 4f);
            orb.Position = new Vector2(x, OrbitCenterY + y);
            orb.UpdateVisual(dt);
            activeIndex++;
        }
    }

    public override void _ExitTree()
    {
        if (_resource != null)
        {
            _resource.AmountChanged -= OnAmountChanged;
            _resource = null;
        }
        MainFile.Logger.Info("[BelieverOrbs] orb layer exited tree; resource unsubscribed");
    }
}

/// <summary>
/// Attaches the orb layer to the Believer's direct-texture visual root after NCreature._Ready.
/// It deliberately does not touch combat logic or the resource class.
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class BelieverDivinityOrbAttachPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        if (!__instance.Entity.IsPlayer || __instance.Entity.Player == null)
        {
            return;
        }
        if (__instance.Entity.Player.Character is not global::Believer.BelieverCode.Character.Believer)
        {
            return;
        }
        var effectLayer = __instance.Visuals.GetNodeOrNull<Node2D>("BelieverEffectLayer");
        if (effectLayer == null)
        {
            MainFile.Logger.Warn("[BelieverOrbs] BelieverEffectLayer not found; orbs not attached.");
            return;
        }
        if (effectLayer.GetNodeOrNull<BelieverDivinityOrbLayer>("BelieverDivinityOrbLayer") != null)
        {
            return;
        }
        var orbLayer = new BelieverDivinityOrbLayer { Name = "BelieverDivinityOrbLayer" };
        orbLayer.Initialize(__instance.Entity.Player);
        effectLayer.AddChild(orbLayer);
        MainFile.Logger.Info("[BelieverOrbs] attached to Believer visual root");
    }
}

/// <summary>Retire the orbs on player death, restore them after a revive.</summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class BelieverDivinityOrbDeathPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance, string trigger)
    {
        var effectLayer = __instance.Visuals?.GetNodeOrNull<Node2D>("BelieverEffectLayer");
        var layer = effectLayer?.GetNodeOrNull<BelieverDivinityOrbLayer>("BelieverDivinityOrbLayer");
        if (layer == null)
        {
            return;
        }
        if (trigger == "Dead")
        {
            layer.BeginDeath();
        }
        else if (trigger == "Revive")
        {
            layer.EndDeath();
        }
    }
}
