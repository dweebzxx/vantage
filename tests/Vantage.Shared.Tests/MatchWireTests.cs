using MemoryPack;
using Vantage.Shared.Game;
using Vantage.Shared.Net;
using Vantage.Shared.Protocol;
using Xunit;

namespace Vantage.Shared.Tests;

public sealed class MatchWireTests
{
    private static ScoreboardRow[] Rows() =>
        [new(7, Faction.DevRed, 3, 1), new(2, Faction.DevBlue, 5, 4), new(9, Faction.DevBlue, 0, 0)];

    private static ScoreboardMessage Scoreboard() => new(500, Rows());
    private static MatchStateMessage MatchState() => new(1200, MatchPhase.Playing, 19000, 6, 4);

    private static byte[] MatchStatePacket(MatchStateMessage m) =>
        [(byte)MessageType.MatchState, .. MemoryPackSerializer.Serialize(m)];

    private static byte[] ScoreboardPacket(ScoreboardMessage m) =>
        [(byte)MessageType.Scoreboard, .. MemoryPackSerializer.Serialize(m)];

    private static byte[] SnapshotPacket() =>
        MessageCodec.EncodeSnapshot(new(1, 0, [new(1, new(0, 0), 0f, Faction.DevBlue)], [], []));

    [Fact]
    public void Message_type_numeric_values_keep_1_to_6_and_add_7_and_8()
    {
        Assert.Equal(1, (byte)MessageType.Input);
        Assert.Equal(2, (byte)MessageType.Snapshot);
        Assert.Equal(3, (byte)MessageType.Welcome);
        Assert.Equal(4, (byte)MessageType.Hit);
        Assert.Equal(5, (byte)MessageType.DamageDealt);
        Assert.Equal(6, (byte)MessageType.Defeat);
        Assert.Equal(7, (byte)MessageType.MatchState);
        Assert.Equal(8, (byte)MessageType.Scoreboard);
    }

    [Theory]
    [InlineData(MatchPhase.Idle)]
    [InlineData(MatchPhase.Warmup)]
    [InlineData(MatchPhase.Playing)]
    [InlineData(MatchPhase.Postgame)]
    public void Match_state_round_trip_preserves_every_field_for_each_phase(MatchPhase phase)
    {
        MatchStateMessage original = new(1200, phase, 19000, 6, 4);
        Assert.True(MessageCodec.TryDecodeMatchState(MessageCodec.EncodeMatchState(original), out var actual));
        Assert.Equal(1200u, actual.ServerTick);
        Assert.Equal(phase, actual.Phase);
        Assert.Equal(19000u, actual.PhaseEndTick);
        Assert.Equal(6, actual.BlueScore);
        Assert.Equal(4, actual.RedScore);
    }

    [Fact]
    public void Match_state_envelope_starts_with_7_and_carries_the_memorypack_payload()
    {
        byte[] packet = MessageCodec.EncodeMatchState(MatchState());
        Assert.Equal(7, packet[0]);
        Assert.Equal(MemoryPackSerializer.Serialize(MatchState()), packet[1..]);
    }

    [Fact]
    public void Scoreboard_envelope_starts_with_8_and_carries_the_memorypack_payload()
    {
        byte[] packet = MessageCodec.EncodeScoreboard(Scoreboard());
        Assert.Equal(8, packet[0]);
        Assert.Equal(MemoryPackSerializer.Serialize(Scoreboard()), packet[1..]);
    }

    [Fact]
    public void Scoreboard_round_trip_preserves_every_field_and_sender_order()
    {
        Assert.True(MessageCodec.TryDecodeScoreboard(MessageCodec.EncodeScoreboard(Scoreboard()), out var actual));
        Assert.NotNull(actual);
        Assert.Equal(500u, actual!.ServerTick);
        Assert.Equal(3, actual.Rows.Length);
        Assert.Equal(new ScoreboardRow(7, Faction.DevRed, 3, 1), actual.Rows[0]);
        Assert.Equal(new ScoreboardRow(2, Faction.DevBlue, 5, 4), actual.Rows[1]);
        Assert.Equal(new ScoreboardRow(9, Faction.DevBlue, 0, 0), actual.Rows[2]);
    }

