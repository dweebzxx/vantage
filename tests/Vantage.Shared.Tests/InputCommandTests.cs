using System.Numerics;
using Vantage.Shared.Protocol;
using Xunit;

namespace Vantage.Shared.Tests;

public sealed class InputCommandTests
{
    [Fact]
    public void Input_range_and_clamp_handle_nan_infinity_and_out_of_range_values()
    {
        // Each component and aim must independently reject nonfinite values.
        float[] invalid = [float.NaN, float.PositiveInfinity, float.NegativeInfinity];
        float[] clamped = [0.0f, 1.0f, -1.0f];
        for (int i = 0; i < invalid.Length; i++)
        {
            InputCommand x = new(17u, new(invalid[i], 0.5f), 2.0f, InputButtons.SecondaryFire);
            InputCommand y = new(17u, new(-0.5f, invalid[i]), 2.0f, InputButtons.SecondaryFire);
            InputCommand aim = new(17u, new(-0.5f, 0.5f), invalid[i], InputButtons.SecondaryFire);
            Assert.False(x.IsInRange());
            Assert.False(y.IsInRange());
            Assert.False(aim.IsInRange());
            Assert.Equal(new InputCommand(17u, new(clamped[i], 0.5f), 2.0f, InputButtons.SecondaryFire), x.Clamped());
            Assert.Equal(new InputCommand(17u, new(-0.5f, clamped[i]), 2.0f, InputButtons.SecondaryFire), y.Clamped());
            Assert.Equal(new InputCommand(17u, new(-0.5f, 0.5f), 0.0f, InputButtons.SecondaryFire), aim.Clamped());
            Assert.True(x.Clamped().IsInRange());
            Assert.True(y.Clamped().IsInRange());
            Assert.True(aim.Clamped().IsInRange());
        }

        InputCommand outside = new(99u, new(-2.5f, 3.5f), 20.0f, (InputButtons)128);
        Assert.False(outside.IsInRange());
        Assert.Equal(new InputCommand(99u, new(-1.0f, 1.0f), 20.0f, (InputButtons)128), outside.Clamped());
        Assert.Equal(new Vector2(-2.5f, 3.5f), outside.Move);
    }

    [Fact]
    public void Input_range_accepts_boundaries_and_clamp_preserves_valid_values()
    {
        InputCommand input = new(3u, new(-1.0f, 1.0f), -20.0f,
            InputButtons.PrimaryFire | InputButtons.SecondaryFire | InputButtons.Grenade);

        Assert.True(input.IsInRange());
        Assert.Equal(input, input.Clamped());
        Assert.True(default(InputCommand).IsInRange());
        Assert.Equal(0, (byte)InputButtons.None);
        Assert.Equal(1, (byte)InputButtons.PrimaryFire);
        Assert.Equal(2, (byte)InputButtons.SecondaryFire);
        Assert.Equal(4, (byte)InputButtons.Grenade);
        Assert.Equal(2, ProtocolConstants.ProtocolVersion);
    }
}
