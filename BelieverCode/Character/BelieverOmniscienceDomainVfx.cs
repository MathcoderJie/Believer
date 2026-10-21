using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using Believer.BelieverCode.Powers;

namespace Believer.BelieverCode.Character;

/// <summary>
/// New Omniscience presentation: an ascension into a seraphic sacred domain.
///
/// Charge (0-0.36s): ground rune, divine orbs rise, aura appears.
/// Ascend (0.36-0.96s): the Believer levitates, a giant rose-window halo unfolds behind,
///   two wings of light spread, a sacred pillar descends.
/// Burst (0.96-1.40s): halo/wings fully open, white-gold flash, radial shockwave, feather burst.
/// Sustain: hovering body, pulsing halo/wings, orbiting light feathers, card-play wing pulse.
///
/// This layer is client-side only. It does not change combat state, costs or duration.
/// </summary>
internal partial class BelieverOmniscienceDomainVfxLayer : Node2D
{
    private const float ChargeEnd = 0.36f;
    private const float FlyEnd = 0.96f;
    private const float BurstEnd = 1.40f;

    private Player? _player;
    private NCreatureVisuals? _visualRoot;
    private Vector2 _baseVisualPosition;
    private bool _started;
    private bool _ending;
    private bool _powerWasActive;
    private bool _flashActive;
    private float _elapsed;
    private float _endElapsed;
    private Vector2 _endStartPosition;
    private float _sustainTime;
    private float _cardPulse;
    private float _shieldPulse;
    private int _orbCount;

    private SeraphicHalo _halo = null!;
    private SeraphicWings _wings = null!;
    private SeraphicPillar _pillar = null!;
    private SeraphicGround _ground = null!;
    private SeraphicAura _aura = null!;
    private SeraphicFeathers _feathers = null!;
    private SeraphicShockwave _shock = null!;
    private readonly List<SeraphicOrb> _orbs = new();
    private CanvasLayer? _flashLayer;
    private ColorRect? _flashRect;

    public void Initialize(Player? player, NCreatureVisuals visuals)
    {
        _player = player;
        _visualRoot = visuals;
        if (_visualRoot != null) _baseVisualPosition = _visualRoot.Position;
    }

    public override void _Ready()
    {
        BuildNodes();
        SetEffectVisible(false);
        MainFile.Logger.Info("[OmniscienceDomain] seraphic domain layer ready");
    }

    private void BuildNodes()
    {
        _ground = new SeraphicGround { Name = "SeraphicGround", ZIndex = -1, Position = new Vector2(0, 24) };
        AddChild(_ground);

        _halo = new SeraphicHalo { Name = "SeraphicHalo", ZIndex = -2, Position = new Vector2(0, -560) };
        AddChild(_halo);

        _wings = new SeraphicWings { Name = "SeraphicWings", ZIndex = -1, Position = new Vector2(0, -235) };
        AddChild(_wings);

        _aura = new SeraphicAura { Name = "SeraphicAura", ZIndex = 1 };
        AddChild(_aura);

        _pillar = new SeraphicPillar { Name = "SeraphicPillar", ZIndex = 0 };
        AddChild(_pillar);

        for (var i = 0; i < 7; i++)
        {
            var orb = new SeraphicOrb { Name = $"SeraphicOrb_{i + 1}", ZIndex = 2 };
            orb.Initialize(i);
            AddChild(orb);
            _orbs.Add(orb);
        }

        _feathers = new SeraphicFeathers { Name = "SeraphicFeathers", ZIndex = 3 };
        AddChild(_feathers);

        _shock = new SeraphicShockwave { Name = "SeraphicShockwave", ZIndex = 4 };
        AddChild(_shock);
    }

    private void SetEffectVisible(bool visible)
    {
        _halo.Visible = visible;
        _wings.Visible = visible;
        _pillar.Visible = visible;
        _ground.Visible = visible;
        _aura.Visible = visible;
        _feathers.Visible = visible;
        _shock.Visible = visible;
        foreach (var orb in _orbs) orb.Visible = visible;
    }

