using Godot;
using Vantage.Shared.Net;
using Vantage.Shared.Protocol;

namespace Vantage.Server;

public sealed record WorldState(uint Tick, IReadOnlyList<PlayerState> Players);

public partial class World : Node
{
    public const int HistoryCapacity = TickStateRingBuffer<WorldState>.DefaultCapacity;
    public static readonly Vector2 FirstSpawn = new(0, 0);
    public static readonly Vector2 SecondSpawn = new(-160, 96);
    private static readonly Vector2[] Spawns = [FirstSpawn, SecondSpawn];
    private readonly Dictionary<int, ServerPlayer> _players = new();
    private readonly bool[] _occupiedSpawns = new bool[TransportConstants.MaxClients];
    private uint _nextEntityId = 1;
    private Node2D _arena = null!;

    private sealed record ServerPlayer(uint EntityId, int SpawnIndex, CharacterBody2D Body, PeerInputCounters Input);

    public WorldState CurrentState { get; private set; } = new(0u, Array.Empty<PlayerState>());
    public TickStateRingBuffer<WorldState> History { get; } = new(HistoryCapacity);

    public void Initialize()
    {
        _arena = GD.Load<PackedScene>("res://arenas/Arena01.tscn").Instantiate<Node2D>();
        AddChild(_arena);
    }

    // Run after the physics server has registered the arena, before any players exist.
    public bool VerifyArena(bool printWalls)
    {
        var space = _arena.GetWorld2D().DirectSpaceState;
        using var shape = new RectangleShape2D { Size = new Vector2(32, 32) };
        foreach (Vector2 spawn in Spawns)
        {
            using var query = new PhysicsShapeQueryParameters2D
            {
                Shape = shape, Transform = new Transform2D(0, spawn),
                CollisionMask = 1, CollideWithBodies = true, CollideWithAreas = false,
            };
            int count = space.IntersectShape(query).Count;
            GD.Print($"[Arena] spawn={spawn}; wall overlaps={count}");
            if (count != 0) return false;
        }
        return !printWalls || ArenaProbe.Check(space);
    }

    public uint AddPlayer(int peerId, PeerInputCounters input)
    {
        int slot = Array.FindIndex(_occupiedSpawns, occupied => !occupied);
        if (slot < 0 || _nextEntityId == 0) throw new InvalidOperationException("No spawn or entity id available.");
        uint entityId = _nextEntityId++;
        var body = new CharacterBody2D
        {
            Name = $"Player{entityId}", Position = Spawns[slot], CollisionLayer = 1, CollisionMask = 1,
            // Players block each other; they must never carry each other as moving platforms.
            PlatformFloorLayers = 0, PlatformWallLayers = 0,
        };
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(32, 32) } });
        _arena.GetParent().AddChild(body);
        _occupiedSpawns[slot] = true;
        _players.Add(peerId, new(entityId, slot, body, input));
        GD.Print($"[World] joined peer={peerId}; entity={entityId}; spawn={slot}; position={body.GlobalPosition}");
        return entityId;
    }

    public void RemovePlayer(int peerId)
    {
        if (!_players.Remove(peerId, out ServerPlayer? player)) return;
        GD.Print($"[World] departed peer={peerId}; entity={player.EntityId}; position={player.Body.GlobalPosition}");
        _occupiedSpawns[player.SpawnIndex] = false;
        player.Body.Free();
    }

    public PlayerState[] CapturePlayers() => _players.Values.OrderBy(p => p.EntityId).Select(p =>
        new PlayerState(p.EntityId, new(p.Body.GlobalPosition.X, p.Body.GlobalPosition.Y),
            p.Input.LatestAccepted?.AimAngleRadians ?? 0f)).ToArray();

    public void RecordTick(uint tick)
    {
        foreach (ServerPlayer player in _players.Values) MovementSystem.Step(player.Body, player.Input);
        CurrentState = new(tick, Array.AsReadOnly(CapturePlayers()));
        History.Add(tick, CurrentState);
        if (tick % TickMath.SimulationRateHz == 0)
            foreach (ServerPlayer player in _players.Values)
                GD.Print($"[World] tick={tick}; entity={player.EntityId}; position={player.Body.GlobalPosition}");
    }
}
