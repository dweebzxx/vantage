using Godot;
using Vantage.Shared.Game;
using Vantage.Shared.Net;
using Vantage.Shared.Protocol;

namespace Vantage.Server;

public sealed record WorldState(uint Tick, IReadOnlyList<PlayerState> Players);

public partial class World : Node
{
    public const int HistoryCapacity = TickStateRingBuffer<WorldState>.DefaultCapacity;
    // Global spawn index: 0 = Blue first, 1 = Red first, 2 = Blue second, 3 = Red second.
    private static readonly Vector2[] Spawns = [new(0, 0), new(-160, 96), new(96, -64), new(-160, -64)];
    private static readonly int[] BlueSpawns = [0, 2];
    private static readonly int[] RedSpawns = [1, 3];
    private readonly Dictionary<int, ServerPlayer> _players = new();
    private uint _nextEntityId = 1;
    private Node2D _arena = null!;

    private readonly WeaponSystem _weapons = new();
    private readonly GrenadeSystem _grenades = new();
    public List<byte[]> Events { get; } = new();
    // ServerMain replaces this with the configured timing before the first tick.
    public MatchHost Match { get; set; } = new(MatchTiming.Default);
    public ProjectileState[] CaptureProjectiles() => _weapons.Capture();
    public GrenadeState[] CaptureGrenades() => _grenades.Capture();
    public uint AllocateEntityId()
    {
        if (_nextEntityId == 0) throw new InvalidOperationException("Entity id space exhausted.");
        return _nextEntityId++;
    }

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
                Shape = shape,
                Transform = new Transform2D(0, spawn),
                CollisionMask = 1,
                CollideWithBodies = true,
                CollideWithAreas = false,
            };
            int count = space.IntersectShape(query).Count;
            GD.Print($"[Arena] spawn={spawn}; wall overlaps={count}");
            if (count != 0) return false;
        }
        return !printWalls || ArenaProbe.Check(space);
    }

    private static int[] SpawnsOf(Faction team) => team == Faction.DevBlue ? BlueSpawns : RedSpawns;

    // The global spawn index for a join or a respawn: the team's first spawn that no other player stands on.
    private int ChooseSpawn(Faction team, ServerPlayer? self)
    {
        int[] list = SpawnsOf(team);
        var others = _players.Values.Where(p => p != self)
            .Select(p => new System.Numerics.Vector2(p.Body.GlobalPosition.X, p.Body.GlobalPosition.Y));
        var positions = list.Select(i => new System.Numerics.Vector2(Spawns[i].X, Spawns[i].Y)).ToArray();
        return list[SpawnRules.ChooseSpawn(positions, others)];
    }

    // Used by the defeat path. Bodies of the other players are read as they are at call time.
    public Vector2 RespawnPosition(ServerPlayer player) => Spawns[ChooseSpawn(player.Faction, player)];

    public uint AddPlayer(int peerId, PeerInputCounters input)
    {
        if (_nextEntityId == 0) throw new InvalidOperationException("No entity id available.");
        uint entityId = AllocateEntityId();
        Faction team = Match.ChooseTeamForNewPlayer();
        int spawn = ChooseSpawn(team, null);
        var body = new CharacterBody2D
        {
            Name = $"Player{entityId}",
            Position = Spawns[spawn],
            CollisionLayer = 1,
            CollisionMask = 1,
            // Players block each other; they must never carry each other as moving platforms.
            PlatformFloorLayers = 0,
            PlatformWallLayers = 0,
        };
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(32, 32) } });
        _arena.GetParent().AddChild(body);
        var player = new ServerPlayer(entityId, team, body, input);
        _players.Add(peerId, player);
        GD.Print($"[World] joined peer={peerId}; entity={entityId}; spawn={spawn}; position={body.GlobalPosition}");
        Match.AddPlayer(entityId);
        return entityId;
    }

    public void RemovePlayer(int peerId)
    {
        if (!_players.Remove(peerId, out ServerPlayer? player)) return;
        GD.Print($"[World] departed peer={peerId}; entity={player.EntityId}; position={player.Body.GlobalPosition}");
        player.Body.Free();
        Match.RemovePlayer(player.EntityId);
    }

    public PlayerState[] CapturePlayers() => _players.Values.OrderBy(p => p.EntityId).Select(p =>
        new PlayerState(p.EntityId, new(p.Body.GlobalPosition.X, p.Body.GlobalPosition.Y),
            p.Input.LatestAccepted?.AimAngleRadians ?? 0f, p.Faction)).ToArray();

    public void SimulateTick(uint tick)
    {
        Events.Clear();
        ApplyMatchTransition(tick, Match.Advance(tick));
        var players = _players.Values.ToArray();
        var damage = new List<PendingDamage>();
        foreach (var player in players) WeaponSystem.SelectAndAdvanceCooldowns(player);
        foreach (var player in players) MovementSystem.Step(player.Body, player.Input);
        foreach (var player in players)
        {
            var presses = player.Input.Presses.Consume();
            if (presses.FireAim is float fireAim) _weapons.Fire(tick, player, fireAim, _arena.GetWorld2D().DirectSpaceState, players, AllocateEntityId, damage);
            if (presses.GrenadeAim is float grenadeAim) _grenades.Throw(tick, player, grenadeAim, AllocateEntityId);
        }
        _weapons.StepProjectiles(tick, _arena.GetWorld2D().DirectSpaceState, players, damage);
        _grenades.Step(tick, players, damage);
        DamageSystem.Resolve(tick, damage, Events, Match, RespawnPosition);
        Match.AppendMessages(tick, Events);
    }

    // Team moves first, then the reset, so a moved player spawns on the new team's spawns.
    private void ApplyMatchTransition(uint tick, MatchTransition? transition)
    {
        if (transition is not { } made) return;
        bool newMatch = made is { From: MatchPhase.Postgame, To: MatchPhase.Warmup };
        bool realPlay = made is { From: MatchPhase.Warmup, To: MatchPhase.Playing };
        if (newMatch)
        {
            foreach (TeamMove move in Match.TeamMoves)
            {
                ServerPlayer? moved = _players.Values.FirstOrDefault(p => p.EntityId == move.EntityId);
                if (moved is null) continue;
                moved.Faction = move.To;
                GD.Print($"[Match] team-move tick={tick}; entity={move.EntityId}; from={move.From}; to={move.To}");
            }
        }
        if (newMatch || realPlay) ResetPlayers(tick, made);
    }

    private void ResetPlayers(uint tick, MatchTransition made)
    {
        _weapons.Clear();
        _grenades.Clear();
        ServerPlayer[] ordered = _players.Values.OrderBy(p => p.EntityId).ToArray();
        GD.Print($"[Match] reset tick={tick}; from={made.From}; to={made.To}; players={ordered.Length}");
        int blueRank = 0, redRank = 0;
        foreach (ServerPlayer player in ordered)
        {
            int[] list = SpawnsOf(player.Faction);
            int rank = player.Faction == Faction.DevBlue ? blueRank++ : redRank++;
            int spawn = list[rank % list.Length];
            player.Health = CombatRules.DevPlayerMaxHealth;
            player.Body.GlobalPosition = Spawns[spawn];
            player.Body.Velocity = Vector2.Zero;
            player.Weapon = WeaponDef.DevHitscan;
            player.FireRemaining = player.GrenadeRemaining = 0;
            player.Input.Presses.ClearPending();
            GD.Print($"[Match] reset-player tick={tick}; entity={player.EntityId}; team={player.Faction}; spawn={spawn}; position={player.Body.GlobalPosition}; health={player.Health}");
        }
    }

    public void RecordTick(uint tick)
    {
        CurrentState = new(tick, Array.AsReadOnly(CapturePlayers()));
        History.Add(tick, CurrentState);
        if (tick % TickMath.SimulationRateHz == 0)
            foreach (ServerPlayer player in _players.Values)
                GD.Print($"[World] tick={tick}; entity={player.EntityId}; position={player.Body.GlobalPosition}");
    }
}
