using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using Believer.BelieverCode.Powers;

namespace Believer.BelieverCode.Character;

/// <summary>
/// Low-cost 2D Omniscience presentation: levitation, a holy sun above/behind the character, and
/// white-gold lightning wrapping the body. No combat values are changed; this is a client-side layer.
/// Timeline: charge 0-0.38, fly 0.38-1.0, burst 1.0-1.4, sustain afterwards.
/// </summary>
internal partial class BelieverOmniscienceVfxLayer : Node2D
{
    private const float ChargeEnd = 0.38f;
    private const float FlyEnd = 1.00f;
    private const float BurstEnd = 1.40f;
    private const int AmbientBoltCount = 8;

    private Player? _player;
    private Node2D? _visualRoot;
    private Vector2 _baseVisualPosition;
    private bool _started;
    private bool _powerWasActive;
    private bool _flashActive;
    private float _elapsed;
    private float _sustainTime;
    private float _cardPulse;
    private float _shieldPulse;
    private int _orbCount;

    private OmniscienceSun _sun = null!;
    private OmniscienceGroundGlow _ground = null!;
    private OmniscienceBodyGlow _bodyGlow = null!;
    private OmniscienceShockwave _shockwave = null!;
    private OmniscienceLightningBurst _burst = null!;
    private readonly List<OmniscienceLightningBolt> _bolts = new();
    private readonly List<OmniscienceEnergyOrb> _orbs = new();
    private CanvasItemMaterial? _additive;
    private CanvasLayer? _flashLayer;
    private ColorRect? _flashRect;

    public void Initialize(Player? player, NCreatureVisuals visuals)
    {
        _player = player;
        _visualRoot = visuals as Node2D;
        if (_visualRoot != null) _baseVisualPosition = _visualRoot.Position;
    }

    public override void _Ready()
    {
        BuildNodes();
        SetEffectVisible(false);
        MainFile.Logger.Info("[OmniscienceVfx] lightning-sun layer ready");
    }

    private void BuildNodes()
    {
        _ground = new OmniscienceGroundGlow { Name = "OmniscienceGroundGlow", ZIndex = -1, Position = new Vector2(0, 30) };
        AddChild(_ground);

        _sun = new OmniscienceSun { Name = "OmniscienceSun", ZIndex = -2, Position = new Vector2(0, -450) };
        AddChild(_sun);

        _bodyGlow = new OmniscienceBodyGlow { Name = "OmniscienceBodyGlow", ZIndex = 1 };
        AddChild(_bodyGlow);

        for (var i = 0; i < 7; i++)
        {
            var orb = new OmniscienceEnergyOrb { Name = $"OmniscienceEnergyOrb_{i + 1}", ZIndex = 2 };
            orb.Initialize(i);
            AddChild(orb);
            _orbs.Add(orb);
        }

        for (var i = 0; i < AmbientBoltCount; i++)
        {
            var bolt = new OmniscienceLightningBolt { Name = $"OmniscienceLightning_{i + 1}", ZIndex = 3 };
            bolt.Configure(i, AmbientBoltCount);
            AddChild(bolt);
            _bolts.Add(bolt);
        }

        _burst = new OmniscienceLightningBurst { Name = "OmniscienceLightningBurst", ZIndex = 4 };
        AddChild(_burst);

        _shockwave = new OmniscienceShockwave { Name = "OmniscienceShockwave", ZIndex = 5 };
        AddChild(_shockwave);
    }

    private void SetEffectVisible(bool visible)
    {
        _sun.Visible = visible;
        _ground.Visible = visible;
        _bodyGlow.Visible = visible;
        _shockwave.Visible = visible;
        _burst.Visible = visible;
        foreach (var bolt in _bolts) bolt.Visible = visible;
        foreach (var orb in _orbs) orb.Visible = visible;
    }

