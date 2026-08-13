/// <summary>
/// Thrown when a tournament structure cannot be parsed or fails validation.
/// The message is written to be safe (and useful) to surface directly to an executive.
/// </summary>
public class TournamentFormatException(string message) : Exception(message);

/// <summary>
/// A structured reference to one side of a series matchup.
/// Serializes to the compact <see cref="RoundSeries.Matchup"/> encoding.
/// </summary>
/// <param name="Type">One of Seed, Winner, Loser, Reseed.</param>
/// <param name="Number">
/// For Seed: the initial seed number. For Winner/Loser: the series <see cref="RoundSeries.Number"/>
/// whose winner/loser fills this spot. For Reseed: the rank among teams still alive.
/// </param>
public record SeriesSpotRef(string Type, int Number)
{
    public const string Seed = "Seed";
    public const string Winner = "Winner";
    public const string Loser = "Loser";
    public const string Reseed = "Reseed";
}

/// <summary>
/// A structured seeding rule: "output seeds X through Y come from ranks A through B of some source".
/// Serializes to one semicolon-delimited group of <see cref="TournamentBracket.SeedingConfiguration"/>.
/// </summary>
public record SeedGroup(
    int OutputStart,
    int OutputEnd,
    string Result,
    string SourceType,
    long SourceID,
    int RankStart,
    int RankEnd)
{
    public const string ResultStandings = "Standings";
    public const string ResultLosers = "Losers";

    public const string SourceSeason = "Season";
    public const string SourceBracketRound = "BracketRound";
    public const string SourceRoundRobin = "TournamentRoundRobin";
}

/// <summary>
/// Converts between the structured tournament shapes used by the API and the compact
/// string encodings stored in the database.
///
/// <para>
/// These encodings are a storage detail. Nothing outside this class (and the seeding
/// source classes that read them) should build or interpret them by hand.
/// </para>
///
/// <para><b>Matchup</b> (<see cref="RoundSeries.Matchup"/>) — two dash-separated spot refs,
/// each a source character followed by a number:
/// <c>#</c> initial seed, <c>w</c> winner of series N, <c>l</c> loser of series N,
/// <c>r</c> rank N among remaining teams. e.g. <c>#1-#8</c>, <c>w5-w6</c>, <c>r1-r4</c>.
/// </para>
///
/// <para><b>HostOrder</b> (<see cref="RoundSeries.HostOrder"/>) — one digit per game in the
/// series, naming which spot hosts that game. <c>121</c> is a three game series where spot 1
/// hosts games 1 and 3. The length of the string is the length of the series.
/// </para>
///
/// <para><b>SeedingConfiguration</b> (<see cref="TournamentBracket.SeedingConfiguration"/>) —
/// semicolon-separated groups of <c>outputStart-outputEnd,Result,SourceType:SourceID:rankStart-rankEnd</c>.
/// e.g. <c>1-8,Standings,Season:13:1-8</c> seeds 1 through 8 from the top 8 of season 13.
/// A null or empty value means "seed from this year's regular season standings".
/// </para>
/// </summary>
public static class TournamentFormats
{
    public const string BracketFormatFixed = "Fixed";
    public const string BracketFormatReseed = "Re-seed";

    public const string SeriesFormatBestOf = "Best of";
    public const string SeriesFormatAggregate = "Aggregate";

    static readonly Dictionary<char, string> SpotSourceToType = new()
    {
        ['#'] = SeriesSpotRef.Seed,
        ['w'] = SeriesSpotRef.Winner,
        ['l'] = SeriesSpotRef.Loser,
        ['r'] = SeriesSpotRef.Reseed,
    };

    static readonly Dictionary<string, char> SpotTypeToSource =
        SpotSourceToType.ToDictionary(kv => kv.Value, kv => kv.Key);

    // ---------------------------------------------------------------- Matchup

