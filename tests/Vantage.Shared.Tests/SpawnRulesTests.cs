using System.Numerics;
using Vantage.Shared.Game;
using Xunit;
namespace Vantage.Shared.Tests;

public sealed class SpawnRulesTests
{
    private static readonly Vector2 A = new(0, 0);
    private static readonly Vector2 B = new(100, 0);
    private static readonly Vector2 C = new(200, 0);

    [Fact]
    public void Body_size_is_the_32_unit_player_body() => Assert.Equal(32f, SpawnRules.BodySize);

    [Fact]
    public void No_other_players_gives_the_first_spawn() =>
        Assert.Equal(0, SpawnRules.ChooseSpawn([A, B, C], []));

    [Fact]
    public void First_spawn_occupied_gives_the_second() =>
        Assert.Equal(1, SpawnRules.ChooseSpawn([A, B, C], [A]));

    [Fact]
    public void First_two_occupied_gives_the_third() =>
        Assert.Equal(2, SpawnRules.ChooseSpawn([A, B, C], [A, B]));

    [Fact]
    public void Only_a_later_spawn_occupied_still_gives_the_first() =>
        Assert.Equal(0, SpawnRules.ChooseSpawn([A, B, C], [B]));

    [Fact]
    public void All_occupied_falls_back_to_the_first_spawn() =>
        Assert.Equal(0, SpawnRules.ChooseSpawn([A, B, C], [A, B, C]));

    [Theory]
    [InlineData(32f, 0f)]
    [InlineData(-32f, 0f)]
    [InlineData(0f, 32f)]
    [InlineData(0f, -32f)]
    [InlineData(32f, 32f)]
    public void A_position_exactly_one_body_away_does_not_occupy(float dx, float dy) =>
        Assert.Equal(0, SpawnRules.ChooseSpawn([A, B], [new Vector2(dx, dy)]));

    [Theory]
    [InlineData(31.9f, 0f)]
    [InlineData(-31.9f, 0f)]
    [InlineData(0f, 31.9f)]
    [InlineData(0f, -31.9f)]
    [InlineData(31.9f, -31.9f)]
    public void A_position_just_inside_the_box_occupies(float dx, float dy) =>
        Assert.Equal(1, SpawnRules.ChooseSpawn([A, B], [new Vector2(dx, dy)]));

    [Theory]
    [InlineData(10f, 32f)]
    [InlineData(32f, 10f)]
    [InlineData(10f, 500f)]
    [InlineData(500f, 10f)]
    public void Within_the_box_on_one_axis_only_does_not_occupy(float dx, float dy) =>
        Assert.Equal(0, SpawnRules.ChooseSpawn([A, B], [new Vector2(dx, dy)]));

    [Fact]
    public void A_player_standing_on_another_teams_spawn_still_occupies()
    {
        // The caller passes every other player, whatever their team; the rule does not know teams.
        Vector2 otherTeamSpawn = new(500, 500);
        Assert.Equal(1, SpawnRules.ChooseSpawn([otherTeamSpawn, B], [otherTeamSpawn]));
    }

    [Fact]
    public void A_single_spawn_list_returns_zero_occupied_or_not()
    {
        Assert.Equal(0, SpawnRules.ChooseSpawn([A], []));
        Assert.Equal(0, SpawnRules.ChooseSpawn([A], [A]));
    }

    [Fact]
    public void An_empty_spawn_list_is_rejected() =>
        Assert.Throws<ArgumentException>(() => SpawnRules.ChooseSpawn([], []));

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => SpawnRules.ChooseSpawn(null!, []));
        Assert.Throws<ArgumentNullException>(() => SpawnRules.ChooseSpawn([A], null!));
    }
}