    public void Begin(int divinityAmount)
    {
        if (_started) return;
        _started = true;
        _elapsed = 0f;
        _sustainTime = 0f;
        _cardPulse = 0f;
        _shieldPulse = 0f;
        _orbCount = Math.Clamp(divinityAmount, 0, 7);
        _powerWasActive = true;
        SetEffectVisible(true);

        _ground.Progress = 0f;
        _ground.Alpha = 0f;
        _sun.Progress = 0f;
        _sun.Alpha = 0f;
        _sun.Pulse = 1f;
        _bodyGlow.Alpha = 0f;
        _burst.Progress = 0f;
        _burst.Alpha = 0f;
        _shockwave.Progress = 1f;
        _shockwave.Alpha = 0f;
        foreach (var bolt in _bolts)
        {
            bolt.Alpha = 0f;
            bolt.Speed = 1f;
        }
        foreach (var orb in _orbs) orb.ResetForCharge();

        MainFile.Logger.Info($"[OmniscienceVfx] sun activation begin, divinity={_orbCount}");
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (_player?.Creature != null)
        {
            var active = _player.Creature.HasPower<OmnisciencePower>();
            if (active && !_powerWasActive) Begin(7);
            _powerWasActive = active;
            if (_started && !active && _elapsed > 0.25f) Stop();
        }

        if (!_started) return;
        _elapsed += dt;

        if (_elapsed < ChargeEnd) UpdateCharge(_elapsed / ChargeEnd);
        else if (_elapsed < FlyEnd) UpdateFly((_elapsed - ChargeEnd) / (FlyEnd - ChargeEnd));
        else if (_elapsed < BurstEnd) UpdateBurst((_elapsed - FlyEnd) / (BurstEnd - FlyEnd));
        else UpdateSustain(dt);

        UpdatePulses(dt);
        UpdateOrbs();
        UpdateFlash(dt);
    }

    private void UpdateOrbs()
    {
        var charge = _elapsed < ChargeEnd;
        foreach (var orb in _orbs) orb.UpdateVisual(_elapsed, charge);
    }

    private void UpdateCharge(float p)
    {
        _ground.Progress = 0.2f + 0.25f * p;
        _ground.Alpha = p * 0.75f;
        _ground.Pulse = 0.9f + 0.1f * Mathf.Sin(_elapsed * 14f);
        _bodyGlow.Alpha = p * 0.25f;
        _sun.Progress = 0.05f * p;
        _sun.Alpha = 0.05f * p;

        var activeOrbs = Mathf.Clamp(_orbCount, 0, 7);
        var activeBolts = Mathf.Clamp(2 + Mathf.RoundToInt(p * 2f), 2, 4);
        for (var i = 0; i < _orbs.Count; i++)
        {
            _orbs[i].Visible = i < activeOrbs;
            _orbs[i].Progress = p;
            _orbs[i].Alpha = p;
        }
        for (var i = 0; i < _bolts.Count; i++)
        {
            _bolts[i].Visible = i < activeBolts;
            _bolts[i].Alpha = p * 0.18f;
            _bolts[i].Speed = 0.8f;
        }
    }

    private void UpdateFly(float p)
    {
        var ease = p * p * (3f - 2f * p);
        _ground.Progress = 0.45f + 0.35f * ease;
        _ground.Alpha = 0.75f + 0.2f * ease;
        _ground.Pulse = 1f + 0.08f * Mathf.Sin(_elapsed * 10f);
        _bodyGlow.Alpha = 0.25f + 0.55f * ease;
        _sun.Progress = 0.05f + 0.95f * ease;
        _sun.Alpha = 0.05f + 0.95f * ease;
        _sun.Pulse = 0.9f + 0.25f * ease;

        for (var i = 0; i < _orbs.Count; i++)
        {
            _orbs[i].Visible = i < _orbCount;
            _orbs[i].Progress = 1f;
            _orbs[i].Alpha = 1f - ease * 0.35f;
        }
        for (var i = 0; i < _bolts.Count; i++)
        {
            _bolts[i].Visible = true;
            _bolts[i].Alpha = 0.18f + 0.55f * ease;
            _bolts[i].Speed = 1f + ease * 0.8f;
        }
        if (_visualRoot != null)
        {
            var lift = -105f * ease;
            _visualRoot.Position = _baseVisualPosition + new Vector2(0, lift);
        }
    }