    public void Begin(int divinityAmount)
    {
        if (_started) return;
        _started = true;
        _ending = false;
        Modulate = Colors.White;
        _elapsed = 0f;
        _sustainTime = 0f;
        _cardPulse = 0f;
        _shieldPulse = 0f;
        _orbCount = Math.Clamp(divinityAmount, 0, 7);
        _powerWasActive = true;
        SetEffectVisible(true);
        _halo.Progress = 0f;
        _halo.Alpha = 0f;
        _halo.Pulse = 1f;
        _halo.HaloRotation = 0f;
        _wings.Progress = 0f;
        _wings.Alpha = 0f;
        _pillar.Progress = 0f;
        _pillar.Alpha = 0f;
        _ground.Progress = 0f;
        _ground.Alpha = 0f;
        _aura.Alpha = 0f;
        _feathers.Progress = 0f;
        _feathers.Alpha = 0f;
        _shock.Progress = 0f;
        _shock.Alpha = 0f;
        foreach (var orb in _orbs) orb.ResetForCharge();
        MainFile.Logger.Info($"[OmniscienceDomain] activation begin, orbs={_orbCount}");
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (_player?.Creature != null)
        {
            var active = _player.Creature.HasPower<OmnisciencePower>();
            if (active && !_powerWasActive) Begin(7);
            _powerWasActive = active;
            if (_started && !active && _elapsed > 0.25f) BeginEnding();
        }
        if (!_started) return;
        if (_ending)
        {
            UpdateEnding(dt);
            UpdateFlash(dt);
            return;
        }

        _elapsed += dt;
        if (_elapsed < ChargeEnd) UpdateCharge(_elapsed / ChargeEnd);
        else if (_elapsed < FlyEnd) UpdateFly((_elapsed - ChargeEnd) / (FlyEnd - ChargeEnd));
        else if (_elapsed < BurstEnd) UpdateBurst((_elapsed - FlyEnd) / (BurstEnd - FlyEnd));
        else UpdateSustain(dt);

        UpdateOrbs();
        UpdatePulses(dt);
        UpdateFlash(dt);
    }

    private void UpdateCharge(float p)
    {
        _ground.Progress = 0.2f + 0.3f * p;
        _ground.Alpha = p * 0.8f;
        _ground.Pulse = 0.85f + 0.15f * Mathf.Sin(_elapsed * 15f);
        _halo.Progress = 0.04f + 0.16f * p;
        _halo.Alpha = 0.15f * p;
        _wings.Progress = 0.02f;
        _wings.Alpha = 0.06f * p;
        _aura.Alpha = 0.25f * p;
        _pillar.Alpha = 0.12f * p;
        _pillar.Progress = 0.1f * p;
        for (var i = 0; i < _orbs.Count; i++)
        {
            _orbs[i].Progress = p;
            _orbs[i].Alpha = p;
            _orbs[i].Visible = i < _orbCount;
        }
    }

    private void UpdateFly(float p)
    {
        var ease = p * p * (3f - 2f * p);
        _ground.Progress = 0.5f + 0.35f * ease;
        _ground.Alpha = 0.8f;
        _ground.Pulse = 1f + 0.06f * Mathf.Sin(_elapsed * 11f);
        _halo.Progress = 0.2f + 0.8f * ease;
        _halo.Alpha = 0.15f + 0.85f * ease;
        _halo.Pulse = 0.9f + 0.2f * ease;
        _wings.Progress = 0.02f + 0.98f * ease;
        _wings.Alpha = 0.06f + 0.9f * ease;
        _aura.Alpha = 0.25f + 0.55f * ease;
        _pillar.Progress = 0.1f + 0.9f * ease;
        _pillar.Alpha = 0.12f + 0.6f * ease;
        foreach (var orb in _orbs)
        {
            orb.Progress = 1f;
            orb.Alpha = 1f - ease * 0.55f;
        }
        if (_visualRoot != null)
        {
            _visualRoot.Position = _baseVisualPosition + new Vector2(0, -215f * ease);
        }
    }

