using Godot;

/// <summary>
/// Player-facing combat HUD: active weapon, fire-cooldown feedback, and grenade-throw
/// cooldown feedback, sourced from PlayerCombatController's public accessors.
/// Real gameplay systems, replication, and protocol are intentionally out of scope.
/// </summary>
[GlobalClass]
public partial class CombatHud : CanvasLayer
{
    [Export]
    public NodePath CombatControllerPath { get; set; } = new("../DevDebugCombatController");

    [Export]
    public NodePath WeaponNameLabelPath { get; set; } = new("Root/WeaponNameLabel");

    [Export]
    public NodePath FireCooldownBarPath { get; set; } = new("Root/FireCooldownRow/FireCooldownBar");

    [Export]
    public NodePath FireCooldownValueLabelPath { get; set; } = new("Root/FireCooldownRow/FireCooldownValueLabel");

    [Export]
    public NodePath GrenadeCooldownBarPath { get; set; } = new("Root/GrenadeCooldownRow/GrenadeCooldownBar");

    [Export]
    public NodePath GrenadeCooldownValueLabelPath { get; set; } = new("Root/GrenadeCooldownRow/GrenadeCooldownValueLabel");

    private PlayerCombatController? _combatController;
    private Label? _weaponNameLabel;
    private ProgressBar? _fireCooldownBar;
    private Label? _fireCooldownValueLabel;
    private ProgressBar? _grenadeCooldownBar;
    private Label? _grenadeCooldownValueLabel;

    public override void _Ready()
    {
        _combatController = GetNodeOrNull<PlayerCombatController>(CombatControllerPath);
        _weaponNameLabel = GetNodeOrNull<Label>(WeaponNameLabelPath);
        _fireCooldownBar = GetNodeOrNull<ProgressBar>(FireCooldownBarPath);
        _fireCooldownValueLabel = GetNodeOrNull<Label>(FireCooldownValueLabelPath);
        _grenadeCooldownBar = GetNodeOrNull<ProgressBar>(GrenadeCooldownBarPath);
        _grenadeCooldownValueLabel = GetNodeOrNull<Label>(GrenadeCooldownValueLabelPath);
    }

    public override void _Process(double delta)
    {
        if (_combatController is null)
        {
            return;
        }

        if (_weaponNameLabel is not null)
        {
            _weaponNameLabel.Text = $"Weapon: {_combatController.ActiveWeaponId}";
        }

        UpdateCooldownDisplay(
            _combatController.CooldownRemaining,
            _combatController.CooldownDuration,
            _fireCooldownBar,
            _fireCooldownValueLabel);

        UpdateCooldownDisplay(
            _combatController.GrenadeCooldownRemaining,
            _combatController.GrenadeCooldownDuration,
            _grenadeCooldownBar,
            _grenadeCooldownValueLabel);
    }

    private static void UpdateCooldownDisplay(double remaining, double duration, ProgressBar? bar, Label? valueLabel)
    {
        double normalizedPercent = duration > 0.0 ? Mathf.Clamp(remaining / duration, 0.0, 1.0) * 100.0 : 0.0;

        if (bar is not null)
        {
            bar.Value = normalizedPercent;
        }

        if (valueLabel is not null)
        {
            valueLabel.Text = remaining > 0.0 ? remaining.ToString("0.00") : "READY";
        }
    }
}