    private void UpdateBurst(float p)
    {
        var ease = p * p * (3f - 2f * p);
        _ground.Progress = 0.8f + 0.25f * ease;
        _ground.Alpha = 1f;
        _ground.Pulse = 1f + 0.12f * Mathf.Sin(_elapsed * 25f);
        _bodyGlow.Alpha = 1f;
        _sun.Progress = 1f;
        _sun.Alpha = 1f;
        _sun.Pulse = 1f + 0.18f * Mathf.Sin(_elapsed * 24f);
        _burst.Progress = p;
        _burst.Alpha = 1f - p * 0.45f;
        _shockwave.Progress = p;
        _shockwave.Alpha = 1f - p * 0.85f;
        _shockwave.RadiusBoost = 0.35f;

        for (var i = 0; i < _bolts.Count; i++)
        {
            _bolts[i].Visible = true;
            _bolts[i].Alpha = 0.75f + 0.25f * Mathf.Sin(_elapsed * 20f + i);
            _bolts[i].Speed = 1.8f;
        }
        foreach (var orb in _orbs) orb.Alpha = 0f;
        if (_visualRoot != null)
        {
            _visualRoot.Position = _baseVisualPosition + new Vector2(0, -105f - 4f * Mathf.Sin(p * Mathf.Pi));
        }
        if (p < 0.28f) TriggerFlash(0.8f * (1f - p / 0.28f));
    }

    private void UpdateSustain(float dt)
    {
        _sustainTime += dt;
        var bob = Mathf.Sin(_sustainTime * 1.15f) * 3f;
        _ground.Progress = 1f + 0.04f * Mathf.Sin(_sustainTime * 1.1f);
        _ground.Alpha = 0.85f;
        _ground.Pulse = 1f + 0.04f * Mathf.Sin(_sustainTime * 2.1f);
        _bodyGlow.Alpha = 0.75f;
        _sun.Progress = 1f;
        _sun.Alpha = 0.88f + 0.1f * Mathf.Sin(_sustainTime * 1.2f);
        _sun.Pulse = 1f + 0.06f * Mathf.Sin(_sustainTime * 1.6f);
        _sun.SunRotation = _sustainTime * 0.06f;
        _burst.Progress = 1f;
        _burst.Alpha = 0f;
        _shockwave.Progress = 1f;
        _shockwave.Alpha = 0f;
        foreach (var orb in _orbs) orb.Alpha = 0f;

        for (var i = 0; i < _bolts.Count; i++)
        {
            _bolts[i].Visible = true;
            _bolts[i].Alpha = 0.22f + 0.14f * Mathf.Sin(_sustainTime * 2.4f + i * 0.9f);
            _bolts[i].Speed = 0.45f;
        }
        if (_visualRoot != null)
        {
            _visualRoot.Position = _baseVisualPosition + new Vector2(0, -100f + bob);
        }
    }

    private void UpdatePulses(float dt)
    {
        if (_cardPulse > 0f)
        {
            _cardPulse = Mathf.Max(0f, _cardPulse - dt * 2.4f);
            _shockwave.Progress = 1f - _cardPulse;
            _shockwave.Alpha = _cardPulse;
            _shockwave.RadiusBoost = 0.55f;
            _sun.Pulse = 1f + _cardPulse * 0.22f;
            foreach (var bolt in _bolts) bolt.Alpha = Mathf.Max(bolt.Alpha, _cardPulse * 0.8f);
        }
        else if (_shieldPulse > 0f)
        {
            _shieldPulse = Mathf.Max(0f, _shieldPulse - dt * 3.2f);
            _shockwave.Progress = 1f - _shieldPulse;
            _shockwave.Alpha = _shieldPulse * 0.65f;
            _shockwave.RadiusBoost = 0.25f;
            _sun.Pulse = 1f + _shieldPulse * 0.12f;
        }
    }

    public void PlayCardBurst()
    {
        if (!_started) return;
        _cardPulse = 1f;
        _shieldPulse = 0f;
        _shockwave.Progress = 0f;
        _shockwave.Alpha = 0.8f;
        _shockwave.RadiusBoost = 0.55f;
        TriggerFlash(0.14f);
        MainFile.Logger.Info("[OmniscienceVfx] card lightning trail / sun pulse");
    }

    public void ShieldFeedback()
    {
        if (!_started) return;
        _shieldPulse = 1f;
        _cardPulse = 0f;
        _shockwave.Progress = 0f;
        _shockwave.Alpha = 0.65f;
        _shockwave.RadiusBoost = 0.2f;
        TriggerFlash(0.08f);
        MainFile.Logger.Info("[OmniscienceVfx] sacred shield feedback");
    }

