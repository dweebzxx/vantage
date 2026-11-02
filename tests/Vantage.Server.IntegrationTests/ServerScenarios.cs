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
}