    public static (SeriesSpotRef Spot1, SeriesSpotRef Spot2) ParseMatchup(string matchup)
    {
        if (string.IsNullOrWhiteSpace(matchup))
            throw new TournamentFormatException("A series matchup is missing.");

        var parts = matchup.Split('-');
        if (parts.Length != 2)
            throw new TournamentFormatException($"Series matchup \"{matchup}\" must name exactly two spots.");

        return (ParseSpot(parts[0], matchup), ParseSpot(parts[1], matchup));
    }

    static SeriesSpotRef ParseSpot(string spot, string matchup)
    {
        if (spot.Length < 2)
            throw new TournamentFormatException($"Series matchup \"{matchup}\" has an incomplete spot \"{spot}\".");

        if (!SpotSourceToType.TryGetValue(spot[0], out var type))
            throw new TournamentFormatException($"Series matchup \"{matchup}\" uses unknown spot source '{spot[0]}'.");

        if (!int.TryParse(spot[1..], out var number) || number < 1)
            throw new TournamentFormatException($"Series matchup \"{matchup}\" has an invalid spot number \"{spot[1..]}\".");

        return new SeriesSpotRef(type, number);
    }

    public static string SerializeMatchup(SeriesSpotRef spot1, SeriesSpotRef spot2) =>
        $"{SerializeSpot(spot1)}-{SerializeSpot(spot2)}";

    static string SerializeSpot(SeriesSpotRef spot)
    {
        if (!SpotTypeToSource.TryGetValue(spot.Type, out var source))
            throw new TournamentFormatException(
                $"Unknown matchup spot type \"{spot.Type}\". Expected Seed, Winner, Loser or Reseed.");

        if (spot.Number < 1)
            throw new TournamentFormatException($"Matchup spot number must be 1 or greater (got {spot.Number}).");

        return $"{source}{spot.Number}";
    }

    // -------------------------------------------------------------- HostOrder

    /// <summary>Returns which spot (1 or 2) hosts each game, in game order.</summary>
    public static List<int> ParseHostOrder(string hostOrder)
    {
        if (string.IsNullOrWhiteSpace(hostOrder))
            throw new TournamentFormatException("A series is missing its host order.");

        var hosts = new List<int>();
        foreach (var c in hostOrder)
        {
            if (c != '1' && c != '2')
                throw new TournamentFormatException(
                    $"Host order \"{hostOrder}\" may only contain 1 or 2 (found '{c}').");
            hosts.Add(c - '0');
        }
        return hosts;
    }

    public static string SerializeHostOrder(IEnumerable<int> hosts)
    {
        var list = hosts.ToList();
        if (list.Count == 0)
            throw new TournamentFormatException("A series must be at least one game long.");

        foreach (var host in list)
        {
            if (host != 1 && host != 2)
                throw new TournamentFormatException($"Host order entries must be 1 or 2 (got {host}).");
        }
        return string.Concat(list);
    }

    /// <summary>Number of games in a series, derived from its host order.</summary>
    public static int SeriesLength(string hostOrder) => ParseHostOrder(hostOrder).Count;

    /// <summary>Wins needed to take a "Best of" series.</summary>
    public static int WinsRequired(string hostOrder) =>
        Convert.ToInt32(Math.Ceiling(SeriesLength(hostOrder) / 2d));

    // ---------------------------------------------------- SeedingConfiguration

    /// <summary>
    /// Parses a seeding configuration. Null or empty yields an empty list, which the
    /// tournament treats as "seed from this year's regular season standings".
    /// </summary>
    public static List<SeedGroup> ParseSeeding(string? configuration)
    {
        var groups = new List<SeedGroup>();
        if (string.IsNullOrWhiteSpace(configuration)) return groups;

        foreach (var group in configuration.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = group.Split(',');
            if (fields.Length != 3)
                throw new TournamentFormatException(
                    $"Seeding rule \"{group}\" must have three comma-separated parts.");

            var (outputStart, outputEnd) = ParseRange(fields[0], $"seeding rule \"{group}\"");
            var result = fields[1];

            var source = fields[2].Split(':');
            if (source.Length != 3)
                throw new TournamentFormatException(
                    $"Seeding rule \"{group}\" source must look like Type:ID:start-end.");

            if (!long.TryParse(source[1], out var sourceID))
                throw new TournamentFormatException($"Seeding rule \"{group}\" has an invalid source ID.");

            var (rankStart, rankEnd) = ParseRange(source[2], $"seeding rule \"{group}\"");

            var seedGroup = new SeedGroup(outputStart, outputEnd, result, source[0], sourceID, rankStart, rankEnd);
            ValidateSeedGroup(seedGroup);
            groups.Add(seedGroup);
        }

        return groups;
    }

