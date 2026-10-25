namespace Vantage.Shared.Net;

/// <summary>
/// Holds the last Capacity states in insertion order. Add ticks must be strictly
/// newer, within TickMath's half-range comparison window. Gaps are allowed.
/// Stored states must be immutable (the buffer does not clone them).
/// </summary>
public sealed class TickStateRingBuffer<T>
{
    public const int DefaultCapacity = 64;

    private readonly (uint Tick, T State)[] _entries;
    private int _next;

    public TickStateRingBuffer(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _entries = new (uint, T)[capacity];
    }

    public int Capacity => _entries.Length;
    public int Count { get; private set; }
    public uint? OldestTick => Count == 0 ? null : _entries[(_next - Count + Capacity) % Capacity].Tick;
    public uint? NewestTick => Count == 0 ? null : _entries[(_next - 1 + Capacity) % Capacity].Tick;

    public void Add(uint tick, T state)
    {
        if (NewestTick is uint newest && !TickMath.IsNewerThan(tick, newest))
        {
            throw new ArgumentException("Tick must be strictly newer within TickMath's comparison window.", nameof(tick));
        }

        _entries[_next] = (tick, state);
        _next = (_next + 1) % Capacity;
        Count = Math.Min(Count + 1, Capacity);
    }

    public bool TryGet(uint tick, out T state)
    {
        for (int offset = 0; offset < Count; offset++)
        {
            var entry = _entries[(_next - 1 - offset + Capacity) % Capacity];
            if (entry.Tick == tick)
            {
                state = entry.State;
                return true;
            }
        }

        state = default!;
        return false;
    }
}
