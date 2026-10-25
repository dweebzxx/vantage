using Vantage.Shared.Net;
using Xunit;

namespace Vantage.Shared.Tests;

public sealed class TickStateRingBufferTests
{
    [Fact]
    public void Empty_buffer_has_no_bounds_or_states()
    {
        var buffer = new TickStateRingBuffer<string>();

        Assert.Equal(64, buffer.Capacity);
        Assert.Equal(0, buffer.Count);
        Assert.Null(buffer.OldestTick);
        Assert.Null(buffer.NewestTick);
        Assert.False(buffer.TryGet(0u, out _));
        Assert.False(buffer.TryGet(uint.MaxValue, out _));
    }

    [Fact]
    public void Partial_fill_returns_exact_states_and_rejects_missing_ticks()
    {
        var buffer = new TickStateRingBuffer<string>(4);
        buffer.Add(10u, "first");
        buffer.Add(12u, "second"); // Gaps do not invent a stored state.

        Assert.Equal(2, buffer.Count);
        Assert.Equal(10u, buffer.OldestTick);
        Assert.Equal(12u, buffer.NewestTick);
        Assert.True(buffer.TryGet(10u, out string first));
        Assert.Equal("first", first);
        Assert.True(buffer.TryGet(12u, out string second));
        Assert.Equal("second", second);
        Assert.False(buffer.TryGet(9u, out _));
        Assert.False(buffer.TryGet(11u, out _));
        Assert.False(buffer.TryGet(13u, out _));
    }

    [Fact]
    public void Exactly_full_buffer_retains_every_state()
    {
        var buffer = new TickStateRingBuffer<string>(3);
        buffer.Add(20u, "a");
        buffer.Add(21u, "b");
        buffer.Add(22u, "c");

        Assert.Equal(3, buffer.Count);
        Assert.Equal(20u, buffer.OldestTick);
        Assert.Equal(22u, buffer.NewestTick);
        Assert.True(buffer.TryGet(20u, out string first));
        Assert.Equal("a", first);
        Assert.True(buffer.TryGet(21u, out string middle));
        Assert.Equal("b", middle);
        Assert.True(buffer.TryGet(22u, out string last));
        Assert.Equal("c", last);
    }

    [Fact]
    public void Repeated_overwrites_evict_oldest_and_update_bounds()
    {
        var buffer = new TickStateRingBuffer<int>(3);
        for (uint tick = 10; tick <= 17; tick++)
        {
            buffer.Add(tick, (int)tick * 10);
        }

        Assert.Equal(3, buffer.Count);
        Assert.Equal(15u, buffer.OldestTick);
        Assert.Equal(17u, buffer.NewestTick);
        for (uint tick = 10; tick <= 14; tick++)
        {
            Assert.False(buffer.TryGet(tick, out _));
        }
        Assert.True(buffer.TryGet(15u, out int first));
        Assert.Equal(150, first);
        Assert.True(buffer.TryGet(16u, out int middle));
        Assert.Equal(160, middle);
        Assert.True(buffer.TryGet(17u, out int last));
        Assert.Equal(170, last);
        Assert.False(buffer.TryGet(18u, out _));
    }

    [Fact]
    public void Uint_wraparound_preserves_exact_lookup_and_overwrite_order()
    {
        var buffer = new TickStateRingBuffer<string>(3);
        buffer.Add(uint.MaxValue - 1u, "evicted");
        buffer.Add(uint.MaxValue, "before wrap");
        buffer.Add(0u, "zero");

        Assert.Equal(uint.MaxValue - 1u, buffer.OldestTick);
        Assert.Equal(0u, buffer.NewestTick);
        Assert.True(buffer.TryGet(uint.MaxValue - 1u, out string initial));
        Assert.Equal("evicted", initial);
        buffer.Add(1u, "after wrap");

        Assert.Equal(3, buffer.Count);
        Assert.Equal(uint.MaxValue, buffer.OldestTick);
        Assert.Equal(1u, buffer.NewestTick);
        Assert.False(buffer.TryGet(uint.MaxValue - 1u, out _));
        Assert.False(buffer.TryGet(2u, out _));
        Assert.True(buffer.TryGet(uint.MaxValue, out string before));
        Assert.Equal("before wrap", before);
        Assert.True(buffer.TryGet(0u, out string zero));
        Assert.Equal("zero", zero);
        Assert.True(buffer.TryGet(1u, out string after));
        Assert.Equal("after wrap", after);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Nonpositive_capacity_is_rejected(int capacity)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TickStateRingBuffer<int>(capacity));
        Assert.Equal("capacity", exception.ParamName);
    }

    [Theory]
    [InlineData(5u)] // Duplicate.
    [InlineData(4u)] // Older.
    [InlineData(2147483653u)] // Exactly half-range is ambiguous.
    public void Nonnewer_tick_is_rejected_without_changing_history(uint tick)
    {
        var buffer = new TickStateRingBuffer<string>(1);
        buffer.Add(5u, "kept");

        var exception = Assert.Throws<ArgumentException>(() => buffer.Add(tick, "rejected"));

        Assert.Equal("tick", exception.ParamName);
        Assert.Equal(1, buffer.Count);
        Assert.Equal(5u, buffer.OldestTick);
        Assert.Equal(5u, buffer.NewestTick);
        Assert.True(buffer.TryGet(5u, out string kept));
        Assert.Equal("kept", kept);
    }
}
