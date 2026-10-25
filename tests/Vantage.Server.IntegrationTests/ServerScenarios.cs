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

    private static async Task<(ProcessRunner Shooter, ProcessRunner Target)> Fight(Scenario s, string script, int ticks = 330, string targetScript = "idle")
    {
        await s.Boot();
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
}
