using System.Numerics;
using Vantage.Shared.Game;
using Vantage.Shared.Net;
using Vantage.Shared.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Vantage.Server.IntegrationTests;

[Trait("Category", "ServerIntegration")]
public sealed class ServerScenarios(ScenarioFixture fixture, ITestOutputHelper output) : IClassFixture<ScenarioFixture>
{
    private async Task Run(Func<Scenario, Task> test)
    {
        await fixture.RequireGodot();
        await using var scenario = new Scenario(fixture, output);
        await test(scenario);
    }

    private static Patterns.Entry One(ProcessRunner process, string kind) => Assert.Single(Patterns.Read(process, kind));
    private static void Counters(Scenario scenario, ProcessRunner client, bool illegal = false)
    {
        var smoke = One(client, "smoke");
        long peer = One(client, "connected").Number("peer");
        var counts = Assert.Single(Patterns.Read(scenario.Server, "counters"), p => p.Number("peer") == peer);
        long sent = smoke.Number("sent"), received = counts.Number("received");
        // P26 D4 / P27 D2 record real unreliable-channel loss; R4 chooses an 80% floor.
        Assert.InRange(received, (sent * 80 + 99) / 100, sent);
        Assert.Equal(received, counts.Number("accepted") + counts.Number("rejected") + counts.Number("out-of-order"));
        Assert.Equal(0, counts.Number("out-of-order"));
        if (illegal) Assert.InRange(counts.Number("rejected"), 1, smoke.Number("bad-sent"));
        else Assert.Equal(0, counts.Number("rejected"));
    }

    private static void Positions(Scenario scenario, ProcessRunner client)
    {
        var snapshots = Patterns.Read(client, "snapshot");
        Assert.NotEmpty(snapshots);
        foreach (var snapshot in snapshots)
        {
            var final = Assert.Single(Patterns.Read(scenario.Server, "departed"), p => p.Number("entity") == snapshot.Number("entity"));
            // P26 D3/D5: one two-tick snapshot interval at shared speed = 8 units.
            float allowance = MovementMath.MoveSpeed * TransportConstants.SnapshotIntervalTicks / TickMath.SimulationRateHz;
            Assert.InRange(Vector2.Distance(final.Position, snapshot.Position), 0, allowance);
        }
    }

    [Fact, Trait("Scenario", "Boot")]
    public Task Boot_and_SIGINT_shutdown_have_equal_tick_and_physics_counts() => Run(async s =>
    {
        await s.Boot();
        Assert.Equal(s.Port, One(s.Server, "port").Number("port"));
        await s.StopServer();
        var final = One(s.Server, "final");
        // P24 follow-up 2: equality applies to successful startup, not invalid-port startup.
        Assert.Equal(final.Number("tick"), final.Number("physics frames"));
        Assert.True(final.Number("tick") > 0);
    });

    [Fact, Trait("Scenario", "InvalidPort")]
    public Task Invalid_port_exits_nonzero_with_diagnostic() => Run(async s =>
    {
        var server = s.InvalidPort();
        Assert.NotEqual(0, await server.Exit(TimeSpan.FromSeconds(15)));
        Assert.Contains(server.Lines, line => Patterns.Is(line, "invalid-port"));
    });

    [Fact, Trait("Scenario", "InputTransport")]
    public Task Input_transport_delivers_300_inputs_with_loss_tolerant_counters() => Run(async s =>
    {
        await s.Boot();
        var client = await s.Client("client", "idle", 300);
        await s.Finish(client);
        await s.StopServer();
        // P25 D2: 300 scripted inputs; arrival counts deliberately use P28 R4 instead.
        Assert.Equal(300, One(client, "smoke").Number("sent"));
        Counters(s, client);
    });

    [Fact, Trait("Scenario", "ServerShutdown")]
    public Task Server_shutdown_notifies_connected_client_which_requires_harness_stop() => Run(async s =>
    {
        await s.Boot();
        var client = await s.Client("client", "idle", 36000);
        await s.StopServer();
        await s.Wait(client, "server-lost");
        // P25 V8: loss after a successful connection keeps local play alive.
        Assert.False(client.HasExited);
        Assert.Equal(One(client, "connected").Number("peer"), One(s.Server, "shutdown-peer").Number("peer"));
        // Disposal explicitly records a harness stop, never a clean client exit assertion.
    });

    [Fact, Trait("Scenario", "Movement")]
    public Task East_movement_stops_at_wall_and_matches_last_snapshot() => Run(async s =>
    {
        await s.Boot();
        var client = await s.Client("client", "east", 600);
        await s.Finish(client);
        await s.StopServer();
        var position = One(s.Server, "departed").Position;
        // P26 D3: 32-wide body; right edge within 1 unit of wall x=258, never beyond.
        Assert.InRange(position.X + 16, 257, 258);
        Assert.InRange(MathF.Abs(position.Y), 0, 1);
        Positions(s, client);
        Counters(s, client);
    });

    [Fact, Trait("Scenario", "IllegalInput")]
    public Task Illegal_input_is_rejected_without_exceeding_shared_move_speed() => Run(async s =>
    {
        await s.Boot();
        var client = await s.Client("client", "illegal", 600);
        await s.Finish(client);
        await s.StopServer();
        Counters(s, client, illegal: true);
        var positions = Patterns.Read(s.Server, "position");
        Assert.True(positions.Length >= 2, "Need consecutive server positions to measure movement.");
        foreach (var pair in positions.Zip(positions.Skip(1)))
        {
            long ticks = pair.Second.Number("tick") - pair.First.Number("tick");
            Assert.True(ticks > 0);
            // P26 D4: displacement between logged server ticks cannot exceed shared speed.
            float maximum = MovementMath.MoveSpeed * ticks / TickMath.SimulationRateHz;
            Assert.InRange(Vector2.Distance(pair.First.Position, pair.Second.Position), 0, maximum);
        }
    });

    [Fact, Trait("Scenario", "TwoClients")]
    public Task Two_clients_see_one_other_player_and_match_server_positions() => Run(async s =>
    {
        await s.Boot();
        var first = await s.Client("first", "east", 600);
        var second = await s.Client("second", "east", 600);
        await s.Finish(first);
        await s.Finish(second);
        await s.StopServer();
        foreach (var client in new[] { first, second })
        {
            // P26 D5: others-seen is the maximum over snapshots; teardown can remove a peer.
            Assert.Equal(1, One(client, "smoke").Number("others-seen"));
            Positions(s, client);
            Counters(s, client);
        }
        Assert.Equal(2, Patterns.Read(first, "snapshot").Length);
        // Each client must have observed the other entity, even if it left before the last snapshot.
        Assert.Equal(2, Patterns.Read(s.Server, "joined").Length);
    });

    private static async Task<(ProcessRunner Shooter, ProcessRunner Target)> Fight(Scenario s, string script, int ticks = 330, string targetScript = "idle", string[]? serverArgs = null)
    {
        await s.Boot(serverArgs ?? []);
        var shooter = await s.Client("shooter", script, ticks);
        var target = await s.Client("target", targetScript, ticks + 40);
        await s.Finish(shooter);
        await s.Finish(target);
        await s.StopServer();
        Counters(s, shooter);
        Counters(s, target);
        return (shooter, target);
    }

    [Fact, Trait("Scenario", "Hitscan")]
    public Task Hitscan_four_hits_defeat_respawn_and_reach_both_clients() => Run(async s =>
    {
        var (shooter, target) = await Fight(s, "fire-hitscan");
        var defeat = One(s.Server, "defeat");
        var damage = Patterns.Read(s.Server, "damage").Where(p => p.Index < defeat.Index && p.Number("target") == defeat.Number("target")).ToArray();
        // P27 D2: four 25-damage hits, then full-health respawn at the target's join spawn.
        Assert.Equal(4, damage.Length);
        Assert.All(damage, p => Assert.Equal(25, p.Number("amount")));
        var respawn = One(s.Server, "respawn");
        Assert.True(respawn.Index > defeat.Index);
        Assert.Equal(defeat.Number("tick"), respawn.Number("tick"));
        Assert.Equal(100, respawn.Number("health"));
        var joined = Assert.Single(Patterns.Read(s.Server, "joined"), p => p.Number("entity") == defeat.Number("target"));
        Assert.Equal(joined.Position, respawn.Position);
        var expected = damage.SelectMany(p => new[] { Event("hit", p), Event("damage", p) }).Append(Event("defeat", defeat)).ToArray();
        Assert.Equal(expected, Patterns.Read(shooter, "event").Select(Event).ToArray());
        Assert.Equal(expected, Patterns.Read(target, "event").Select(Event).ToArray());
    });