    private void TriggerFlash(float alpha)
    {
        if (_flashActive) return;
        if (_flashLayer == null)
        {
            _flashLayer = new CanvasLayer { Name = "OmniscienceScreenFlashLayer", Layer = 100 };
            GetTree().Root.AddChild(_flashLayer);
            _flashRect = new ColorRect
            {
                Name = "OmniscienceScreenFlash",
                Color = new Color(1f, 1f, 1f, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _flashRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _flashLayer.AddChild(_flashRect);
        }
        _flashActive = true;
        _flashRect!.Color = new Color(1f, 1f, 1f, alpha);
    }

    private void UpdateFlash(float dt)
    {
        if (!_flashActive || _flashRect == null) return;
        var color = _flashRect.Color;
        color.A = Mathf.Max(0f, color.A - dt * 4.5f);
        _flashRect.Color = color;
        if (color.A <= 0f)
        {
            _flashActive = false;
            _flashRect.QueueFree();
            _flashRect = null;
            if (_flashLayer != null)
            {
                _flashLayer.QueueFree();
                _flashLayer = null;
            }
        }
    }

    private void Stop()
    {
        if (!_started) return;
        _started = false;
        _elapsed = 0f;
        if (_visualRoot != null) _visualRoot.Position = _baseVisualPosition;
        SetEffectVisible(false);
        if (_flashRect != null)
        {
            _flashRect.QueueFree();
            _flashRect = null;
        }
        if (_flashLayer != null)
        {
            _flashLayer.QueueFree();
            _flashLayer = null;
        }
        MainFile.Logger.Info("[OmniscienceVfx] sun/lightning form ended");
    }

    public override void _ExitTree()
    {
        BelieverOmniscienceVfx.Unregister(_player, this);
        if (_flashLayer != null) _flashLayer.QueueFree();
    }
}

internal static class BelieverOmniscienceVfx
{
    private static readonly Dictionary<Player, BelieverOmniscienceVfxLayer> Layers = new();

    public static void Register(Player player, BelieverOmniscienceVfxLayer layer) => Layers[player] = layer;
    public static void Unregister(Player? player, BelieverOmniscienceVfxLayer layer)
    {
        if (player != null && Layers.TryGetValue(player, out var current) && current == layer) Layers.Remove(player);
    }

    public static void Begin(Player? player, int divinityAmount)
    {
        if (player != null && Layers.TryGetValue(player, out var layer)) layer.Begin(divinityAmount);
    }

    public static void CardPlayed(Player? player)
    {
        if (player != null && Layers.TryGetValue(player, out var layer)) layer.PlayCardBurst();
    }

    public static void ShieldFeedback(Player? player)
    {
        if (player != null && Layers.TryGetValue(player, out var layer)) layer.ShieldFeedback();
    }
}

/// <summary>
/// The new core visual: a radiant holy sun hovering above and behind the character. It is drawn
/// procedurally (no external texture dependency) with a white core, gold corona and rotating rays.
/// </summary>
internal partial class OmniscienceSun : Node2D
{
    public float Progress { get; set; }
    public float Alpha { get; set; }
    public float Pulse { get; set; } = 1f;
    public float SunRotation { get; set; }
    private float _time;

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        var p = Mathf.Clamp(Progress, 0f, 1f);
        var scale = (0.45f + 0.55f * p) * Pulse;
        var core = new Color(1f, 1f, 0.95f, Alpha);
        var gold = new Color(1f, 0.79f, 0.32f, Alpha);
        var white = new Color(1f, 1f, 1f, Alpha);

        DrawCircle(Vector2.Zero, 230f * scale, new Color(1f, 0.82f, 0.45f, Alpha * 0.07f));
        DrawCircle(Vector2.Zero, 170f * scale, new Color(1f, 0.89f, 0.56f, Alpha * 0.13f));
        DrawCircle(Vector2.Zero, 118f * scale, new Color(1f, 0.9f, 0.58f, Alpha * 0.28f));
        DrawCircle(Vector2.Zero, 82f * scale, new Color(1f, 0.96f, 0.76f, Alpha * 0.65f));
        DrawCircle(Vector2.Zero, 54f * scale, new Color(1f, 1f, 1f, Alpha * 0.98f));
        DrawArc(Vector2.Zero, 118f * scale, 0f, Mathf.Tau, 96, core, 2.5f, true);
        DrawArc(Vector2.Zero, 82f * scale, 0f, Mathf.Tau, 80, gold, 1.6f, true);

        for (var i = 0; i < 16; i++)
        {
            var angle = i * Mathf.Tau / 16f + SunRotation;
            var inner = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 105f * scale;
            var outer = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (175f + 18f * Mathf.Sin(_time * 1.4f + i)) * scale;
            DrawLine(inner, outer, new Color(1f, 0.91f, 0.58f, Alpha * 0.6f), 3.5f, true);
            DrawLine(inner, outer, new Color(1f, 1f, 1f, Alpha * 0.78f), 1.1f, true);
        }
        for (var i = 0; i < 24; i++)
        {
            var angle = i * Mathf.Tau / 24f - SunRotation * 0.6f;
            var point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 140f * scale;
            DrawCircle(point, 2.1f * scale, white);
        }
    }
}

/// <summary>Ground glow under the character during the initial charge.</summary>
internal partial class OmniscienceGroundGlow : Node2D
{
    public float Progress { get; set; }
    public float Alpha { get; set; }
    public float Pulse { get; set; } = 1f;
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        var p = Mathf.Max(0.01f, Progress);
        DrawCircle(Vector2.Zero, 120f * p * Pulse, new Color(1f, 0.86f, 0.55f, Alpha * 0.10f));
        DrawCircle(Vector2.Zero, 78f * p * Pulse, new Color(1f, 0.94f, 0.75f, Alpha * 0.16f));
        DrawArc(Vector2.Zero, 105f * p, 0, Mathf.Tau, 80, new Color(1f, 0.92f, 0.68f, Alpha * 0.5f), 2f, true);
    }
}

