using Godot;
using Vantage.Shared.Game;
using Vantage.Shared.Protocol;
namespace Vantage.Server;

// The only server object that owns the MatchState. World calls it at fixed points in the tick.
public sealed class MatchHost(MatchTiming timing)
{
    private readonly MatchState _state = new(timing);
    private bool _changed;

    public MatchTiming Timing => _state.Timing;

    // Called once per tick, first thing after Events.Clear().
    // Returns the transition made this tick, if any. TeamMoves is valid until the next Advance.
    public MatchTransition? Advance(uint tick)
    {
        MatchTransition? transition = _state.Advance();
        if (transition is not { } made) return null;
        _changed = true;
        GD.Print($"[Match] phase tick={tick}; from={made.From}; to={made.To}; end-tick={PhaseEndTick(tick)}; blue={_state.Score.BlueScore}; red={_state.Score.RedScore}");
        return made;
    }

    // The rebalance moves made by the latest Advance (already applied to the MatchState rows).
    public IReadOnlyList<TeamMove> TeamMoves => _state.LastTeamMoves;

    // The team the next joiner will get. World places the new player on it before registering.
    public Faction ChooseTeamForNewPlayer() => _state.ChooseTeamForNewPlayer();

    public void AddPlayer(uint entityId)
    {
        Faction team = _state.ChooseTeamForNewPlayer();
        _state.AddPlayer(entityId, team);
        _changed = true;
        GD.Print($"[Match] join entity={entityId}; team={team}; players={_state.PlayerCount}");
    }

    public void RemovePlayer(uint entityId)
    {
        _state.RemovePlayer(entityId);
        _changed = true;
        GD.Print($"[Match] leave entity={entityId}; players={_state.PlayerCount}");
    }

    // Returns true when the kill scored (only while Playing).
    public bool RecordKill(uint tick, uint killerEntityId, Faction killerFaction, uint victimEntityId, Faction victimFaction)
    {
        bool scored = _state.RecordKill(killerEntityId, killerFaction, victimEntityId, victimFaction);
        if (scored) _changed = true;
        GD.Print($"[Match] kill tick={tick}; killer={killerEntityId}; victim={victimEntityId}; scored={(scored ? "true" : "false")}; phase={_state.Phase}; blue={_state.Score.BlueScore}; red={_state.Score.RedScore}");
        return scored;
    }

    // Called last in the tick. Appends nothing unless the match state changed since the last broadcast.
    public void AppendMessages(uint tick, List<byte[]> events)
    {
        if (!_changed) return;
        _changed = false;
        events.Add(MessageCodec.EncodeMatchState(new(tick, _state.Phase, PhaseEndTick(tick), _state.Score.BlueScore, _state.Score.RedScore)));
        ScoreboardRow[] rows = _state.Score.Players.Select(p => new ScoreboardRow(p.EntityId, p.Team, p.Kills, p.Deaths)).ToArray();
        events.Add(MessageCodec.EncodeScoreboard(new(tick, rows)));
    }

    private uint PhaseEndTick(uint tick) => _state.Phase == MatchPhase.Idle ? tick : unchecked(tick + _state.TicksRemaining);
}