    static (int Start, int End) ParseRange(string range, string context)
    {
        var parts = range.Split('-');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var start)
            || !int.TryParse(parts[1], out var end))
            throw new TournamentFormatException($"Range \"{range}\" in {context} must look like start-end.");

        if (start < 1 || end < start)
            throw new TournamentFormatException($"Range \"{range}\" in {context} is not a valid range.");

        return (start, end);
    }

    public static string? SerializeSeeding(IEnumerable<SeedGroup> groups)
    {
        var list = groups.ToList();
        if (list.Count == 0) return null;

        ValidateSeedGroups(list);

        return string.Join(';', list.Select(g =>
            $"{g.OutputStart}-{g.OutputEnd},{g.Result},{g.SourceType}:{g.SourceID}:{g.RankStart}-{g.RankEnd}"));
    }

    public static void ValidateSeedGroups(IEnumerable<SeedGroup> groups)
    {
        var list = groups.ToList();
        foreach (var group in list) ValidateSeedGroup(group);

        var claimed = new Dictionary<int, SeedGroup>();
        foreach (var group in list)
        {
            for (var seed = group.OutputStart; seed <= group.OutputEnd; seed++)
            {
                if (claimed.ContainsKey(seed))
                    throw new TournamentFormatException(
                        $"Seed {seed} is assigned by more than one seeding rule.");
                claimed[seed] = group;
            }
        }
    }

    static void ValidateSeedGroup(SeedGroup group)
    {
        if (group.Result != SeedGroup.ResultStandings && group.Result != SeedGroup.ResultLosers)
            throw new TournamentFormatException(
                $"Unknown seeding result \"{group.Result}\". Expected {SeedGroup.ResultStandings} or {SeedGroup.ResultLosers}.");

        string[] validSources = group.Result == SeedGroup.ResultLosers
            ? [SeedGroup.SourceBracketRound]
            : [SeedGroup.SourceSeason, SeedGroup.SourceBracketRound, SeedGroup.SourceRoundRobin];

        if (!validSources.Contains(group.SourceType))
            throw new TournamentFormatException(
                $"Seeding source \"{group.SourceType}\" cannot be used with {group.Result}. " +
                $"Expected one of: {string.Join(", ", validSources)}.");

        var outputCount = group.OutputEnd - group.OutputStart + 1;
        var rankCount = group.RankEnd - group.RankStart + 1;
        if (outputCount != rankCount)
            throw new TournamentFormatException(
                $"Seeding rule for seeds {group.OutputStart}-{group.OutputEnd} takes {rankCount} " +
                $"team(s) from its source but needs {outputCount}.");
    }

    // ------------------------------------------------------- Series validation

    public static void ValidateSeriesFormat(string format, string hostOrder)
    {
        if (format != SeriesFormatBestOf && format != SeriesFormatAggregate)
            throw new TournamentFormatException(
                $"Unknown series format \"{format}\". Expected \"{SeriesFormatBestOf}\" or \"{SeriesFormatAggregate}\".");

        var length = SeriesLength(hostOrder);
        if (format == SeriesFormatBestOf && length % 2 == 0)
            throw new TournamentFormatException(
                $"A \"{SeriesFormatBestOf}\" series must have an odd number of games (got {length}).");
    }

    public static void ValidateBracketFormat(string format)
    {
        if (format != BracketFormatFixed && format != BracketFormatReseed)
            throw new TournamentFormatException(
                $"Unknown bracket format \"{format}\". Expected \"{BracketFormatFixed}\" or \"{BracketFormatReseed}\".");
    }
}