/// <summary>Soft white-gold body aura; the actual lightning is separate bolts.</summary>
internal partial class OmniscienceBodyGlow : Node2D
{
    public float Alpha { get; set; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        DrawCircle(new Vector2(0, -210f), 150f, new Color(1f, 0.9f, 0.58f, Alpha * 0.08f));
        DrawCircle(new Vector2(0, -210f), 100f, new Color(1f, 0.98f, 0.82f, Alpha * 0.12f));
    }
}

/// <summary>One jagged white-gold lightning bolt wrapping the body.</summary>
internal partial class OmniscienceLightningBolt : Node2D
{
    private int _index;
    private int _total;
    private float _time;
    public float Alpha { get; set; }
    public float Speed { get; set; } = 1f;

    public void Configure(int index, int total)
    {
        _index = index;
        _total = total;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta * Speed;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        var angle = _index * Mathf.Tau / _total + 0.22f;
        var center = new Vector2(0, -220f);
        var inner = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 48f;
        var outer = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (150f + 45f * Mathf.Sin(_time * 1.3f + _index));
        var points = new Vector2[7];
        for (var j = 0; j < points.Length; j++)
        {
            var t = j / (points.Length - 1f);
            var basePoint = inner.Lerp(outer, t);
            var perpendicular = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
            var jitter = Mathf.Sin(_time * (3.2f + _index * 0.17f) + j * 2.1f) * (6f + 8f * t);
            points[j] = center + basePoint + perpendicular * jitter;
        }
        DrawPolyline(points, new Color(1f, 0.9f, 0.55f, Alpha * 0.55f), 9f, true);
        DrawPolyline(points, new Color(1f, 1f, 0.9f, Alpha * 0.9f), 3.5f, true);
        DrawPolyline(points, new Color(1f, 1f, 1f, Alpha), 1.15f, true);
    }
}

