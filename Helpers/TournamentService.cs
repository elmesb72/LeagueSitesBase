using Microsoft.EntityFrameworkCore;

public interface ITournamentService
{
    /// <summary>
    /// Loads a tournament with its full structure and resolves every seed, matchup spot,
    /// series winner and pool standing. Returns null if the tournament does not exist.
    /// </summary>
    Task<Tournament?> GetPopulatedAsync(long tournamentID);

    /// <summary>Builds the structured management view of an already-populated tournament.</summary>
    Task<TournamentDetailDto> BuildDetailAsync(Tournament tournament);

    /// <summary>
    /// The seeding rule the UI should offer as the default: every team in the regular
    /// season standings for the tournament's year, in standings order. Null when no
    /// regular season games have been played yet.
    /// </summary>
    Task<SeedGroupDto?> GetDefaultSeedingAsync(Tournament tournament);

    /// <summary>
    /// Resolves who hosts and who visits a given game of a series, using the series' host
    /// order and its currently resolved spots.
    /// </summary>
    (Team Host, Team Visitor) ResolveGameTeams(RoundSeries series, long gameNumber);

    /// <summary>
    /// Confirms every seeding rule points at something that actually exists, so a saved
    /// structure can never reference a deleted round, pool or season.
    /// </summary>
    Task ValidateSeedingSourcesAsync(IEnumerable<SeedGroupDto> seeding);
}