    private static string Event(string type, Patterns.Entry p) => $"{type}/{p.Number("tick")}/{p.Number("source")}/{p.Number("target")}/{(type == "damage" ? p.Text("amount") : "")}";
    private static string Event(Patterns.Entry p) => Event(p.Text("event"), p);

    [Fact, Trait("Scenario", "BlockedHitscan")]
    public Task Wall_blocked_hitscan_never_damages_target() => Run(async s =>
    {
        await Fight(s, "blocked-hitscan", targetScript: "east");
        // P27 D3: shooter moves east, target moves against the interior obstacle.
        Assert.NotEmpty(Patterns.Read(s.Server, "wall"));
        Assert.Empty(Patterns.Read(s.Server, "damage"));
    });

    [Fact, Trait("Scenario", "Cooldown")]
    public Task Mash_fire_obeys_15_tick_cooldown_and_six_shot_limit() => Run(async s =>
    {
        var (shooter, _) = await Fight(s, "mash-fire", 270);
        var shots = Patterns.Read(s.Server, "shot");
        // P27 D6: 90-tick mash window, 15-tick minimum, at most six shots.
        Assert.InRange(shots.Length, 1, 6);
        foreach (var pair in shots.Zip(shots.Skip(1)))
            Assert.True(pair.Second.Number("tick") - pair.First.Number("tick") >= 15);
        long peer = One(shooter, "connected").Number("peer");
        var counter = Assert.Single(Patterns.Read(s.Server, "counters"), p => p.Number("peer") == peer);
        if (One(shooter, "smoke").Number("sent") == counter.Number("accepted")) Assert.Equal(6, shots.Length);
    });

    [Fact, Trait("Scenario", "Projectile")]
    public Task Projectile_player_hits_use_shared_range_damage() => Run(async s =>
    {
        await Fight(s, "fire-projectile");
        var removed = Patterns.Read(s.Server, "projectile-removed").Where(p => p.Text("reason") == "player").ToArray();
        // P27 D4: every player resolution must be accounted for by exactly one damage line.
        Assert.NotEmpty(removed);
        var damage = Patterns.Read(s.Server, "damage");
        Assert.Equal(removed.Length, damage.Length);
        foreach (var projectile in removed)
        {
            var hit = Assert.Single(damage, d => d.Number("tick") == projectile.Number("tick") && d.Number("source") == projectile.Number("owner"));
            Assert.True(hit.Index > projectile.Index);
            Assert.Equal("projectile", hit.Text("weapon"));
            Assert.Equal(DamageMath.DamageByRange(WeaponDef.DevProjectile.Damage, projectile.Float("distance"), WeaponDef.DevProjectile.RangeUnits), hit.Number("amount"));
        }
    });

    [Fact, Trait("Scenario", "BlockedProjectile")]
    public Task Projectile_wall_removals_never_cause_damage() => Run(async s =>
    {
        await Fight(s, "blocked-projectile");
        // P27 D4 blocked: direct spawn-to-spawn projectile path intersects the obstacle.
        var removed = Patterns.Read(s.Server, "projectile-removed");
        Assert.NotEmpty(removed);
        Assert.All(removed, p => Assert.Equal("wall", p.Text("reason")));
        Assert.Empty(Patterns.Read(s.Server, "damage"));
    });

    [Fact, Trait("Scenario", "GrenadeInside")]
    public Task Grenade_inside_radius_uses_shared_falloff_after_90_ticks() => Grenade("throw-grenade", "inside");

    [Fact, Trait("Scenario", "GrenadeOutside")]
    public Task Grenade_outside_radius_does_no_damage_after_90_ticks() => Grenade("grenade-away", "outside");

    [Fact, Trait("Scenario", "GrenadeOwner")]
    public Task Grenade_owner_inside_radius_remains_immune() => Grenade("grenade-owner", "owner");

    private Task Grenade(string script, string geometry) => Run(async s =>
    {
        await Fight(s, script);
        var created = One(s.Server, "grenade-created");
        var detonated = One(s.Server, "grenade-detonated");
        // P27 D5: fuse is 90 simulation ticks, damage uses logged centre distances.
        Assert.Equal(90, detonated.Number("elapsed"));
        Assert.Equal(90, detonated.Number("tick") - created.Number("tick"));
        Assert.Equal(created.Number("entity"), detonated.Number("entity"));
        var targets = Patterns.Read(s.Server, "grenade-target");
        Assert.Equal(2, targets.Length);
        foreach (var target in targets)
        {
            int expected = target.Text("eligible") == "True" ? DamageMath.RadiusDamage(GrenadeDef.DevPlaceholder, target.Float("distance")) : 0;
            Assert.Equal(expected, target.Number("amount"));
        }
        long owner = created.Number("owner");
        var own = Assert.Single(targets, p => p.Number("target") == owner);
        var other = Assert.Single(targets, p => p.Number("target") != owner);
        Assert.Equal("False", own.Text("eligible"));
        Assert.Equal(0, own.Number("amount"));
        var damage = Patterns.Read(s.Server, "damage");
        Assert.DoesNotContain(damage, p => p.Number("target") == owner);
        if (geometry == "inside")
        {
            Assert.True(other.Number("amount") > 0);
            Assert.Equal(other.Number("amount"), One(s.Server, "damage").Number("amount"));
        }
        else Assert.Empty(damage);
        if (geometry == "outside") Assert.True(other.Float("distance") > GrenadeDef.DevPlaceholder.RadiusUnits);
        if (geometry == "owner") Assert.True(own.Float("distance") < GrenadeDef.DevPlaceholder.RadiusUnits);
    });

    [Fact, Trait("Scenario", "IllegalCannotDamage")]
    public Task Illegal_client_inputs_cannot_cause_damage_or_defeat() => Run(async s =>
    {
        await s.Boot();
        var client = await s.Client("illegal", "illegal", 300);
        var target = await s.Client("target", "idle", 340);
        await s.Finish(client);
        await s.Finish(target);
        await s.StopServer();
        Counters(s, client, illegal: true);
        Counters(s, target);
        // P27 D7: rejected input cannot enter the server's damage/life-state path.
        Assert.Empty(Patterns.Read(s.Server, "damage"));
        Assert.Empty(Patterns.Read(s.Server, "defeat"));
        Assert.Empty(Patterns.Read(client, "event"));
        Assert.Empty(Patterns.Read(target, "event"));
    });

    private static readonly string[] ShortMatch = ["--match-ticks", "30,3000,30"];

    private static string TeamOfEntity(Scenario s, long entity)
    {
        var joined = Assert.Single(Patterns.Read(s.Server, "joined"), p => p.Number("entity") == entity);
        // P34 derived rule: spawn slot 0 is DevBlue, slot 1 is DevRed.
        return joined.Number("spawn") == 0 ? "DevBlue" : "DevRed";
    }

    private static (long Entity, string Faction, long Kills, long Deaths)[] Rows(Patterns.Entry scoreboard) =>
        scoreboard.Text("rows").Length == 0 ? [] : scoreboard.Text("rows").Split(',').Select(row =>
        {
            string[] f = row.Split(':');
            return (long.Parse(f[0]), f[1], long.Parse(f[2]), long.Parse(f[3]));
        }).ToArray();