    private void UpdateBurst(float p)
    {
        var ease = p * p * (3f - 2f * p);
        _ground.Progress = 0.85f + 0.2f * ease;
        _ground.Alpha = 1f;
        _halo.Progress = 1f;
        _halo.Alpha = 1f;
        _halo.Pulse = 1f + 0.22f * Mathf.Sin(_elapsed * 25f);
        _wings.Progress = 1f;
        _wings.Alpha = 1f;
        _aura.Alpha = 1f;
        _pillar.Progress = 1f;
        _pillar.Alpha = 1f;
        _shock.Progress = p;
        _shock.Alpha = 1f - p * 0.85f;
        _shock.RadiusBoost = 0.35f;
        _feathers.Progress = p;
        _feathers.Alpha = 1f - p * 0.35f;
        foreach (var orb in _orbs) orb.Alpha = 0f;
        if (_visualRoot != null)
        {
            _visualRoot.Position = _baseVisualPosition + new Vector2(0, -215f - 5f * Mathf.Sin(p * Mathf.Pi));
        }
        if (p < 0.24f) TriggerFlash(0.75f * (1f - p / 0.24f));
    }

    private void UpdateSustain(float dt)
    {
        _sustainTime += dt;
        var bob = Mathf.Sin(_sustainTime * 1.1f) * 3f;
        _ground.Progress = 1f + 0.04f * Mathf.Sin(_sustainTime * 1.3f);
        _ground.Alpha = 0.9f;
        _ground.Pulse = 1f + 0.03f * Mathf.Sin(_sustainTime * 2.2f);
        _halo.Progress = 1f;
        _halo.Alpha = 0.88f + 0.08f * Mathf.Sin(_sustainTime * 1.2f);
        _halo.Pulse = 1f + 0.05f * Mathf.Sin(_sustainTime * 1.7f);
        _halo.HaloRotation = _sustainTime * 0.08f;
        _wings.Progress = 1f;
        _wings.Alpha = 0.82f + 0.12f * Mathf.Sin(_sustainTime * 1.35f);
        _aura.Alpha = 0.78f + 0.1f * Mathf.Sin(_sustainTime * 1.8f);
        _pillar.Progress = 1f;
        _pillar.Alpha = 0.58f + 0.1f * Mathf.Sin(_sustainTime * 1.4f);
        _shock.Progress = 1f;
        _shock.Alpha = 0f;
        _feathers.Progress = 1f;
        _feathers.Alpha = 0.3f + 0.08f * Mathf.Sin(_sustainTime * 1.6f);
        foreach (var orb in _orbs) orb.Alpha = 0f;
        if (_visualRoot != null)
        {
            _visualRoot.Position = _baseVisualPosition + new Vector2(0, -210f + bob);
        }
    }

    private void UpdateOrbs()
    {
        var charge = _elapsed < ChargeEnd;
        foreach (var orb in _orbs) orb.UpdateVisual(_elapsed, charge);
    }

    private void UpdatePulses(float dt)
    {
        if (_cardPulse > 0f)
        {
            _cardPulse = Mathf.Max(0f, _cardPulse - dt * 2.4f);
            _shock.Progress = 1f - _cardPulse;
            _shock.Alpha = _cardPulse;
            _shock.RadiusBoost = 0.55f;
            _halo.Pulse = 1f + _cardPulse * 0.2f;
            _feathers.Progress = 1f;
            _feathers.Alpha = Mathf.Max(_feathers.Alpha, _cardPulse * 0.75f);
        }
        else if (_shieldPulse > 0f)
        {
            _shieldPulse = Mathf.Max(0f, _shieldPulse - dt * 3.2f);
            _shock.Progress = 1f - _shieldPulse;
            _shock.Alpha = _shieldPulse * 0.6f;
            _shock.RadiusBoost = 0.2f;
        }
    }

    public void PlayCardBurst()
    {
        if (!_started) return;
        _cardPulse = 1f;
        _shieldPulse = 0f;
        _shock.Progress = 0f;
        _shock.Alpha = 0.8f;
        _shock.RadiusBoost = 0.55f;
        MainFile.Logger.Info("[OmniscienceDomain] card-play wing pulse (no flash)");
    }

    public void ShieldFeedback()
    {
        if (!_started) return;
        _shieldPulse = 1f;
        _cardPulse = 0f;
        _shock.Progress = 0f;
        _shock.Alpha = 0.6f;
        TriggerFlash(0.07f);
        MainFile.Logger.Info("[OmniscienceDomain] sacred shield feedback");
    }

