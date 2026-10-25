using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Vantage.Server.IntegrationTests;

// All program wording and parsing lives here. Assertions use named fields only.
internal static class Patterns
{
    private static readonly Dictionary<string, string> Prefixes = new()
    {
        ["port"] = "[Server] port=", ["final"] = "[Server] final ",
        ["invalid-port"] = "[Server] error: --port requires an integer from 1 to 65535.",
        ["listening"] = "[NetServer] listening ", ["arena"] = "[Arena] spawn=",
        ["joined"] = "[World] joined ", ["departed"] = "[World] departed ",
        ["position"] = "[World] tick=", ["counters"] = "[NetServer] disconnected (client left); final ",
        ["shutdown-peer"] = "[NetServer] disconnected (server shutdown); final ",
        ["connected"] = "[GameClient] connected ", ["welcome"] = "[GameClient] welcome ",
        ["smoke"] = "[GameClient] smoke complete; ", ["snapshot"] = "[GameClient] last snapshot ",
        ["server-lost"] = "[GameClient] server disconnected; ",
        ["event"] = "[GameClient] event=", ["shot"] = "[Combat] shot ",
        ["wall"] = "[Combat] wall-block ", ["damage"] = "[Combat] damage ",
        ["defeat"] = "[Combat] defeat ", ["respawn"] = "[Combat] respawn ",
        ["projectile-created"] = "[Combat] projectile-created ",
        ["projectile-removed"] = "[Combat] projectile-removed ",
        ["grenade-created"] = "[Combat] grenade-created ",
        ["grenade-detonated"] = "[Combat] grenade-detonated ",
        ["grenade-target"] = "[Combat] grenade-target ",
    };
    private static readonly Regex Fields = new(@"(?:^|; )(?<key>[\w -]+)=(?<value>[^;]*)", RegexOptions.CultureInvariant);
    public static bool Is(string line, string kind) => line.StartsWith(Prefixes[kind], StringComparison.Ordinal);
    public static Entry[] Read(ProcessRunner process, string kind) => process.Lines
        .Select((line, index) => (line, index)).Where(x => Is(x.line, kind))
        .Select(x => new Entry(x.index, Fields.Matches(x.line[(x.line.IndexOf(']') + 2)..]
            .Replace("smoke complete; ", "").Replace("last snapshot ", "")
            .Replace("disconnected (client left); final ", "")
            .Replace("disconnected (server shutdown); final ", "")
            .Replace("final tick=", "tick=")
            .Replace(" (--port)", "")
            // Strip the event name before the first key (joined peer, shot tick, etc.).
            .TrimStart())
            .ToDictionary(m => Normalize(m.Groups["key"].Value), m => m.Groups["value"].Value)))
        .ToArray();

    private static string Normalize(string key) => key switch
    {
        "joined peer" or "departed peer" or "connected peer" => "peer",
        "welcome entity" => "entity",
        "shot tick" or "wall-block tick" or "damage tick" or "defeat tick" or "respawn tick"
            or "projectile-created tick" or "projectile-removed tick" or "grenade-created tick"
            or "grenade-detonated tick" or "grenade-target tick" => "tick",
        _ => key,
    };

    internal sealed record Entry(int Index, Dictionary<string, string> Values)
    {
        public string Text(string key) => Values[key];
        public long Number(string key) => long.Parse(Text(key), CultureInfo.InvariantCulture);
        public float Float(string key) => float.Parse(Text(key), CultureInfo.InvariantCulture);
        public Vector2 Position => Vector("position");
        public Vector2 Vector(string key)
        {
            string[] parts = Text(key).Trim('(', ')', '<', '>').Split(',');
            return new(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture));
        }
    }
}