    [Fact, Trait("Scenario", "MatchPhaseCycle")]
    public Task Match_phases_cycle_on_exact_ticks_and_return_to_idle_when_the_client_leaves() => Run(async s =>
    {
        await s.Boot("--match-ticks", "30,60,30");
        var client = await s.Client("client", "idle", 300);
        await s.Finish(client);
        await s.Server.WaitFor(line => Patterns.Is(line, "match-phase") && line.Contains("to=Idle;", StringComparison.Ordinal), "return to Idle", TimeSpan.FromSeconds(15));
        await s.StopServer();
        var timing = One(s.Server, "match-timing");
        Assert.Equal((30, 60, 30, "option"), (timing.Number("warmup"), timing.Number("playing"), timing.Number("postgame"), timing.Text("source")));
        var phases = Patterns.Read(s.Server, "match-phase");
        string[] expected = ["Idle>Warmup", "Warmup>Playing", "Playing>Postgame", "Postgame>Warmup"];
        long[] duration = [30, 60, 30, 30];
        Assert.Equal(expected, phases.Take(4).Select(p => $"{p.Text("from")}>{p.Text("to")}").ToArray());
        // P34 R10: gaps equal the duration of the phase being left; end-tick = transition + entered duration.
        Assert.Equal(30, phases[1].Number("tick") - phases[0].Number("tick"));
        Assert.Equal(60, phases[2].Number("tick") - phases[1].Number("tick"));
        Assert.Equal(30, phases[3].Number("tick") - phases[2].Number("tick"));
        for (int i = 0; i < 4; i++) Assert.Equal(phases[i].Number("tick") + duration[i], phases[i].Number("end-tick"));
        var states = Patterns.Read(client, "match-state");
        Assert.True(states.Length >= 4);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(phases[i].Text("to"), states[i].Text("phase"));
            Assert.Equal(phases[i].Number("tick"), states[i].Number("tick"));
            Assert.Equal(phases[i].Number("end-tick"), states[i].Number("end-tick"));
        }
        Assert.True(One(client, "welcome").Index < states[0].Index);
        var idle = Assert.Single(phases, p => p.Text("to") == "Idle");
        Assert.Equal((0, 0), (idle.Number("blue"), idle.Number("red")));
        Assert.True(idle.Index > One(s.Server, "match-leave").Index);
        Assert.Empty(Patterns.Read(s.Server, "team-mismatch"));
    });

    [Fact, Trait("Scenario", "MatchKillInWarmup")]
    public Task Match_kill_during_warmup_does_not_score() => Run(async s =>
    {
        var (shooter, target) = await Fight(s, "fire-hitscan");
        Assert.Single(Patterns.Read(s.Server, "defeat"));
        var kill = One(s.Server, "match-kill");
        Assert.Equal(("false", "Warmup", 0, 0), (kill.Text("scored"), kill.Text("phase"), kill.Number("blue"), kill.Number("red")));
        foreach (var client in new[] { shooter, target })
        {
            var boards = Patterns.Read(client, "scoreboard");
            Assert.NotEmpty(boards);
            Assert.All(boards, b => Assert.All(Rows(b), r => Assert.Equal((0, 0), (r.Kills, r.Deaths))));
        }
        Assert.Empty(Patterns.Read(s.Server, "team-mismatch"));
    });

    [Fact, Trait("Scenario", "MatchKillInPlaying")]
    public Task Match_kill_during_playing_scores_for_the_killers_team() => Run(async s =>
    {
        var (shooter, target) = await Fight(s, "fire-hitscan", serverArgs: ShortMatch);
        var defeat = One(s.Server, "defeat");
        var kill = One(s.Server, "match-kill");
        Assert.Equal(("true", "Playing"), (kill.Text("scored"), kill.Text("phase")));
        long shooterId = defeat.Number("source"), targetId = defeat.Number("target");
        Assert.Equal((shooterId, targetId), (kill.Number("killer"), kill.Number("victim")));
        string shooterTeam = TeamOfEntity(s, shooterId), targetTeam = TeamOfEntity(s, targetId);
        Assert.NotEqual(shooterTeam, targetTeam);
        (long shooterScore, long targetScore) = shooterTeam == "DevBlue"
            ? (kill.Number("blue"), kill.Number("red")) : (kill.Number("red"), kill.Number("blue"));
        Assert.Equal((1, 0), (shooterScore, targetScore));
        foreach (var client in new[] { shooter, target })
        {
            var last = Patterns.Read(client, "match-state").Last();
            Assert.Equal((kill.Number("blue"), kill.Number("red")), (last.Number("blue"), last.Number("red")));
            var boards = Patterns.Read(client, "scoreboard");
            Assert.Contains(boards, b => Rows(b).Length == 2);
            // R6 sends a scoreboard when a player leaves, so the state at the kill is the last two-row board.
            var full = Rows(boards.Last(b => Rows(b).Length == 2));
            Assert.Equal(new[] { (shooterId, shooterTeam, 1L, 0L), (targetId, targetTeam, 0L, 1L) }.OrderBy(r => r.Item1), full);
        }
        // The shooter left first; the target's final board is its own row, still 0 kills and 1 death.
        var finalRows = Rows(Patterns.Read(target, "scoreboard").Last());
        Assert.Equal([(targetId, targetTeam, 0L, 1L)], finalRows);
        Assert.Empty(Patterns.Read(s.Server, "team-mismatch"));
    });

    [Fact, Trait("Scenario", "InvalidMatchTicks")]
    public Task Invalid_match_ticks_exit_nonzero_with_diagnostic() => Run(async s =>
    {
        foreach (string value in new[] { "0,1,1", "1,1", "a,b,c" })
        {
            var server = s.InvalidMatchTicks(value);
            Assert.NotEqual(0, await server.Exit(TimeSpan.FromSeconds(15)));
            Assert.Contains(server.Lines, line => Patterns.Is(line, "invalid-match-ticks"));
        }
    });

    // P36. Spawn lists by global index (see World.cs): Blue 0 and 2, Red 1 and 3. Positions come from the
    // server's own [Arena] lines, so the scenarios do not hard-code coordinates.
    private static Vector2[] ArenaSpawns(Scenario s) => Patterns.Read(s.Server, "arena").Select(a => a.Vector("spawn")).ToArray();

    private static bool Overlap(Vector2 a, Vector2 b) =>
        MathF.Abs(a.X - b.X) < SpawnRules.BodySize && MathF.Abs(a.Y - b.Y) < SpawnRules.BodySize;

    [Fact, Trait("Scenario", "FourClients")]
    public Task Four_clients_join_two_per_team_on_distinct_spawns() => Run(async s =>
    {
        await s.Boot();
        // Assumption: the staggered lengths (15 s down to 10 s) keep all four connected together, because
        // each later client starts within a few seconds of the one before it.
        var clients = new List<ProcessRunner>();
        foreach ((string name, int ticks) in new[] { ("c1", 900), ("c2", 800), ("c3", 700), ("c4", 600) })
            clients.Add(await s.Client(name, "idle", ticks));
        foreach (var client in clients) await s.Finish(client);
        await s.StopServer();

        var arena = Patterns.Read(s.Server, "arena");
        Assert.Equal(4, arena.Length);
        Assert.All(arena, a => Assert.Equal(0, a.Number("wall overlaps")));
        var spawns = ArenaSpawns(s);

        var joins = Patterns.Read(s.Server, "match-join");
        Assert.Equal(["DevBlue", "DevRed", "DevBlue", "DevRed"], joins.Select(j => j.Text("team")).ToArray());
        var joined = Patterns.Read(s.Server, "joined");
        Assert.Equal(joins.Select(j => j.Number("entity")), joined.Select(j => j.Number("entity")));
        Assert.Equal([0L, 1L], joined.Take(2).Select(j => j.Number("spawn")).ToArray());
        for (int i = 0; i < 4; i++)
        {
            long spawn = joined[i].Number("spawn");
            Assert.Contains(spawn, i % 2 == 0 ? new[] { 0L, 2L } : new[] { 1L, 3L });
            Assert.Equal(spawns[spawn], joined[i].Position);
        }
        Assert.Equal(4, joined.Select(j => j.Number("spawn")).Distinct().Count());
        for (int i = 0; i < 4; i++)
            for (int j = i + 1; j < 4; j++)
                Assert.False(Overlap(joined[i].Position, joined[j].Position), $"joiners {i} and {j} overlap");

        var teamOf = joins.ToDictionary(j => j.Number("entity"), j => j.Text("team"));
        foreach (var client in clients)
        {
            var boards = Patterns.Read(client, "scoreboard");
            var rows = Rows(boards.Last(b => Rows(b).Length == 4));
            Assert.Equal(2, rows.Count(r => r.Faction == "DevBlue"));
            Assert.Equal(2, rows.Count(r => r.Faction == "DevRed"));
            // D3: the scoreboard rows carry the same team the server joined each player with.
            Assert.All(rows, r => Assert.Equal(teamOf[r.Entity], r.Faction));
        }
    });

    [Fact, Trait("Scenario", "TeamRebalance")]
    public Task Rebalance_moves_the_latest_blue_player_and_resets_everyone() => Run(async s =>
    {
        // Warmup 60 (1 s), playing 720 (12 s), postgame 60 (1 s): the Postgame>Warmup transition is about
        // 14 s after the first join. Assumptions: the Red client (300 ticks, 5 s) leaves after the third
        // client joined and before 14 s; the other two (1080 and 960 ticks) stay at least 4 s past it.
        await s.Boot("--match-ticks", "60,720,60");
        var first = await s.Client("blue-first", "idle", 1080);
        var red = await s.Client("red-leaves", "idle", 300);
        var late = await s.Client("blue-late", "idle", 960);
        await s.Finish(red);
        await s.Server.WaitFor(line => Patterns.Is(line, "team-move"), "team-move", TimeSpan.FromSeconds(25));
        await s.Finish(first);
        await s.Finish(late);
        await s.StopServer();

        var spawns = ArenaSpawns(s);
        var joins = Patterns.Read(s.Server, "match-join");
        Assert.Equal(["DevBlue", "DevRed", "DevBlue"], joins.Select(j => j.Text("team")).ToArray());
        long firstId = joins[0].Number("entity"), redId = joins[1].Number("entity"), lateId = joins[2].Number("entity");
        Assert.Equal(lateId, joins.Where(j => j.Text("team") == "DevBlue").Max(j => j.Number("entity")));
        var lateJoin = Assert.Single(Patterns.Read(s.Server, "joined"), p => p.Number("entity") == lateId);

        var back = Assert.Single(Patterns.Read(s.Server, "match-phase"), p => p.Text("from") == "Postgame" && p.Text("to") == "Warmup");
        var departed = Assert.Single(Patterns.Read(s.Server, "departed"), p => p.Number("entity") == redId);
        Assert.True(departed.Index < back.Index, "the Red client must leave before the new match starts");

        var move = Assert.Single(Patterns.Read(s.Server, "team-move"));
        Assert.Equal((lateId, "DevBlue", "DevRed", back.Number("tick")), (move.Number("entity"), move.Text("from"), move.Text("to"), move.Number("tick")));
        var reset = Assert.Single(Patterns.Read(s.Server, "match-reset"), p => p.Text("from") == "Postgame");
        Assert.True(back.Index < move.Index && move.Index < reset.Index);
        Assert.Equal((back.Number("tick"), "Warmup", 2L), (reset.Number("tick"), reset.Text("to"), reset.Number("players")));

        var players = Patterns.Read(s.Server, "match-reset-player").Where(p => p.Number("tick") == back.Number("tick")).ToArray();
        Assert.All(players, p => Assert.True(p.Index > reset.Index));
        var movedPlayer = Assert.Single(players, p => p.Number("entity") == lateId);
        // Red has one player, so it takes Red's first spawn (index 1), not the Blue spawn it joined on.
        Assert.Equal(("DevRed", 1L, 100L), (movedPlayer.Text("team"), movedPlayer.Number("spawn"), movedPlayer.Number("health")));
        Assert.Equal(spawns[1], movedPlayer.Position);
        Assert.NotEqual(lateJoin.Position, movedPlayer.Position);
        var stayed = Assert.Single(players, p => p.Number("entity") == firstId);
        Assert.Equal(("DevBlue", 0L), (stayed.Text("team"), stayed.Number("spawn")));
        Assert.Equal(spawns[0], stayed.Position);

        foreach (var client in new[] { first, late })
        {
            var board = Assert.Single(Patterns.Read(client, "scoreboard"), b => b.Number("tick") == back.Number("tick"));
            Assert.Equal(new[] { (firstId, "DevBlue", 0L, 0L), (lateId, "DevRed", 0L, 0L) }.OrderBy(r => r.Item1), Rows(board));
        }
    });

    [Fact, Trait("Scenario", "WarmupReset")]
    public Task Warmup_damage_is_reset_when_real_play_starts() => Run(async s =>
    {
        // Warmup 330 ticks (5.5 s after the first join). fire-hitscan starts hitting about 2 to 4 s after the
        // first join (the existing Hitscan scenario needs four hits inside its 330 ticks), and both clients
        // stay connected until at least 6.7 s. Assumption: the second client joins within about 3 s.
        var (shooter, target) = await Fight(s, "fire-hitscan", ticks: 420, serverArgs: ["--match-ticks", "330,3000,30"]);
        var spawns = ArenaSpawns(s);
        var phases = Patterns.Read(s.Server, "match-phase");
        var start = Assert.Single(phases, p => p.Text("from") == "Idle" && p.Text("to") == "Warmup");
        var reset = Assert.Single(Patterns.Read(s.Server, "match-reset"));
        Assert.Equal(("Warmup", "Playing"), (reset.Text("from"), reset.Text("to")));
        Assert.True(reset.Number("tick") > start.Number("tick"));
        Assert.DoesNotContain(Patterns.Read(s.Server, "match-reset"), p => p.Number("tick") == start.Number("tick"));

        var damage = Patterns.Read(s.Server, "damage");
        Assert.NotEmpty(damage);
        var first = damage[0];
        Assert.True(first.Number("tick") < reset.Number("tick"), "the first damage must land during warmup");
        long victim = first.Number("target");
        var line = Assert.Single(Patterns.Read(s.Server, "match-reset-player"), p => p.Number("tick") == reset.Number("tick") && p.Number("entity") == victim);
        Assert.True(line.Index > reset.Index);
        Assert.Equal(100, line.Number("health"));
        string team = Assert.Single(Patterns.Read(s.Server, "match-join"), j => j.Number("entity") == victim).Text("team");
        Assert.Equal(team, line.Text("team"));
        // Each team has one player here, so the victim takes its team's first spawn.
        Assert.Equal(team == "DevBlue" ? 0 : 1, line.Number("spawn"));
        Assert.Equal(spawns[line.Number("spawn")], line.Position);
    });

    // P37. The server's own [Match] join line is the oracle for every team a client shows.
    private static string JoinTeam(Scenario s, long entity) =>
        Assert.Single(Patterns.Read(s.Server, "match-join"), j => j.Number("entity") == entity).Text("team");

    [Fact, Trait("Scenario", "HudPhases")]
    public Task Hud_follows_the_server_phases_and_countdown() => Run(async s =>
    {
        // Warmup 3 s, Playing 6 s, Postgame 3 s. The phases start at the first join (Idle>Warmup), so the
        // second Warmup begins 12 s after it. Assumption: the client's 1000 inputs (about 16.7 s) outlast
        // that point by several seconds, because the client connects within about a second of the join.
        await s.Boot("--match-ticks", "180,360,180");
        var client = await s.Client("client", "idle", 1000);
        await s.Finish(client);
        await s.StopServer();

        var serverPhases = Patterns.Read(s.Server, "match-phase").Take(4).Select(p => p.Text("to")).ToArray();
        Assert.Equal(["Warmup", "Playing", "Postgame", "Warmup"], serverPhases);

        var huds = Patterns.Read(client, "hud");
        Assert.NotEmpty(huds);
        Assert.All(huds, h => Assert.Equal((0L, 0L), (h.Number("blue"), h.Number("red"))));
        Assert.True(One(client, "welcome").Index < huds[0].Index);

        // A team or score change prints another line inside a phase, so look at the first line of each run of
        // the same phase.
        var firsts = new List<Patterns.Entry>();
        foreach (var hud in huds)
            if (firsts.Count == 0 || firsts[^1].Text("phase") != hud.Text("phase")) firsts.Add(hud);
        Assert.Equal(serverPhases, firsts.Take(4).Select(h => h.Text("phase")).ToArray());

        long[] seconds = [3, 6, 3, 3];
        for (int i = 0; i < 4; i++)
        {
            long shown = firsts[i].Number("seconds-left");
            // Tolerance: the newest tick the client knows is never older than the match-state tick, so the
            // countdown can only be at or below the phase length. A snapshot a couple of ticks (well under a
            // second) ahead of the state lowers it by at most one whole second.
            Assert.InRange(shown, seconds[i] - 1, seconds[i]);
        }
    });

    [Fact, Trait("Scenario", "HudTint")]
    public Task Player_bodies_are_tinted_with_the_team_the_server_joined_them_with() => Run(async s =>
    {
        // Assumption: the second client joins within a few seconds of the first, so each client sees a snapshot
        // with both players while both are connected (the clients run 10 s and 9 s).
        await s.Boot();
        var first = await s.Client("first", "idle", 600);
        var second = await s.Client("second", "idle", 540);
        await s.Finish(first);
        await s.Finish(second);
        await s.StopServer();

        var joins = Patterns.Read(s.Server, "match-join");
        Assert.Equal(["DevBlue", "DevRed"], joins.Select(j => j.Text("team")).ToArray());
        var entities = joins.Select(j => j.Number("entity")).Order().ToArray();
        foreach (var client in new[] { first, second })
        {
            var tints = Patterns.Read(client, "tint");
            // Teams never change in this scenario, so each player is tinted exactly once.
            Assert.Equal(entities, tints.Select(t => t.Number("entity")).Order().ToArray());
            Assert.Contains(tints, t => t.Number("entity") == One(client, "welcome").Number("entity"));
            Assert.All(tints, t => Assert.Equal(JoinTeam(s, t.Number("entity")), t.Text("team")));
        }
    });

    [Fact, Trait("Scenario", "HudScores")]
    public Task Hud_shows_the_server_scores_and_each_clients_own_team() => Run(async s =>
    {
        // Same timing assumption as MatchKillInPlaying: the second client joins more than 30 ticks (0.5 s)
        // after the first, so the kill falls in Playing; both clients stay connected past the kill.
        var (shooter, target) = await Fight(s, "fire-hitscan", serverArgs: ShortMatch);
        var kill = One(s.Server, "match-kill");
        Assert.Equal(("true", "Playing"), (kill.Text("scored"), kill.Text("phase")));
        Assert.Equal(1, kill.Number("blue") + kill.Number("red"));
        foreach (var client in new[] { shooter, target })
        {
            var last = Patterns.Read(client, "hud").Last();
            Assert.Equal("Playing", last.Text("phase"));
            Assert.Equal((kill.Number("blue"), kill.Number("red")), (last.Number("blue"), last.Number("red")));
            Assert.Equal(JoinTeam(s, One(client, "welcome").Number("entity")), last.Text("team"));
        }
    });

    // P38. Server timing for the winner scenarios: Warmup 30 ticks (0.5 s), Playing 360 ticks (6 s), Postgame 600 ticks (10 s).
    private static readonly string[] ResultMatch = ["--match-ticks", "30,360,600"];
    private const int ResultPostgameSeconds = 10;

    [Fact, Trait("Scenario", "ResultWinner")]
    public Task Result_screen_names_the_shooters_team_with_the_servers_final_scores() => Run(async s =>
    {
        // Timing assumption: Playing starts 0.5 s after the first join and Postgame 6.5 s after it. The shooter
        // (600 inputs, 10 s) fires its fourth hit about 2.5 s after it sees the target, and the target process
        // starts within a few seconds of the shooter's welcome, so the kill lands in Playing with at least
        // 2 s to spare before Postgame, and both clients stay connected for 3 s or more of Postgame.
        var (shooter, target) = await Fight(s, "fire-hitscan", ticks: 600, serverArgs: ResultMatch);
        var kill = One(s.Server, "match-kill");
        Assert.Equal(("true", "Playing"), (kill.Text("scored"), kill.Text("phase")));
        string shooterTeam = TeamOfEntity(s, kill.Number("killer"));
        string expectedOutcome = shooterTeam == "DevBlue" ? "BlueWins" : "RedWins";
        var postgame = Assert.Single(Patterns.Read(s.Server, "match-phase"), p => p.Text("to") == "Postgame");
        Assert.Equal((kill.Number("blue"), kill.Number("red")), (postgame.Number("blue"), postgame.Number("red")));
        foreach (var client in new[] { shooter, target })
        {
            var result = One(client, "result");
            Assert.Equal(expectedOutcome, result.Text("outcome"));
            Assert.Equal((kill.Number("blue"), kill.Number("red")), (result.Number("blue"), result.Number("red")));
            Assert.InRange(result.Number("next-in"), ResultPostgameSeconds - 1, ResultPostgameSeconds);
            var shown = Patterns.Read(client, "match-state").Last(st => st.Index < result.Index);
            Assert.Equal("Postgame", shown.Text("phase"));
        }
    });

    [Fact, Trait("Scenario", "ResultDraw")]
    public Task Result_is_a_draw_when_no_kill_happens_and_the_screen_hides_at_the_next_warmup() => Run(async s =>
    {
        // Timing assumption: Warmup 30, Playing 60 and Postgame 60 ticks repeat every 2.5 s while a client is
        // connected. Both idle clients (480 and 420 inputs, 8 s and 7 s) overlap the second client's join
        // (within a few seconds of the first) with at least one full Postgame and the Warmup after it.
        await s.Boot("--match-ticks", "30,60,60");
        var first = await s.Client("first", "idle", 480);
        var second = await s.Client("second", "idle", 420);
        await s.Finish(first);
        await s.Finish(second);
        await s.StopServer();
        Assert.Empty(Patterns.Read(s.Server, "match-kill"));
        foreach (var client in new[] { first, second })
        {
            var results = Patterns.Read(client, "result");
            Assert.NotEmpty(results);
            var states = Patterns.Read(client, "match-state");
            Assert.All(results, r =>
            {
                Assert.Equal(("Draw", 0L, 0L), (r.Text("outcome"), r.Number("blue"), r.Number("red")));
                // A result line only follows a Postgame match-state, never any other phase.
                Assert.Equal("Postgame", states.Last(st => st.Index < r.Index).Text("phase"));
            });
            var warmup = states.First(st => st.Index > results[0].Index && st.Text("phase") == "Warmup");
            var hidden = Patterns.Read(client, "board").Where(b => b.Text("view") == "hidden");
            Assert.Contains(hidden, h => h.Index > warmup.Index);
        }
    });

    [Fact, Trait("Scenario", "ResultBoard")]
    public Task Result_screen_lists_the_scoreboard_rows_in_display_order() => Run(async s =>
    {
        // Same timing assumption as ResultWinner (Warmup 30, Playing 360, Postgame 600 ticks; clients 600 and 640 inputs).
        var (shooter, target) = await Fight(s, "fire-hitscan", ticks: 600, serverArgs: ResultMatch);
        var kill = One(s.Server, "match-kill");
        long shooterId = kill.Number("killer"), targetId = kill.Number("victim");
        string shooterTeam = TeamOfEntity(s, shooterId);
        // Blue block first, then Red; each team has one player, so the expected order is by team only.
        string[] expected = shooterTeam == "DevBlue"
            ? [$"DevBlue:{shooterId}:1:0", $"DevRed:{targetId}:0:1"]
            : [$"DevBlue:{targetId}:0:1", $"DevRed:{shooterId}:1:0"];
        foreach (var client in new[] { shooter, target })
        {
            var boards = Patterns.Read(client, "board");
            // Nobody holds the scoreboard key in a scripted client, so the key view never appears.
            Assert.DoesNotContain(boards, b => b.Text("view") == "key");
            var shown = boards.First(b => b.Text("view") == "postgame");
            Assert.Equal(string.Join(",", expected), shown.Text("rows"));
            // The same rows as the client's last two-row scoreboard message, put in display order.
            var last = Rows(Patterns.Read(client, "scoreboard").Last(b => Rows(b).Length == 2));
            string fromMessage = string.Join(",", last.OrderBy(r => r.Faction == "DevBlue" ? 0 : 1).Select(r => $"{r.Faction}:{r.Entity}:{r.Kills}:{r.Deaths}"));
            Assert.Equal(fromMessage, shown.Text("rows"));
        }
    });

    // P39. M5 closing scenarios. Spawn geometry they rely on (World.cs spawns, ArenaProbe wall faces): the
    // straight lines spawn 0 to spawn 1, spawn 0 to spawn 2 and spawn 2 to spawn 3 cross no wall and no other
    // spawn; every scripted player except the "east" one stands still on its spawn.
    private static Vector2 JoinedAt(Scenario s, long entity) =>
        Assert.Single(Patterns.Read(s.Server, "joined"), p => p.Number("entity") == entity).Position;

    private static float AimFrom(Vector2 from, Vector2 to) => MathF.Atan2(to.Y - from.Y, to.X - from.X);

    private static bool SameAim(float a, float b) => MathF.Abs(MathF.IEEERemainder(a - b, 2 * MathF.PI)) < 0.01f;

    [Fact, Trait("Scenario", "FriendlyFireShots")]
    public Task Friendly_fire_shots_and_projectiles_never_damage_a_teammate_but_the_same_gun_damages_an_enemy() => Run(async s =>
    {
        // The shooter (first join, Blue) starts its script only when it sees a teammate and an enemy. It then
        // needs 90 + 7 * 45 = 405 inputs for two instant shots and two projectiles at the teammate and four
        // instant shots at the enemy. Assumption: the two other clients join within about 5 s of the shooter,
        // so its 1000 inputs cover that, and the others (900 inputs each) outlast the shooter's last shot.
        await s.Boot(ShortMatch);
        var shooter = await s.Client("shooter", "ff-fire", 1000);
        var enemy = await s.Client("enemy", "idle", 900);
        var mate = await s.Client("teammate", "idle", 900);
        await s.Server.WaitFor(line => Patterns.Is(line, "defeat"), "defeat", TimeSpan.FromSeconds(30));
        foreach (var client in new[] { shooter, enemy, mate }) await s.Finish(client);
        await s.StopServer();

        var joins = Patterns.Read(s.Server, "match-join");
        Assert.Equal(["DevBlue", "DevRed", "DevBlue"], joins.Select(j => j.Text("team")).ToArray());
        long shooterId = joins[0].Number("entity"), enemyId = joins[1].Number("entity"), mateId = joins[2].Number("entity");
        float mateAim = AimFrom(JoinedAt(s, shooterId), JoinedAt(s, mateId));
        float enemyAim = AimFrom(JoinedAt(s, shooterId), JoinedAt(s, enemyId));

        var shots = Patterns.Read(s.Server, "shot");
        Assert.All(shots, shot => Assert.Equal(shooterId, shot.Number("shooter")));
        var atMate = shots.Where(shot => SameAim(shot.Float("aim"), mateAim)).ToArray();
        var atEnemy = shots.Where(shot => SameAim(shot.Float("aim"), enemyAim)).ToArray();
        Assert.Equal(shots.Length, atMate.Length + atEnemy.Length);
        // At least one of each kind reached the server (two are sent; an input can be lost).
        Assert.Contains(atMate, shot => shot.Text("weapon") == "DevHitscan");
        var projectileShots = atMate.Where(shot => shot.Text("weapon") == "DevProjectile").ToArray();
        Assert.NotEmpty(projectileShots);
        Assert.All(atEnemy, shot => Assert.Equal("DevHitscan", shot.Text("weapon")));
        // No shot was stopped by a wall, so every instant shot reached a player's body.
        Assert.Empty(Patterns.Read(s.Server, "wall"));
        // Every projectile flew into the teammate and was removed there.
        var removed = Patterns.Read(s.Server, "projectile-removed");
        Assert.Equal(projectileShots.Length, removed.Length);
        Assert.All(removed, p => Assert.Equal((shooterId, "player"), (p.Number("owner"), p.Text("reason"))));

        // The only damage is the enemy's: four 25-damage hits, each on the tick of a shot aimed at the enemy.
        var damage = Patterns.Read(s.Server, "damage");
        Assert.Equal(4, damage.Length);
        Assert.All(damage, d => Assert.Equal((shooterId, enemyId, 25L), (d.Number("source"), d.Number("target"), d.Number("amount"))));
        Assert.All(damage, d => Assert.Contains(atEnemy, shot => shot.Number("tick") == d.Number("tick")));
        Assert.All(atMate, shot => Assert.DoesNotContain(damage, d => d.Number("tick") == shot.Number("tick")));
        Assert.True(atMate.Max(shot => shot.Index) < damage[0].Index, "the teammate shots come before the first enemy hit");
        Assert.Equal(enemyId, One(s.Server, "defeat").Number("target"));
        var kill = One(s.Server, "match-kill");
        Assert.Equal(("true", 1L, 0L), (kill.Text("scored"), kill.Number("blue"), kill.Number("red")));
        foreach (var client in new[] { shooter, enemy, mate })
        {
            Assert.All(Patterns.Read(client, "event"), e => Assert.Equal(enemyId, e.Number("target")));
            var rows = Rows(Patterns.Read(client, "scoreboard").Last(b => Rows(b).Length == 3));
            Assert.Equal([(shooterId, "DevBlue", 1L, 0L), (enemyId, "DevRed", 0L, 1L), (mateId, "DevBlue", 0L, 0L)], rows);
        }
    });

    [Fact, Trait("Scenario", "FriendlyFireGrenade")]
    public Task Friendly_fire_grenade_blast_does_not_damage_a_teammate_inside_the_radius() => Run(async s =>
    {
        // The fourth joiner (Red, spawn 3) throws at its teammate on spawn 1, 160 units away. The grenade lands
        // 248 units from the thrower, so 88 units from the teammate, inside the 96-unit radius.
        // Assumption: the thrower's 420 inputs cover 90 aim ticks, the throw and the 90-tick fuse; the three
        // idle clients (1000, 900 and 800 inputs) outlast it because each joins within a few seconds.
        await s.Boot();
        var blueFirst = await s.Client("blue-first", "idle", 1000);
        var redMate = await s.Client("red-teammate", "idle", 900);
        var blueSecond = await s.Client("blue-second", "idle", 800);
        var thrower = await s.Client("red-thrower", "throw-grenade", 420, "--net-target", "teammate", "--net-wait-players", "4");
        foreach (var client in new[] { thrower, blueSecond, redMate, blueFirst }) await s.Finish(client);
        await s.StopServer();

        var joins = Patterns.Read(s.Server, "match-join");
        Assert.Equal(["DevBlue", "DevRed", "DevBlue", "DevRed"], joins.Select(j => j.Text("team")).ToArray());
        long mateId = joins[1].Number("entity"), throwerId = joins[3].Number("entity");
        var created = One(s.Server, "grenade-created");
        Assert.Equal(throwerId, created.Number("owner"));
        var detonated = One(s.Server, "grenade-detonated");
        var targets = Patterns.Read(s.Server, "grenade-target");
        Assert.Equal(4, targets.Length);
        var mate = Assert.Single(targets, t => t.Number("target") == mateId);
        // The teammate is close enough that an enemy standing there would be damaged...
        Assert.InRange(mate.Float("distance"), 0f, GrenadeDef.DevPlaceholder.RadiusUnits);
        Assert.True(DamageMath.RadiusDamage(GrenadeDef.DevPlaceholder, mate.Float("distance")) > 0);
        Assert.InRange(Vector2.Distance(detonated.Position, JoinedAt(s, mateId)), mate.Float("distance") - 0.5f, mate.Float("distance") + 0.5f);
        // ...and the server still gives it nothing.
        Assert.Equal(("False", 0L), (mate.Text("eligible"), mate.Number("amount")));
        foreach (var target in targets.Where(t => t.Number("target") != mateId && t.Number("target") != throwerId))
        {
            // The two Blue players may be damaged by rule; here they stand outside the radius.
            Assert.Equal("True", target.Text("eligible"));
            Assert.Equal(DamageMath.RadiusDamage(GrenadeDef.DevPlaceholder, target.Float("distance")), target.Number("amount"));
        }
        Assert.Empty(Patterns.Read(s.Server, "damage"));
        Assert.Empty(Patterns.Read(s.Server, "defeat"));
        foreach (var client in new[] { thrower, blueSecond, redMate, blueFirst }) Assert.Empty(Patterns.Read(client, "event"));
    });

    [Fact, Trait("Scenario", "RespawnSkipsOccupied")]
    public Task Respawn_skips_the_team_spawn_a_teammate_stands_on() => Run(async s =>
    {
        // The third joiner (Blue, spawn 2) shoots the fourth (Red, spawn 3). Red's first spawn (1) is held by
        // the second joiner, so the defeated player must come back on spawn 3.
        // Assumption: the fourth client joins within about 5 s of the third, so the shooter's 900 inputs cover
        // the 90 aim ticks and four shots (150 ticks) after it; every other client is still connected then.
        await s.Boot();
        var blueFirst = await s.Client("blue-first", "idle", 1100);
        var redFirst = await s.Client("red-first", "idle", 1000);
        var shooter = await s.Client("blue-shooter", "fire-hitscan", 900, "--net-target", "last-enemy", "--net-wait-players", "4");
        var victim = await s.Client("red-victim", "idle", 700);
        foreach (var client in new[] { victim, shooter, redFirst, blueFirst }) await s.Finish(client);
        await s.StopServer();

        var spawns = ArenaSpawns(s);
        var joined = Patterns.Read(s.Server, "joined");
        Assert.Equal([0L, 1L, 2L, 3L], joined.Select(j => j.Number("spawn")).ToArray());
        long redFirstId = joined[1].Number("entity"), shooterId = joined[2].Number("entity"), victimId = joined[3].Number("entity");
        var defeat = One(s.Server, "defeat");
        Assert.Equal((shooterId, victimId), (defeat.Number("source"), defeat.Number("target")));
        Assert.All(Patterns.Read(s.Server, "damage"), d => Assert.Equal(victimId, d.Number("target")));
        var respawn = One(s.Server, "respawn");
        Assert.Equal(victimId, respawn.Number("entity"));
        // Oracle: the shared spawn rule over Red's spawn list and where the other three players stand.
        var others = joined.Where(j => j.Number("entity") != victimId).Select(j => j.Position).ToArray();
        Assert.Equal(1, SpawnRules.ChooseSpawn([spawns[1], spawns[3]], others));
        Assert.Equal(spawns[1], JoinedAt(s, redFirstId));
        Assert.Equal(spawns[3], respawn.Position);
        Assert.All(others, other => Assert.False(Overlap(other, respawn.Position)));
    });

    [Fact, Trait("Scenario", "TenKills")]
    public Task Ten_kills_add_up_in_team_scores_and_scoreboard_rows() => Run(async s =>
    {
        // Both clients shoot each other through respawns: the first (Blue) until it has 6 kills, the second
        // (Red) until it has 4. A kill takes four shots 20 ticks apart, so 6 kills need about 90 + 6 * 80 = 570
        // ticks after the second join. Assumption: the second client joins within about 5 s, so 1200 and 1240
        // inputs leave room for lost inputs. Playing starts 30 ticks after the first join, before any shot.
        await s.Boot(ShortMatch);
        var blue = await s.Client("blue", "fire-until", 1200, "--net-kills", "6");
        var red = await s.Client("red", "fire-until", 1240, "--net-kills", "4");
        await s.Finish(blue);
        await s.Finish(red);
        await s.StopServer();
        Counters(s, blue);
        Counters(s, red);

        var joins = Patterns.Read(s.Server, "match-join");
        Assert.Equal(["DevBlue", "DevRed"], joins.Select(j => j.Text("team")).ToArray());
        long blueId = joins[0].Number("entity"), redId = joins[1].Number("entity");
        var kills = Patterns.Read(s.Server, "match-kill");
        Assert.Equal(10, kills.Length);
        Assert.All(kills, k => Assert.Equal(("true", "Playing"), (k.Text("scored"), k.Text("phase"))));
        Assert.Equal(10, Patterns.Read(s.Server, "defeat").Length);
        for (int i = 0; i < kills.Length; i++)
        {
            // After every kill the two team scores are exactly the kills so far, split by the killer's team.
            Assert.Equal((long)kills.Take(i + 1).Count(k => k.Number("killer") == blueId), kills[i].Number("blue"));
            Assert.Equal((long)kills.Take(i + 1).Count(k => k.Number("killer") == redId), kills[i].Number("red"));
        }
        Assert.Equal((6L, 4L), (kills[^1].Number("blue"), kills[^1].Number("red")));
        Assert.Equal(6, kills.Count(k => k.Number("killer") == blueId && k.Number("victim") == redId));
        Assert.Equal(4, kills.Count(k => k.Number("killer") == redId && k.Number("victim") == blueId));
        foreach (var client in new[] { blue, red })
        {
            var rows = Rows(Patterns.Read(client, "scoreboard").Last(b => Rows(b).Length == 2));
            Assert.Equal([(blueId, "DevBlue", 6L, 4L), (redId, "DevRed", 4L, 6L)], rows);
            var state = Patterns.Read(client, "match-state").Last();
            Assert.Equal(("Playing", 6L, 4L), (state.Text("phase"), state.Number("blue"), state.Number("red")));
            var hud = Patterns.Read(client, "hud").Last();
            Assert.Equal((6L, 4L), (hud.Number("blue"), hud.Number("red")));
            // The totals a client was told never go down and end at ten (two kills on one tick share a message).
            long[] totals = Patterns.Read(client, "match-state").Select(st => st.Number("blue") + st.Number("red")).ToArray();
            Assert.Equal(totals.Order().ToArray(), totals);
            Assert.Equal(10, totals[^1]);
        }
    });

    [Fact, Trait("Scenario", "MatchKillInPostgame")]
    public Task Match_kill_during_postgame_does_not_score() => Run(async s =>
    {
        // Postgame starts 60 ticks after the first join. The shooter's first shot comes 90 ticks after it sees
        // the target at the earliest, so the kill always lands in the 3000-tick Postgame.
        var (shooter, target) = await Fight(s, "fire-hitscan", serverArgs: ["--match-ticks", "30,30,3000"]);
        Assert.Single(Patterns.Read(s.Server, "defeat"));
        var kill = One(s.Server, "match-kill");
        Assert.Equal(("false", "Postgame", 0L, 0L), (kill.Text("scored"), kill.Text("phase"), kill.Number("blue"), kill.Number("red")));
        foreach (var client in new[] { shooter, target })
        {
            Assert.All(Patterns.Read(client, "scoreboard"), b => Assert.All(Rows(b), r => Assert.Equal((0L, 0L), (r.Kills, r.Deaths))));
            Assert.All(Patterns.Read(client, "match-state"), st => Assert.Equal((0L, 0L), (st.Number("blue"), st.Number("red"))));
            Assert.Equal(("Draw", 0L, 0L), (One(client, "result").Text("outcome"), One(client, "result").Number("blue"), One(client, "result").Number("red")));
        }
    });

    [Fact, Trait("Scenario", "FullMatch")]
    public Task Full_match_ends_and_the_next_match_starts_from_zero() => Run(async s =>
    {
        // Warmup 30, Playing 360, Postgame 120 ticks: the second match's Warmup starts 510 ticks (8.5 s) and
        // its Playing 540 ticks after the first join. Timing assumption as in ResultWinner for the kill (in the
        // first Playing); both clients (900 and 940 inputs) stay connected past tick 540.
        var (shooter, target) = await Fight(s, "fire-hitscan", ticks: 900, serverArgs: ["--match-ticks", "30,360,120"]);
        var phases = Patterns.Read(s.Server, "match-phase");
        Assert.Equal(["Idle>Warmup", "Warmup>Playing", "Playing>Postgame", "Postgame>Warmup", "Warmup>Playing"],
            phases.Take(5).Select(p => $"{p.Text("from")}>{p.Text("to")}").ToArray());
        var kill = One(s.Server, "match-kill");
        Assert.Equal(("true", "Playing"), (kill.Text("scored"), kill.Text("phase")));
        Assert.InRange(kill.Number("tick"), phases[1].Number("tick"), phases[2].Number("tick") - 1);
        Assert.Equal(1, kill.Number("blue") + kill.Number("red"));
        // The match ends with the kill's score, and the next match starts at 0-0 on the exact tick.
        Assert.Equal((kill.Number("blue"), kill.Number("red")), (phases[2].Number("blue"), phases[2].Number("red")));
        Assert.Equal((0L, 0L), (phases[3].Number("blue"), phases[3].Number("red")));
        Assert.Equal(120, phases[3].Number("tick") - phases[2].Number("tick"));
        long next = phases[3].Number("tick");
        var resets = Patterns.Read(s.Server, "match-reset");
        Assert.Contains(resets, r => (r.Number("tick"), r.Text("from"), r.Text("to"), r.Number("players")) == (next, "Postgame", "Warmup", 2L));
        Assert.Contains(resets, r => (r.Number("tick"), r.Text("from"), r.Text("to")) == (phases[4].Number("tick"), "Warmup", "Playing"));
        Assert.Empty(Patterns.Read(s.Server, "team-move"));
        Assert.Empty(Patterns.Read(s.Server, "team-mismatch"));
        string winner = TeamOfEntity(s, kill.Number("killer")) == "DevBlue" ? "BlueWins" : "RedWins";
        foreach (var client in new[] { shooter, target })
        {
            var state = Assert.Single(Patterns.Read(client, "match-state"), st => st.Number("tick") == next);
            // The first match's result. A client still connected 900 ticks after the first join also sees the
            // second match end, which this scenario does not judge.
            var result = Assert.Single(Patterns.Read(client, "result"), r => r.Index < state.Index);
            Assert.Equal((winner, kill.Number("blue"), kill.Number("red")), (result.Text("outcome"), result.Number("blue"), result.Number("red")));
            Assert.Equal(("Warmup", 0L, 0L), (state.Text("phase"), state.Number("blue"), state.Number("red")));
            var board = Assert.Single(Patterns.Read(client, "scoreboard"), b => b.Number("tick") == next);
            Assert.Equal(2, Rows(board).Length);
            Assert.All(Rows(board), r => Assert.Equal((0L, 0L), (r.Kills, r.Deaths)));
            // The end-of-match screen goes away and the HUD shows the new match from zero.
            Assert.Contains(Patterns.Read(client, "board"), b => b.Text("view") == "hidden" && b.Index > state.Index);
            var huds = Patterns.Read(client, "hud").Where(h => h.Index > state.Index).ToArray();
            Assert.Contains(huds, h => h.Text("phase") == "Warmup");
            Assert.Contains(huds, h => h.Text("phase") == "Playing");
            Assert.All(huds, h => Assert.Equal((0L, 0L), (h.Number("blue"), h.Number("red"))));
        }
    });

    [Fact, Trait("Scenario", "LateJoin")]
    public Task Late_joiner_receives_the_current_phase_scores_and_rows() => Run(async s =>
    {
        // The third client is started only after the server has logged the kill, so it always joins a match
        // that already has a score. The first two clients (1500 inputs, 25 s) outlast its 180 inputs.
        // Assumption: the kill and the third client's start-up fit inside those 25 s.
        await s.Boot(ShortMatch);
        var shooter = await s.Client("shooter", "fire-hitscan", 1500);
        var target = await s.Client("target", "idle", 1500);
        await s.Server.WaitFor(line => Patterns.Is(line, "match-kill"), "match-kill", TimeSpan.FromSeconds(30));
        var late = await s.Client("late", "idle", 180);
        await s.Finish(late);
        await s.Finish(shooter);
        await s.Finish(target);
        await s.StopServer();

        var kill = One(s.Server, "match-kill");
        Assert.Equal(("true", "Playing", 1L, 0L), (kill.Text("scored"), kill.Text("phase"), kill.Number("blue"), kill.Number("red")));
        var joins = Patterns.Read(s.Server, "match-join");
        Assert.True(joins[2].Index > kill.Index, "the late client must join after the kill");
        // One Blue and one Red: the tie goes to Blue, and Blue's first spawn is taken by the shooter.
        Assert.Equal("DevBlue", joins[2].Text("team"));
        long lateId = joins[2].Number("entity");
        Assert.Equal(2, Assert.Single(Patterns.Read(s.Server, "joined"), j => j.Number("entity") == lateId).Number("spawn"));
        var playing = Assert.Single(Patterns.Read(s.Server, "match-phase"), p => p.Text("to") == "Playing");

        var first = Patterns.Read(late, "match-state")[0];
        Assert.Equal(("Playing", 1L, 0L, playing.Number("end-tick")),
            (first.Text("phase"), first.Number("blue"), first.Number("red"), first.Number("end-tick")));
        Assert.True(One(late, "welcome").Index < first.Index);
        var rows = Rows(Patterns.Read(late, "scoreboard")[0]);
        Assert.Equal([(kill.Number("killer"), "DevBlue", 1L, 0L), (kill.Number("victim"), "DevRed", 0L, 1L), (lateId, "DevBlue", 0L, 0L)], rows);
        var hud = Patterns.Read(late, "hud").Last();
        Assert.Equal(("Playing", 1L, 0L, "DevBlue"), (hud.Text("phase"), hud.Number("blue"), hud.Number("red"), hud.Text("team")));
        // The clients already connected get the new row too.
        foreach (var client in new[] { shooter, target })
            Assert.Contains(Patterns.Read(client, "scoreboard"), b => Rows(b).SequenceEqual(rows));
    });

    [Fact, Trait("Scenario", "ResetClearsAttacks")]
    public Task Reset_removes_flying_attacks_and_clears_the_grenade_cooldown() => Run(async s =>
    {
        // One client, Warmup 240 ticks. The script reads the server's own phase end tick, so the throw and the
        // shot are placed relative to the reset and need no wall-clock assumption.
        await s.Boot("--match-ticks", "240,3000,30");
        var client = await s.Client("client", "reset-clear", 540);
        await s.Finish(client);
        await s.StopServer();

        var reset = One(s.Server, "match-reset");
        Assert.Equal(("Warmup", "Playing"), (reset.Text("from"), reset.Text("to")));
        long at = reset.Number("tick");
        long fuse = CombatRules.CooldownTicks(GrenadeDef.DevPlaceholder.FuseSeconds);
        var grenades = Patterns.Read(s.Server, "grenade-created");
        Assert.Equal(2, grenades.Length);
        // The first grenade was thrown less than one fuse before the reset and never explodes.
        Assert.InRange(grenades[0].Number("tick"), at - fuse + 1, at - 1);
        var detonated = One(s.Server, "grenade-detonated");
        Assert.Equal(grenades[1].Number("entity"), detonated.Number("entity"));
        // The grenade cooldown equals the fuse. A second throw sooner than that proves the reset cleared it.
        Assert.InRange(grenades[1].Number("tick"), at, grenades[0].Number("tick") + fuse - 1);
        Assert.Equal(fuse, detonated.Number("tick") - grenades[1].Number("tick"));
        // Every projectile fired before the reset is gone without a wall, player or range removal.
        var projectiles = Patterns.Read(s.Server, "projectile-created");
        var before = projectiles.Where(p => p.Number("tick") < at).ToArray();
        Assert.NotEmpty(before);
        var removed = Patterns.Read(s.Server, "projectile-removed");
        Assert.All(before, p => Assert.DoesNotContain(removed, r => r.Number("entity") == p.Number("entity")));
        Assert.Equal(projectiles.Length - before.Length, removed.Length);
    });

    [Fact, Trait("Scenario", "ResetMovesPlayerBack")]
    public Task Reset_puts_a_player_who_walked_away_back_on_the_team_spawn() => Run(async s =>
    {
        // One client walks east for the whole 150-tick Warmup (the wall is reached after about 61 ticks).
        await s.Boot("--match-ticks", "150,3000,30");
        var client = await s.Client("client", "east", 400);
        await s.Finish(client);
        await s.StopServer();

        var spawns = ArenaSpawns(s);
        var reset = One(s.Server, "match-reset");
        Assert.Equal(("Warmup", "Playing"), (reset.Text("from"), reset.Text("to")));
        var before = Patterns.Read(s.Server, "position").Last(p => p.Number("tick") < reset.Number("tick"));
        // P26 D3 wall position: the 32-wide body's right edge is within 1 unit of the wall at x=258.
        Assert.InRange(before.Position.X + 16, 257, 258);
        var player = One(s.Server, "match-reset-player");
        Assert.Equal((reset.Number("tick"), 0L, 100L), (player.Number("tick"), player.Number("spawn"), player.Number("health")));
        Assert.Equal(spawns[0], player.Position);
        // The player keeps walking after the reset, so it leaves the spawn again rather than staying frozen.
        Assert.InRange(One(s.Server, "departed").Position.X + 16, 257, 258);
    });

    [Fact, Trait("Scenario", "BoardHeld")]
    public Task Holding_the_scoreboard_action_shows_the_board_and_releasing_hides_it() => Run(async s =>
    {
        // The script presses the client's "scoreboard" input action at input 120 and releases it at input 240.
        // The phases are too long to end (3000 ticks), so no end-of-match screen can appear.
        await s.Boot();
        var client = await s.Client("client", "hold-board", 360);
        await s.Finish(client);
        await s.StopServer();
        Counters(s, client);

        var join = One(s.Server, "match-join");
        var boards = Patterns.Read(client, "board");
        Assert.Equal(["key", "hidden"], boards.Select(b => b.Text("view")).ToArray());
        Assert.Equal($"{join.Text("team")}:{join.Number("entity")}:0:0", boards[0].Text("rows"));
        Assert.Empty(Patterns.Read(client, "result"));
        // Holding the action sends nothing to the server: no shot, no grenade, no movement.
        Assert.Empty(Patterns.Read(s.Server, "shot"));
        Assert.Empty(Patterns.Read(s.Server, "grenade-created"));
        Assert.Equal(JoinedAt(s, join.Number("entity")), One(s.Server, "departed").Position);
    });

    [Theory, Trait("Scenario", "LocalClick")]
    [InlineData("local-click")] // button held for 6 ticks
    [InlineData("local-tap")]   // press and release inside one tick, like a trackpad tap
    public Task Left_button_events_read_through_the_manual_input_path_fire_one_shot_each(string script) => Run(async s =>
    {
        // The script feeds real left-mouse-button events to the engine at inputs 120, 180, ... 660 (ten clicks)
        // and builds each input with the same code as manual play. No timing assumption: one client only.
        await s.Boot();
        var client = await s.Client("client", script, 700);
        await s.Finish(client);
        await s.StopServer();
        Counters(s, client);
        var shots = Patterns.Read(s.Server, "shot");
        // P28 R4 loss floor: a click whose input is lost is not fired, so allow two of ten to be missing.
        Assert.InRange(shots.Length, 8, 10);
        Assert.All(shots, shot => Assert.Equal("DevHitscan", shot.Text("weapon")));
        // One shot per click: consecutive shots are a whole number of 60-tick click periods apart.
        foreach (var pair in shots.Zip(shots.Skip(1)))
            Assert.InRange((pair.Second.Number("tick") - pair.First.Number("tick") + 5) % 60, 0, 10);
        Assert.Empty(Patterns.Read(s.Server, "grenade-created"));
    });
}