public class TournamentService(
    LeagueSitesContext dbContext,
    IStandingsConfigService standingsConfigService) : ITournamentService
{
    static readonly string[] ExcludedGameStatuses = ["Deleted"];

    public async Task<Tournament?> GetPopulatedAsync(long tournamentID)
    {
        var tournament = await dbContext.Tournaments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(t => t.Season)
            .Include(t => t.Brackets)
                .ThenInclude(b => b.Rounds)
                    .ThenInclude(r => r.Series)
                        .ThenInclude(s => s.Games)
            .Include(t => t.RoundRobins)
                .ThenInclude(rr => rr.Games)
            .FirstOrDefaultAsync(t => t.ID == tournamentID);

        if (tournament is null) return null;

        var games = await dbContext.Games
            .AsNoTracking()
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Status)
            .Include(g => g.Location)
            .Include(g => g.Season)
            .Where(g => g.SeasonID == tournament.SeasonID)
            .ToListAsync();

        await tournament.Populate(games, dbContext, await standingsConfigService.GetAsync());
        return tournament;
    }

    public async Task<TournamentDetailDto> BuildDetailAsync(Tournament tournament)
    {
        var regularSeason = await GetRegularSeasonAsync(tournament);
        var regularSeasonTeamCount = regularSeason is null ? 0 : await CountStandingsAsync(regularSeason.ID);

        return new TournamentDetailDto(
            tournament.ID,
            new SeasonSummaryDto(tournament.Season!),
            [.. tournament.Brackets.Select(b => BuildBracket(b, regularSeason))],
            [.. tournament.RoundRobins.Select(rr => BuildRoundRobin(rr, regularSeason))],
            await BuildReferenceDataAsync(tournament, regularSeason, regularSeasonTeamCount));
    }

    public async Task<SeedGroupDto?> GetDefaultSeedingAsync(Tournament tournament)
    {
        var regularSeason = await GetRegularSeasonAsync(tournament);
        if (regularSeason is null) return null;

        var teamCount = await CountStandingsAsync(regularSeason.ID);
        if (teamCount == 0) return null;

        return new SeedGroupDto(
            1, teamCount, SeedGroup.ResultStandings,
            SeedGroup.SourceSeason, regularSeason.ID, 1, teamCount);
    }

    public (Team Host, Team Visitor) ResolveGameTeams(RoundSeries series, long gameNumber)
    {
        var hostOrder = TournamentFormats.ParseHostOrder(series.HostOrder);

        if (gameNumber < 1 || gameNumber > hostOrder.Count)
            throw new TournamentFormatException(
                $"Series {series.Number} is {hostOrder.Count} game(s) long, so it has no game {gameNumber}.");

        var spot1 = series.Spots.Item1?.Team;
        var spot2 = series.Spots.Item2?.Team;

        if (spot1 is null || spot2 is null)
            throw new TournamentFormatException(
                $"Both teams in series {series.Number} must be known before its games can be scheduled.");

        return hostOrder[(int)gameNumber - 1] == 1 ? (spot1, spot2) : (spot2, spot1);
    }

    // ------------------------------------------------------------------ Brackets

    BracketStructureDto BuildBracket(TournamentBracket bracket, Season? regularSeason)
    {
        var seeding = TournamentFormats.ParseSeeding(bracket.SeedingConfiguration);
        var allSeries = bracket.Rounds.SelectMany(r => r.Series).ToList();

        return new BracketStructureDto(
            bracket.ID,
            bracket.Name,
            bracket.Format,
            bracket.Historical,
            IsRegularSeasonSeeding(seeding, regularSeason),
            [.. seeding.Select(SeedGroupDto.From)],
            BuildResolvedSeeds(bracket.Seeds),
            [.. bracket.Rounds.Select(BuildRound)],
            allSeries.Any(s => s.Winner is not null)
                ? new TeamSummaryDto(bracket.GetWinner())
                : null,
            CanDelete: !allSeries.SelectMany(s => s.Games).Any(g => g.GameID is not null));
    }

    RoundStructureDto BuildRound(BracketRound round) => new(
        round.ID,
        round.Name,
        [.. round.Series.OrderBy(s => s.Number).Select(BuildSeries)]);

    SeriesStructureDto BuildSeries(RoundSeries series)
    {
        var matchup = MatchupDto.From(series.Matchup);
        var hostOrder = TournamentFormats.ParseHostOrder(series.HostOrder);

        var spot1 = BuildSpotState(matchup.Spot1, series.Spots.Item1);
        var spot2 = BuildSpotState(matchup.Spot2, series.Spots.Item2);
        var canSchedule = spot1.Team is not null && spot2.Team is not null;

        string? resultText = null;
        if (canSchedule)
            resultText = SeriesResultsDto.From(series.GetResults(), series).StatusText;

        var (slots, unexpected) = BuildGameSlots(series, hostOrder);

        return new SeriesStructureDto(
            series.ID,
            series.Number,
            series.Format,
            hostOrder.Count,
            hostOrder,
            matchup,
            spot1,
            spot2,
            series.Winner is not null ? new TeamSummaryDto(series.Winner) : null,
            series.Loser is not null ? new TeamSummaryDto(series.Loser) : null,
            resultText,
            canSchedule,
            slots,
            unexpected);
    }

    static SeriesSpotStateDto BuildSpotState(SpotRefDto reference, TournamentSeriesSpot? resolved) => new(
        reference.Type,
        reference.Number,
        reference.Label(),
        resolved?.Team is not null ? new TeamSummaryDto(resolved.Team) : null,
        null);

    (List<SeriesGameSlotDto> Slots, List<SeriesGameLinkDto> Unexpected) BuildGameSlots(
        RoundSeries series, List<int> hostOrder)
    {
        Team? host = null;
        Team? visitor = null;

        var slots = new List<SeriesGameSlotDto>();
        var claimed = new HashSet<long>();

        for (var gameNumber = 1; gameNumber <= hostOrder.Count; gameNumber++)
        {
            if (series.Spots.Item1?.Team is not null && series.Spots.Item2?.Team is not null)
            {
                (host, visitor) = ResolveGameTeams(series, gameNumber);
            }

            // First link wins the slot. Anything else with the same number is surfaced as unexpected.
            var link = series.Games
                .Where(g => g.GameNumber == gameNumber)
                .OrderBy(g => g.ID)
                .FirstOrDefault();

            if (link is not null) claimed.Add(link.ID);

            slots.Add(new SeriesGameSlotDto(
                gameNumber,
                hostOrder[gameNumber - 1],
                link?.ID,
                host is not null ? new TeamSummaryDto(host) : null,
                visitor is not null ? new TeamSummaryDto(visitor) : null,
                link?.Game is not null ? new GameSummaryDto(link.Game) : null));
        }

        var unexpected = series.Games
            .Where(g => !claimed.Contains(g.ID))
            .OrderBy(g => g.GameNumber).ThenBy(g => g.ID)
            .Select(g => new SeriesGameLinkDto(
                g.ID,
                g.GameNumber,
                g.Game is not null ? new GameSummaryDto(g.Game) : null,
                g.GameNumber < 1 || g.GameNumber > hostOrder.Count
                    ? $"This series only has {hostOrder.Count} game(s), so game {g.GameNumber} does not belong to it."
                    : $"Game {g.GameNumber} of this series is linked more than once."))
            .ToList();

        return (slots, unexpected);
    }

    // -------------------------------------------------------------- Round robins

    RoundRobinStructureDto BuildRoundRobin(TournamentRoundRobin roundRobin, Season? regularSeason)
    {
        var seeding = TournamentFormats.ParseSeeding(roundRobin.SeedingConfiguration);
        roundRobin.Standings?.CalculateStreaks();

        return new RoundRobinStructureDto(
            roundRobin.ID,
            roundRobin.Name,
            roundRobin.Historical,
            IsRegularSeasonSeeding(seeding, regularSeason),
            [.. seeding.Select(SeedGroupDto.From)],
            BuildResolvedSeeds(roundRobin.Seeds),
            roundRobin.Standings?.ToDto(),
            [.. roundRobin.Games
                .OrderBy(g => g.Game?.Date)
                .Select(g => new RoundRobinGameSlotDto(
                    g.ID,
                    g.Game is not null ? new GameSummaryDto(g.Game) : null))],
            CanDelete: !roundRobin.Games.Any(g => g.GameID is not null));
    }

    // ------------------------------------------------------------------ Seeding

    static List<SeedAssignmentDto> BuildResolvedSeeds(Dictionary<int, Team>? seeds) =>
        seeds is null
            ? []
            : [.. seeds.OrderBy(s => s.Key).Select(s => new SeedAssignmentDto(s.Key, new TeamSummaryDto(s.Value)))];

    /// <summary>True when the seeding is simply "everyone, in regular season standings order".</summary>
    static bool IsRegularSeasonSeeding(List<SeedGroup> seeding, Season? regularSeason) =>
        regularSeason is not null
        && seeding.Count == 1
        && seeding[0].Result == SeedGroup.ResultStandings
        && seeding[0].SourceType == SeedGroup.SourceSeason
        && seeding[0].SourceID == regularSeason.ID;

    async Task<TournamentReferenceDataDto> BuildReferenceDataAsync(
        Tournament tournament, Season? regularSeason, int regularSeasonTeamCount)
    {
        var teams = await dbContext.Teams
            .AsNoTracking()
            .Where(t => t.Active && !t.Hidden)
            .OrderBy(t => t.Abbreviation)
            .ToListAsync();

        var locations = await dbContext.Locations
            .AsNoTracking()
            .Where(l => l.Active)
            .OrderBy(l => l.Name)
            .ToListAsync();

        return new TournamentReferenceDataDto(
            [.. teams.Select(t => new TeamSummaryDto(t))],
            [.. locations.Select(l => new LocationSummaryDto(l))],
            BuildSeedingSources(tournament, regularSeason, regularSeasonTeamCount),
            regularSeason?.ID,
            regularSeasonTeamCount);
    }

    /// <summary>
    /// Every place an executive can draw teams from, described in plain language so the UI
    /// can present seeding as a choice from a list rather than a configuration string.
    /// </summary>
    static List<SeedingSourceOptionDto> BuildSeedingSources(
        Tournament tournament, Season? regularSeason, int regularSeasonTeamCount)
    {
        var sources = new List<SeedingSourceOptionDto>();

        if (regularSeason is not null)
        {
            sources.Add(new SeedingSourceOptionDto(
                SeedGroup.SourceSeason, regularSeason.ID,
                $"{regularSeason.Year} regular season standings",
                SeedGroup.ResultStandings,
                regularSeasonTeamCount));
        }

        foreach (var bracket in tournament.Brackets)
        {
            foreach (var round in bracket.Rounds)
            {
                var seriesCount = round.Series.Count;

                sources.Add(new SeedingSourceOptionDto(
                    SeedGroup.SourceBracketRound, round.ID,
                    $"{round.Name} results ({bracket.Name} bracket)",
                    SeedGroup.ResultStandings,
                    seriesCount * 2));

                sources.Add(new SeedingSourceOptionDto(
                    SeedGroup.SourceBracketRound, round.ID,
                    $"Teams knocked out in the {round.Name} ({bracket.Name} bracket)",
                    SeedGroup.ResultLosers,
                    seriesCount));
            }
        }

        foreach (var roundRobin in tournament.RoundRobins)
        {
            var available = Math.Max(
                roundRobin.Standings?.Count ?? 0,
                roundRobin.Seeds?.Count ?? 0);

            sources.Add(new SeedingSourceOptionDto(
                SeedGroup.SourceRoundRobin, roundRobin.ID,
                $"{roundRobin.Name} final standings",
                SeedGroup.ResultStandings,
                available));
        }

        return sources;
    }

    public async Task ValidateSeedingSourcesAsync(IEnumerable<SeedGroupDto> seeding)
    {
        foreach (var group in seeding)
        {
            var exists = group.SourceType switch
            {
                SeedGroup.SourceSeason =>
                    await dbContext.Seasons.AnyAsync(s => s.ID == group.SourceID),
                SeedGroup.SourceBracketRound =>
                    await dbContext.BracketRounds.AnyAsync(r => r.ID == group.SourceID),
                SeedGroup.SourceRoundRobin =>
                    await dbContext.TournamentRoundRobins.AnyAsync(r => r.ID == group.SourceID),
                _ => false,
            };

            if (!exists)
                throw new TournamentFormatException(
                    $"The seeding rule for seeds {group.OutputStart}-{group.OutputEnd} refers to " +
                    $"{group.SourceType} {group.SourceID}, which no longer exists.");
        }
    }

    // ------------------------------------------------------------------- Seasons

    async Task<Season?> GetRegularSeasonAsync(Tournament tournament) =>
        await dbContext.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Year == tournament.Season!.Year && s.Subseason == "Regular Season");

    // Uses default StandingsConfig deliberately: only the COUNT of teams is
    // read, which no configured rule (order, points, forfeit score) can
    // change. See the configurable-standings-rules spec, Requirement 4.2.
    async Task<int> CountStandingsAsync(long seasonID)
    {
        var games = await dbContext.Games
            .AsNoTracking()
            .Include(g => g.HostTeam)
            .Include(g => g.VisitingTeam)
            .Include(g => g.Status)
            .Where(g => g.SeasonID == seasonID && !ExcludedGameStatuses.Contains(g.Status!.Name))
            .ToListAsync();

        return new Standings(games).Count;
    }
}
