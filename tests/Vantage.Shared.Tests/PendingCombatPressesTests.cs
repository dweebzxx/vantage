using System.Numerics;
using Vantage.Shared.Net;
using Vantage.Shared.Protocol;
using Xunit;
namespace Vantage.Shared.Tests;

public sealed class PendingCombatPressesTests
{
    private static InputCommand Command(uint tick, InputButtons buttons, float aim = 0) => new(tick, Vector2.Zero, aim, buttons);

    [Fact]
    public void First_held_command_is_only_a_baseline_and_repeated_hold_does_not_fire()
    {
        var presses = new PendingCombatPresses();
        presses.Accept(Command(1, InputButtons.PrimaryFire | InputButtons.Grenade));
        presses.Accept(Command(2, InputButtons.PrimaryFire | InputButtons.Grenade));
        Assert.Equal(((float?)null, (float?)null), presses.Consume());
    }

    [Fact]
    public void Press_then_release_in_one_poll_survives_with_original_aim_and_consumes_once()
    {
        var presses = new PendingCombatPresses();
        presses.Accept(Command(1, InputButtons.None));
        presses.Accept(Command(2, InputButtons.PrimaryFire, 1.25f));
        presses.Accept(Command(3, InputButtons.None, -2f));
        Assert.Equal(((float?)1.25f, (float?)null), presses.Consume());
        Assert.Equal(((float?)null, (float?)null), presses.Consume());
        presses.Accept(Command(4, InputButtons.None));
        Assert.Null(presses.Consume().FireAim);
    }

    [Fact]
    public void Independent_pending_buttons_keep_first_press_aim_when_multiple_arrive()
    {
        var presses = new PendingCombatPresses();
        presses.Accept(Command(1, InputButtons.None));
        presses.Accept(Command(2, InputButtons.PrimaryFire, 0.5f));
        presses.Accept(Command(3, InputButtons.Grenade, -1f));
        presses.Accept(Command(4, InputButtons.PrimaryFire | InputButtons.Grenade, 2f));
        Assert.Equal(((float?)0.5f, (float?)(-1f)), presses.Consume());
    }

    [Fact]
    public void Invalid_duplicate_and_old_commands_change_neither_baseline_nor_pending_aim()
    {
        var presses = new PendingCombatPresses();
        presses.Accept(Command(10, InputButtons.None));
        presses.Accept(Command(11, InputButtons.PrimaryFire, float.NaN));
        presses.Accept(Command(11, InputButtons.Grenade) with { Move = new(100, 0) });
        presses.Accept(Command(10, InputButtons.PrimaryFire, 2f));
        presses.Accept(Command(9, InputButtons.Grenade, 3f));
        Assert.Equal(((float?)null, (float?)null), presses.Consume());
        presses.Accept(Command(11, InputButtons.PrimaryFire, 0.75f));
        presses.Accept(Command(10, InputButtons.None));
        presses.Accept(Command(12, InputButtons.PrimaryFire, 2f));
        Assert.Equal(((float?)0.75f, (float?)null), presses.Consume());
    }

    [Fact]
    public void Accepted_edges_cross_tick_wrap_and_clear_does_not_turn_hold_into_press()
    {
        var presses = new PendingCombatPresses();
        presses.Accept(Command(uint.MaxValue, InputButtons.None));
        presses.Accept(Command(0, InputButtons.Grenade, -0.5f));
        Assert.Equal((float?)(-0.5f), presses.Consume().GrenadeAim);
        presses.Accept(Command(1, InputButtons.None));
        presses.Accept(Command(2, InputButtons.PrimaryFire, 1f));
        presses.ClearPending();
        presses.Accept(Command(3, InputButtons.PrimaryFire));
        Assert.Null(presses.Consume().FireAim);
    }
}
