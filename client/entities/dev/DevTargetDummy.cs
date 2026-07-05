using Godot;
using Vantage.Shared.Game;

/// <summary>
/// Local-only dev target for proving aim-to-hit plumbing in the movement sandbox.
/// </summary>
[GlobalClass]
public partial class DevTargetDummy : Area2D
{
    private static readonly Color BodyColor = new(0.9f, 0.25f, 0.2f);
    private static readonly Color FlashColor = new(1.0f, 0.95f, 0.2f);
    private static readonly Color InertColor = new(0.25f, 0.25f, 0.25f);

    [Export]
    public int MaxDebugHealth { get; set; } = WeaponDef.DevHitscan.Damage * 3;

    [Export]
    public float FlashSeconds { get; set; } = 0.12f;

    [Export]
    public NodePath DevPlaceholderBodyPath { get; set; } = new("DevPlaceholderBody");

    [Export]
    public NodePath DevDebugHitCountLabelPath { get; set; } = new("DevDebugHitCountLabel");

    public int HitCount { get; private set; }

    public int CurrentDebugHealth { get; private set; }

    public int DebugDamageTaken => Mathf.Clamp(MaxDebugHealth - CurrentDebugHealth, 0, MaxDebugHealth);

    private Polygon2D? _devPlaceholderBody;
    private Label? _devDebugHitCountLabel;
    private double _flashRemaining;

    public override void _Ready()
    {
        MaxDebugHealth = Mathf.Max(1, MaxDebugHealth);
        _devPlaceholderBody = GetNodeOrNull<Polygon2D>(DevPlaceholderBodyPath);
        _devDebugHitCountLabel = GetNodeOrNull<Label>(DevDebugHitCountLabelPath);
        ResetDebugState();
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("dev_reset"))
        {
            ResetDebugState();
        }

        if (_flashRemaining <= 0.0)
        {
            return;
        }

        _flashRemaining = Mathf.Max(0.0, _flashRemaining - delta);

        if (_flashRemaining <= 0.0)
        {
            UpdateDevVisuals(false);
        }
    }

    public void RegisterDevHit(int damage)
    {
        ApplyDebugDamage(damage);
    }

    public void ApplyDebugDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        HitCount++;
        CurrentDebugHealth = DamageMath.ApplyDamageToHealth(CurrentDebugHealth, amount);
        _flashRemaining = FlashSeconds;
        UpdateDevVisuals(true);
    }

    public void ResetDebugState()
    {
        MaxDebugHealth = Mathf.Max(1, MaxDebugHealth);
        HitCount = 0;
        CurrentDebugHealth = MaxDebugHealth;
        _flashRemaining = 0.0;
        UpdateDevVisuals(false);
    }

    private void UpdateDevVisuals(bool flash)
    {
        bool defeated = CurrentDebugHealth <= 0;

        if (_devPlaceholderBody is not null)
        {
            _devPlaceholderBody.Color = defeated ? InertColor : flash ? FlashColor : BodyColor;
        }

        if (_devDebugHitCountLabel is not null)
        {
            _devDebugHitCountLabel.Text = defeated
                ? $"Inert 0/{MaxDebugHealth} Dmg {DebugDamageTaken}/{MaxDebugHealth}"
                : $"HP {CurrentDebugHealth}/{MaxDebugHealth} Dmg {DebugDamageTaken}/{MaxDebugHealth}";
        }
    }
}