    private void TriggerFlash(float alpha)
    {
        if (_flashActive) return;
        if (_flashLayer == null)
        {
            _flashLayer = new CanvasLayer { Name = "OmniscienceDomainFlashLayer", Layer = 100 };
            GetTree().Root.AddChild(_flashLayer);
            _flashRect = new ColorRect
            {
                Name = "OmniscienceDomainFlash",
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
        var c = _flashRect.Color;
        c.A = Mathf.Max(0f, c.A - dt * 4.5f);
        _flashRect.Color = c;
        if (c.A <= 0f)
        {
            _flashActive = false;
            _flashRect.QueueFree();
            _flashRect = null;
            if (_flashLayer != null) { _flashLayer.QueueFree(); _flashLayer = null; }
        }
    }

    private void BeginEnding()
    {
        if (_ending) return;
        _ending = true;
        _endElapsed = 0f;
        _endStartPosition = _visualRoot?.Position ?? _baseVisualPosition;
        _shock.Alpha = 0f;
        _cardPulse = 0f;
        _shieldPulse = 0f;
        MainFile.Logger.Info("[OmniscienceDomain] form ending: descending and fading");
    }

    private void UpdateEnding(float dt)
    {
        _endElapsed += dt;
        var p = Mathf.Clamp(_endElapsed / 0.55f, 0f, 1f);
        var ease = p * p * (3f - 2f * p);
        if (_visualRoot != null)
        {
            _visualRoot.Position = _endStartPosition.Lerp(_baseVisualPosition, ease);
        }
        Modulate = new Color(1f, 1f, 1f, 1f - ease);
        if (p >= 1f) Stop();
    }

    private void Stop()
    {
        if (!_started) return;
        _started = false;
        _ending = false;
        _elapsed = 0f;
        Modulate = Colors.White;
        if (_visualRoot != null) _visualRoot.Position = _baseVisualPosition;
        SetEffectVisible(false);
        if (_flashRect != null) { _flashRect.QueueFree(); _flashRect = null; }
        if (_flashLayer != null) { _flashLayer.QueueFree(); _flashLayer = null; }
        MainFile.Logger.Info("[OmniscienceDomain] form ended");
    }

    public override void _ExitTree()
    {
        BelieverOmniscienceDomainVfx.Unregister(_player, this);
        if (_flashLayer != null) _flashLayer.QueueFree();
    }
}

internal static class BelieverOmniscienceDomainVfx
{
    private static readonly Dictionary<Player, BelieverOmniscienceDomainVfxLayer> Layers = new();

    public static void Register(Player player, BelieverOmniscienceDomainVfxLayer layer) => Layers[player] = layer;
    public static void Unregister(Player? player, BelieverOmniscienceDomainVfxLayer layer)
    {
        if (player != null && Layers.TryGetValue(player, out var current) && current == layer) Layers.Remove(player);
    }
    public static void Begin(Player? player, int divinity) { if (player != null && Layers.TryGetValue(player, out var l)) l.Begin(divinity); }
    public static void CardPlayed(Player? player) { if (player != null && Layers.TryGetValue(player, out var l)) l.PlayCardBurst(); }
    public static void ShieldFeedback(Player? player) { if (player != null && Layers.TryGetValue(player, out var l)) l.ShieldFeedback(); }
}

internal partial class SeraphicHalo : Node2D
{
    public float Progress { get; set; }
    public float Alpha { get; set; }
    public float Pulse { get; set; } = 1f;
    public float HaloRotation { get; set; }
    private float _t;

    public override void _Process(double delta) { _t += (float)delta; QueueRedraw(); }

    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        var p = Mathf.Clamp(Progress, 0f, 1f);
        var s = (0.35f + 0.65f * p) * Pulse;
        var gold = new Color(1f, 0.83f, 0.48f, Alpha * 0.5f);
        var ivory = new Color(1f, 0.97f, 0.82f, Alpha * 0.75f);
        DrawCircle(Vector2.Zero, 280f * s, new Color(1f, 0.85f, 0.52f, Alpha * 0.07f));
        DrawCircle(Vector2.Zero, 220f * s, new Color(1f, 0.92f, 0.65f, Alpha * 0.12f));
        DrawCircle(Vector2.Zero, 155f * s, new Color(1f, 0.98f, 0.86f, Alpha * 0.2f));
        DrawArc(Vector2.Zero, 220f * s, 0, Mathf.Tau, 120, ivory, 3f, true);
        DrawArc(Vector2.Zero, 155f * s, 0, Mathf.Tau, 96, gold, 2f, true);
        DrawArc(Vector2.Zero, 110f * s, 0, Mathf.Tau, 96, ivory, 1.5f, true);
        for (var i = 0; i < 12; i++)
        {
            var a = i * Mathf.Tau / 12f + HaloRotation;
            var inner = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 120f * s;
            var outer = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (190f + 14f * Mathf.Sin(_t * 1.3f + i)) * s;
            DrawLine(inner, outer, new Color(1f, 0.97f, 0.8f, Alpha * 0.62f), 3f, true);
        }
        for (var i = 0; i < 24; i++)
        {
            var a = i * Mathf.Tau / 24f - HaloRotation * 0.7f;
            var pt = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 175f * s;
            DrawCircle(pt, 2.5f, new Color(1f, 1f, 0.95f, Alpha * 0.8f));
        }
    }
}

internal partial class SeraphicWings : Node2D
{
    public float Progress { get; set; }
    public float Alpha { get; set; }
    private float _t;

