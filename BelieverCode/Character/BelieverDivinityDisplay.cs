using Godot;
using Believer.BelieverCode.Resources;

namespace Believer.BelieverCode.Character;

/// <summary>
/// Custom Divinity HUD: a sacred seal with seven orbiting divinity points.
/// Full points are ivory-gold; empty points are dark hollow rings. The counter is
/// updated from the real BelieverDivinityResource.AmountChanged event and is combat-only.
/// </summary>
internal partial class BelieverDivinityDisplay : Control
{
    private const int MaxPoints = BelieverDivinityResource.MaxAmount;
    private const float Radius = 46f;
    private const float OrbRadius = 7.5f;

    private readonly BelieverDivinityResource _resource;
    private Label _nameLabel = null!;
    private Label _amountLabel = null!;
    private float _time;
    private int _lastAmount = -1;

    public BelieverDivinityDisplay(BelieverDivinityResource resource)
    {
        _resource = resource;
        CustomMinimumSize = new Vector2(128f, 128f);
        Size = new Vector2(128f, 128f);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Ready()
    {
        _nameLabel = new Label
        {
            Text = "神性",
            Position = new Vector2(40f, 4f),
            Modulate = new Color(1f, 0.92f, 0.7f),
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 15);
        _nameLabel.AddThemeColorOverride("font_outline_color", new Color(0.12f, 0.09f, 0.04f));
        _nameLabel.AddThemeConstantOverride("outline_size", 3);
        AddChild(_nameLabel);

        _amountLabel = new Label
        {
            Text = _resource.GetTextForAmount(_resource.Amount),
            Position = new Vector2(47f, 99f),
            Modulate = new Color(1f, 0.97f, 0.82f),
        };
        _amountLabel.AddThemeFontSizeOverride("font_size", 16);
        _amountLabel.AddThemeColorOverride("font_outline_color", new Color(0.12f, 0.09f, 0.04f));
        _amountLabel.AddThemeConstantOverride("outline_size", 3);
        AddChild(_amountLabel);

        _resource.AmountChanged += OnAmountChanged;
        UpdateTooltip();
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        _resource.AmountChanged -= OnAmountChanged;
    }

    private void OnAmountChanged(int oldAmount, int newAmount)
    {
        _lastAmount = newAmount;
        _amountLabel.Text = _resource.GetTextForAmount(newAmount);
        UpdateTooltip();
        QueueRedraw();
    }

    private void UpdateTooltip()
    {
        TooltipText = $"神性 / Divinity\n{_resource.GetTextForAmount(_resource.Amount)}\n每回合开始时获得 1 点神性。";
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        // Event-first update, with a direct resource poll as a safety net for resource replacement
        // during combat transitions.
        if (_lastAmount != _resource.Amount) OnAmountChanged(_lastAmount, _resource.Amount);
        if (_resource.Amount >= MaxPoints) QueueRedraw();
    }

    public override void _Draw()
    {
        var center = new Vector2(64f, 58f);
        var glow = _resource.Amount >= MaxPoints ? 0.18f + 0.08f * Mathf.Sin(_time * 4f) : 0.08f;
        DrawCircle(center, 58f, new Color(0.02f, 0.025f, 0.04f, 0.72f));
        DrawCircle(center, 58f, new Color(1f, 0.86f, 0.5f, glow));
        DrawArc(center, 55f, 0, Mathf.Tau, 80, new Color(1f, 0.85f, 0.5f, 0.65f), 2f, true);
        DrawArc(center, 38f, 0, Mathf.Tau, 64, new Color(1f, 0.95f, 0.72f, 0.45f), 1.2f, true);

        for (var i = 0; i < MaxPoints; i++)
        {
            var angle = -Mathf.Pi / 2f + i * Mathf.Tau / MaxPoints;
            var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;
            var filled = i < _resource.Amount;
            if (filled)
            {
                DrawCircle(point, OrbRadius * 2.1f, new Color(1f, 0.82f, 0.38f, 0.16f));
                DrawCircle(point, OrbRadius, new Color(1f, 0.97f, 0.78f, 0.95f));
                DrawCircle(point, OrbRadius * 1.5f, new Color(1f, 0.9f, 0.55f, 0.28f));
            }
            else
            {
                DrawCircle(point, OrbRadius, new Color(0.08f, 0.09f, 0.12f, 0.9f));
                DrawArc(point, OrbRadius, 0, Mathf.Tau, 24,
                    new Color(0.75f, 0.7f, 0.58f, 0.45f), 1.2f, true);
            }
        }

        // central sacred seal
        var seal = new Color(1f, 0.9f, 0.6f, 0.9f);
        DrawCircle(center, 25f, new Color(0.12f, 0.1f, 0.06f, 0.9f));
        DrawArc(center, 23f, 0, Mathf.Tau, 48, seal, 1.8f, true);
        for (var i = 0; i < 8; i++)
        {
            var a = i * Mathf.Tau / 8f;
            DrawLine(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 7f,
                center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 20f,
                seal, 1.4f, true);
        }
        DrawCircle(center, 5f, new Color(1f, 1f, 0.92f, 0.95f));
    }
}