    [Fact]
    public void Scoreboard_round_trip_handles_one_row_and_an_empty_array()
    {
        Assert.True(MessageCodec.TryDecodeScoreboard(
            MessageCodec.EncodeScoreboard(new(1, [new(4, Faction.None, 2, 3)])), out var one));
        Assert.Equal(new ScoreboardRow(4, Faction.None, 2, 3), Assert.Single(one!.Rows));

        Assert.True(MessageCodec.TryDecodeScoreboard(MessageCodec.EncodeScoreboard(new(77, [])), out var empty));
        Assert.Equal(77u, empty!.ServerTick);
        Assert.Empty(empty.Rows);
    }

    [Fact]
    public void Player_state_round_trip_carries_the_faction_and_mixed_snapshots_round_trip()
    {
        PlayerState state = new(5, new(1.5f, -2.5f), 0.75f, Faction.DevRed);
        PlayerState decoded = MemoryPackSerializer.Deserialize<PlayerState>(MemoryPackSerializer.Serialize(state));
        Assert.Equal(Faction.DevRed, decoded.Faction);
        Assert.Equal(state, decoded);

        StateSnapshot snapshot = new(9, 8,
            [new(1, new(0, 0), 0f, Faction.DevBlue), new(2, new(1, 1), 1f, Faction.DevRed), new(3, new(2, 2), 2f, Faction.None)],
            [], []);
        Assert.True(MessageCodec.TryDecodeSnapshot(MessageCodec.EncodeSnapshot(snapshot), out var actual));
        Assert.Equal(
            new[] { Faction.DevBlue, Faction.DevRed, Faction.None },
            actual!.Players.Select(p => p.Faction).ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void New_decoders_reject_empty_unknown_wrong_type_truncated_corrupt_and_trailing_bytes(bool scoreboard)
    {
        byte[] valid = scoreboard ? MessageCodec.EncodeScoreboard(Scoreboard()) : MessageCodec.EncodeMatchState(MatchState());
        byte otherNew = (byte)(scoreboard ? MessageType.MatchState : MessageType.Scoreboard);
        List<byte[]> malformed =
        [
            [], [255, .. valid[1..]], [otherNew, .. valid[1..]], [(byte)MessageType.Snapshot, .. valid[1..]],
            [valid[0], 254, 23, 187, 99, 42], [.. valid, 0xAB, 0xCD],
        ];
        for (int length = 1; length < valid.Length; length++) malformed.Add(valid[..length]);

        foreach (byte[] packet in malformed)
        {
            if (scoreboard)
            {
                Assert.False(MessageCodec.TryDecodeScoreboard(packet, out var actual));
                Assert.Null(actual);
            }
            else
            {
                Assert.False(MessageCodec.TryDecodeMatchState(packet, out var actual));
                Assert.Equal(default, actual);
            }
        }
    }

    [Theory]
    [InlineData((byte)99, 0, 0)]
    [InlineData((byte)4, 0, 0)]
    [InlineData((byte)2, -1, 0)]
    [InlineData((byte)2, 0, -1)]
    [InlineData((byte)2, int.MinValue, int.MinValue)]
    public void Match_state_decoder_rejects_undefined_phase_and_negative_scores(byte phase, int blue, int red)
    {
        byte[] packet = MatchStatePacket(new(1, (MatchPhase)phase, 2, blue, red));
        Assert.False(MessageCodec.TryDecodeMatchState(packet, out var actual));
        Assert.Equal(default, actual);
    }

    [Fact]
    public void Match_state_decoder_accepts_zero_scores_and_boundary_values()
    {
        Assert.True(MessageCodec.TryDecodeMatchState(MatchStatePacket(new(0, MatchPhase.Idle, 0, 0, 0)), out _));
        Assert.True(MessageCodec.TryDecodeMatchState(
            MatchStatePacket(new(uint.MaxValue, MatchPhase.Postgame, uint.MaxValue, int.MaxValue, int.MaxValue)), out _));
    }

    [Fact]
    public void Scoreboard_decoder_rejects_null_rows_undefined_faction_and_negative_counts()
    {
        ScoreboardMessage[] invalid =
        [
            new(1, null!),
            new(1, [new(1, Faction.DevBlue, 0, 0), new(2, (Faction)77, 0, 0)]),
            new(1, [new(1, (Faction)(-1), 0, 0)]),
            new(1, [new(1, Faction.DevBlue, -1, 0)]),
            new(1, [new(1, Faction.DevBlue, 0, -1)]),
            new(1, [new(1, Faction.DevBlue, 3, 2), new(2, Faction.DevRed, int.MinValue, 0)]),
        ];
        foreach (ScoreboardMessage message in invalid)
        {
            Assert.False(MessageCodec.TryDecodeScoreboard(ScoreboardPacket(message), out var actual));
            Assert.Null(actual);
        }
    }

    [Fact]
    public void Scoreboard_decoder_rejects_a_null_message()
    {
        byte[] packet = [(byte)MessageType.Scoreboard, .. MemoryPackSerializer.Serialize<ScoreboardMessage?>(null)];
        Assert.False(MessageCodec.TryDecodeScoreboard(packet, out var actual));
        Assert.Null(actual);
    }

    [Fact]
    public void Snapshot_decoder_rejects_an_undefined_faction_but_accepts_none()
    {
        StateSnapshot bad = new(1, 0,
            [new(1, new(0, 0), 0f, Faction.DevBlue), new(2, new(0, 0), 0f, (Faction)9)], [], []);
        Assert.False(MessageCodec.TryDecodeSnapshot(MessageCodec.EncodeSnapshot(bad), out var rejected));
        Assert.Null(rejected);

        StateSnapshot none = new(1, 0, [new(1, new(0, 0), 0f, Faction.None)], [], []);
        Assert.True(MessageCodec.TryDecodeSnapshot(MessageCodec.EncodeSnapshot(none), out var accepted));
        Assert.Equal(Faction.None, Assert.Single(accepted!.Players).Faction);
    }

    [Fact]
    public void Time_left_is_correct_when_the_phase_end_tick_wraps_past_zero()
    {
        // The phase ends 31 ticks after the latest snapshot, across the uint wrap.
        uint snapshotTick = uint.MaxValue - 10;
        MatchStateMessage message = new(snapshotTick, MatchPhase.Playing, 20, 0, 0);
        Assert.True(MessageCodec.TryDecodeMatchState(MessageCodec.EncodeMatchState(message), out var actual));
        Assert.Equal(31, TickMath.Delta(actual.PhaseEndTick, snapshotTick));
    }

    [Fact]
    public void Phase_end_tick_near_max_survives_the_wire_and_a_wrapped_later_snapshot_reads_as_elapsed()
    {
        MatchStateMessage message = new(uint.MaxValue - 20, MatchPhase.Playing, uint.MaxValue - 10, 0, 0);
        Assert.True(MessageCodec.TryDecodeMatchState(MessageCodec.EncodeMatchState(message), out var actual));
        Assert.Equal(uint.MaxValue - 10, actual.PhaseEndTick);
        // Before the wrap, 10 ticks remain; a snapshot at tick 5 (after the wrap) is 16 ticks past the end.
        Assert.Equal(10, TickMath.Delta(actual.PhaseEndTick, uint.MaxValue - 20));
        Assert.Equal(-16, TickMath.Delta(actual.PhaseEndTick, 5));
    }

    [Fact]
    public void Snapshot_decoder_rejects_welcome_and_hit_packets_and_new_decoders_reject_a_snapshot_packet()
    {
        Assert.False(MessageCodec.TryDecodeSnapshot(MessageCodec.EncodeWelcome(new(1, 2)), out var s1));
        Assert.Null(s1);
        Assert.False(MessageCodec.TryDecodeSnapshot(MessageCodec.EncodeHit(new(1, 2, 3)), out var s2));
        Assert.Null(s2);

        byte[] snapshot = SnapshotPacket();
        Assert.False(MessageCodec.TryDecodeMatchState(snapshot, out var match));
        Assert.Equal(default, match);
        Assert.False(MessageCodec.TryDecodeScoreboard(snapshot, out var board));
        Assert.Null(board);
    }
}