    public override void _Process(double delta) { _t += (float)delta; QueueRedraw(); }

    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        var p = Mathf.Clamp(Progress, 0f, 1f);
        for (var side = -1; side <= 1; side += 2)
        {
            for (var i = 0; i < 9; i++)
            {
                var t = i / 8f;
                var points = new Vector2[7];
                for (var j = 0; j < points.Length; j++)
                {
                    var u = j / (points.Length - 1f);
                    var x = side * (135f + (210f + 160f * p) * t * u);
                    var y = -70f - 150f * t * u + 28f * Mathf.Sin(u * Mathf.Pi + t * 2f);
                    points[j] = new Vector2(x, y);
                }
                DrawPolyline(points, new Color(0.95f, 0.98f, 1f, Alpha * (0.2f + 0.28f * (1f - t))), 7f, true);
                DrawPolyline(points, new Color(1f, 0.96f, 0.78f, Alpha * (0.4f + 0.35f * (1f - t))), 2f, true);
            }
        }
    }
}

internal partial class SeraphicPillar : Node2D
{
    public float Progress { get; set; }
    public float Alpha { get; set; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        var h = Mathf.Max(0.01f, Progress);
        var bottom = 40f;
        var top = -760f * h;
        foreach (var side in new[] { -1f, 1f })
        {
            DrawColoredPolygon(new[]
            {
                new Vector2(side * 190f, bottom), new Vector2(side * 145f, top),
                new Vector2(side * 175f, top), new Vector2(side * 245f, bottom),
            }, new Color(1f, 0.97f, 0.8f, Alpha * 0.18f));
            DrawLine(new Vector2(side * 210f, bottom), new Vector2(side * 160f, top),
                new Color(1f, 1f, 0.94f, Alpha * 0.5f), 3f, true);
        }
    }
}

internal partial class SeraphicGround : Node2D
{
    public float Progress { get; set; }
    public float Alpha { get; set; }
    public float Pulse { get; set; } = 1f;
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        var p = Mathf.Max(0.01f, Progress) * Pulse;
        DrawCircle(Vector2.Zero, 190f * p, new Color(1f, 0.86f, 0.52f, Alpha * 0.09f));
        DrawCircle(Vector2.Zero, 130f * p, new Color(1f, 0.95f, 0.72f, Alpha * 0.16f));
        DrawArc(Vector2.Zero, 160f * p, 0, Mathf.Tau, 96, new Color(1f, 0.92f, 0.62f, Alpha * 0.55f), 2.5f, true);
        for (var i = 0; i < 12; i++)
        {
            var a = i * Mathf.Tau / 12f;
            var p1 = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 135f * p;
            var p2 = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 178f * p;
            DrawLine(p1, p2, new Color(1f, 0.96f, 0.78f, Alpha * 0.7f), 2f, true);
        }
    }
}

internal partial class SeraphicAura : Node2D
{
    public float Alpha { get; set; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        DrawArc(new Vector2(0, -210f), 250f, 0, Mathf.Tau, 96, new Color(1f, 0.86f, 0.52f, Alpha * 0.35f), 1.5f, true);
        DrawArc(new Vector2(0, -210f), 205f, 0, Mathf.Tau, 96, new Color(1f, 0.98f, 0.82f, Alpha * 0.45f), 2f, true);
    }
}