/// <summary>Outward lightning burst at the moment the form peaks.</summary>
internal partial class OmniscienceLightningBurst : Node2D
{
    public float Progress { get; set; }
    public float Alpha { get; set; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        var p = Mathf.Clamp(Progress, 0f, 1f);
        var center = new Vector2(0, -240f);
        for (var i = 0; i < 14; i++)
        {
            var angle = i * Mathf.Tau / 14f + 0.11f;
            var radius = 120f + p * 520f;
            var points = new Vector2[5];
            for (var j = 0; j < points.Length; j++)
            {
                var t = j / (points.Length - 1f);
                var perp = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
                var jitter = Mathf.Sin(i * 7.1f + j * 4.3f + p * 5f) * 22f * t;
                points[j] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * t) + perp * jitter;
            }
            DrawPolyline(points, new Color(1f, 0.82f, 0.4f, Alpha * 0.28f), 8f, true);
            DrawPolyline(points, new Color(1f, 1f, 0.9f, Alpha * 0.8f), 2.2f, true);
        }
    }
}

/// <summary>Expanding divine shockwave at the feet/body.</summary>
internal partial class OmniscienceShockwave : Node2D
{
    public float Progress { get; set; }
    public float Alpha { get; set; }
    public float RadiusBoost { get; set; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        var radius = 60f + 380f * Mathf.Clamp(Progress, 0f, 1f) + 110f * RadiusBoost;
        DrawArc(new Vector2(0, -240f), radius, 0, Mathf.Tau, 120, new Color(1f, 0.98f, 0.85f, Alpha), 3f, true);
        DrawArc(new Vector2(0, -240f), radius * 0.8f, 0, Mathf.Tau, 96, new Color(1f, 0.8f, 0.4f, Alpha * 0.5f), 2f, true);
    }
}

/// <summary>Small divinity orb used only for the initial upward float.</summary>
internal partial class OmniscienceEnergyOrb : Node2D
{
    private int _index;
    private float _phase;
    public float Progress { get; set; }
    public float Alpha { get; set; }

    public void Initialize(int index)
    {
        _index = index;
        _phase = index * Mathf.Tau / 7f + 0.35f;
    }

    public void ResetForCharge()
    {
        Progress = 0f;
        Alpha = 0f;
        Position = new Vector2(Mathf.Cos(_phase) * 110f, 20f);
    }

    public void UpdateVisual(float elapsed, bool charge)
    {
        if (Alpha <= 0.01f)
        {
            return;
        }
        if (charge)
        {
            var p = Mathf.Clamp(Progress, 0f, 1f);
            var ease = 1f - (1f - p) * (1f - p);
            Position = new Vector2(
                Mathf.Cos(_phase) * (110f - 40f * ease),
                20f - 235f * ease + Mathf.Sin(elapsed * 2.2f + _index) * 3f);
        }
        else
        {
            Position = new Vector2(
                Mathf.Cos(_phase + elapsed * 0.7f) * 78f,
                -220f + Mathf.Sin(_phase + elapsed * 0.9f) * 24f);
        }
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        DrawCircle(Vector2.Zero, 27f, new Color(1f, 0.82f, 0.36f, Alpha * 0.08f));
        DrawCircle(Vector2.Zero, 15f, new Color(1f, 0.92f, 0.62f, Alpha * 0.25f));
        DrawCircle(Vector2.Zero, 8f, new Color(1f, 0.98f, 0.86f, Alpha * 0.75f));
        DrawCircle(Vector2.Zero, 4.2f, new Color(1f, 1f, 1f, Alpha));
    }
}

/// <summary>
/// Legacy M10.8 lightning-sun attachment is intentionally disabled; M10.8.2's
/// BelieverOmniscienceDomainVfx owns the active Omniscience presentation.
/// </summary>
internal static class BelieverOmniscienceVfxAttachPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        if (!__instance.Entity.IsPlayer || __instance.Entity.Player == null) return;
        if (__instance.Entity.Player.Character is not global::Believer.BelieverCode.Character.Believer) return;
        var effectLayer = __instance.Visuals.GetNodeOrNull<Node2D>("BelieverEffectLayer");
        if (effectLayer == null) return;
        if (effectLayer.GetNodeOrNull<BelieverOmniscienceVfxLayer>("BelieverOmniscienceVfxLayer") != null) return;
        var layer = new BelieverOmniscienceVfxLayer { Name = "BelieverOmniscienceVfxLayer" };
        layer.Initialize(__instance.Entity.Player, __instance.Visuals);
        effectLayer.AddChild(layer);
        BelieverOmniscienceVfx.Register(__instance.Entity.Player, layer);
        MainFile.Logger.Info("[OmniscienceVfx] lightning-sun layer attached");
    }
}