internal partial class SeraphicOrb : Node2D
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
        Progress = 0f; Alpha = 0f;
        Position = new Vector2(Mathf.Cos(_phase) * 120f, 20f);
    }
    public void UpdateVisual(float elapsed, bool charge)
    {
        if (Alpha <= 0.01f) return;
        if (charge)
        {
            var p = Mathf.Clamp(Progress, 0f, 1f);
            var e = 1f - (1f - p) * (1f - p);
            Position = new Vector2(Mathf.Cos(_phase) * (120f - 60f * e), 20f - 220f * e + Mathf.Sin(elapsed * 2f + _index) * 3f);
        }
        else
        {
            Position = new Vector2(Mathf.Cos(_phase + elapsed * 0.5f) * 85f, -220f + Mathf.Sin(_phase + elapsed * 0.8f) * 25f);
        }
    }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        DrawCircle(Vector2.Zero, 24f, new Color(1f, 0.82f, 0.4f, Alpha * 0.08f));
        DrawCircle(Vector2.Zero, 12f, new Color(1f, 0.92f, 0.65f, Alpha * 0.25f));
        DrawCircle(Vector2.Zero, 5f, new Color(1f, 1f, 0.95f, Alpha));
    }
}

internal partial class SeraphicFeathers : Node2D
{
    public float Progress { get; set; }
    public float Alpha { get; set; }
    private float _t;
    public override void _Process(double delta) { _t += (float)delta; QueueRedraw(); }
    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        var p = Mathf.Clamp(Progress, 0f, 1f);
        for (var i = 0; i < 28; i++)
        {
            var a = i * 2.399f + _t * (0.2f + i % 3 * 0.05f);
            var r = (80f + (i * 37 % 240)) * (0.5f + p * 0.8f);
            var pos = new Vector2(Mathf.Cos(a) * r, -220f + Mathf.Sin(a) * r * 0.45f);
            var dir = new Vector2(Mathf.Cos(a + 1.5f), Mathf.Sin(a + 1.5f));
            DrawLine(pos - dir * 7f, pos + dir * 7f, new Color(1f, 0.96f, 0.8f, Alpha * 0.55f), 1.5f, true);
            DrawCircle(pos, 1.7f, new Color(1f, 1f, 0.95f, Alpha * 0.8f));
        }
    }
}

internal partial class SeraphicShockwave : Node2D
{
    public float Progress { get; set; }
    public float Alpha { get; set; }
    public float RadiusBoost { get; set; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (Alpha <= 0.01f) return;
        var r = 60f + 390f * Mathf.Clamp(Progress, 0f, 1f) + 100f * RadiusBoost;
        DrawArc(new Vector2(0, -220f), r, 0, Mathf.Tau, 120, new Color(1f, 0.98f, 0.82f, Alpha), 3f, true);
        DrawArc(new Vector2(0, -220f), r * 0.82f, 0, Mathf.Tau, 96, new Color(1f, 0.85f, 0.5f, Alpha * 0.5f), 2f, true);
    }
}

/// <summary>Attach the seraphic-domain layer to the Believer's visual root.</summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class BelieverOmniscienceDomainVfxAttachPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        if (!__instance.Entity.IsPlayer || __instance.Entity.Player == null) return;
        if (__instance.Entity.Player.Character is not global::Believer.BelieverCode.Character.Believer) return;
        var effectLayer = __instance.Visuals.GetNodeOrNull<Node2D>("BelieverEffectLayer");
        if (effectLayer == null) return;
        if (effectLayer.GetNodeOrNull<BelieverOmniscienceDomainVfxLayer>("BelieverOmniscienceDomainVfxLayer") != null) return;
        var layer = new BelieverOmniscienceDomainVfxLayer { Name = "BelieverOmniscienceDomainVfxLayer" };
        layer.Initialize(__instance.Entity.Player, __instance.Visuals);
        effectLayer.AddChild(layer);
        BelieverOmniscienceDomainVfx.Register(__instance.Entity.Player, layer);
        MainFile.Logger.Info("[OmniscienceDomain] layer attached");
    }
}
