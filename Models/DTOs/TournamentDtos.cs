// Structured, self-describing views of a tournament for the Executive management UI.
//
// The compact string encodings stored in the database (Matchup, HostOrder,
// SeedingConfiguration) are deliberately absent from these shapes. All conversion
// happens in TournamentFormats so the UI never has to understand them.

// ------------------------------------------------------------------- Structure

public record TournamentDetailDto(
    long ID,
    SeasonSummaryDto Season,
    List<BracketStructureDto> Brackets,
    List<RoundRobinStructureDto> RoundRobins,
    TournamentReferenceDataDto ReferenceData);

public record BracketStructureDto(
    long ID,
    string Name,
    string Format,
    bool Historical,
    bool SeedsFromRegularSeason,
    List<SeedGroupDto> Seeding,
    List<SeedAssignmentDto> ResolvedSeeds,
    List<RoundStructureDto> Rounds,
    TeamSummaryDto? Winner,
    bool CanDelete);

public record RoundStructureDto(
    long ID,
    string Name,
    List<SeriesStructureDto> Series);

public record SeriesStructureDto(
    long ID,
    long Number,
    string Format,
    int Length,
    List<int> HostOrder,
    MatchupDto Matchup,
    SeriesSpotStateDto Spot1,
    SeriesSpotStateDto Spot2,
    TeamSummaryDto? Winner,
    TeamSummaryDto? Loser,
    string? ResultText,
    bool CanSchedule,
    List<SeriesGameSlotDto> GameSlots,
    List<SeriesGameLinkDto> UnexpectedLinks);

/// <summary>A matchup spot, both as the rule that defines it and the team it currently resolves to.</summary>
public record SeriesSpotStateDto(
    string Type,
    int Number,
    string Label,
    TeamSummaryDto? Team,
    int? InitialSeed);

public record SeriesGameSlotDto(
    long GameNumber,
    int HostSpot,
    long? SeriesGameID,
    TeamSummaryDto? HostTeam,
    TeamSummaryDto? VisitingTeam,
    GameSummaryDto? Game);

/// <summary>
/// A SeriesGame row that does not correspond to a slot implied by the host order, or a
/// duplicate of one. Surfaced so bad data (from hand-written SQL) is visible and fixable.
/// </summary>
public record SeriesGameLinkDto(
    long SeriesGameID,
    long GameNumber,
    GameSummaryDto? Game,
    string Reason);

public record RoundRobinStructureDto(
    long ID,
    string Name,
    bool Historical,
    bool SeedsFromRegularSeason,
    List<SeedGroupDto> Seeding,
    List<SeedAssignmentDto> ResolvedSeeds,
    List<StandingsEntryDto>? Standings,
    List<RoundRobinGameSlotDto> Games,
    bool CanDelete);

public record RoundRobinGameSlotDto(
    long RoundRobinGameID,
    GameSummaryDto? Game);

public record SeedAssignmentDto(int Seed, TeamSummaryDto Team);

// ------------------------------------------------------------------ Seeding

public record SeedGroupDto(
    int OutputStart,
    int OutputEnd,
    string Result,
    string SourceType,
    long SourceID,
    int RankStart,
    int RankEnd)
{
    public static SeedGroupDto From(SeedGroup group) => new(
        group.OutputStart, group.OutputEnd, group.Result,
        group.SourceType, group.SourceID, group.RankStart, group.RankEnd);

    public SeedGroup ToSeedGroup() => new(
        OutputStart, OutputEnd, Result, SourceType, SourceID, RankStart, RankEnd);
}

/// <summary>A source an executive can seed a bracket or pool from, with a plain-language label.</summary>
public record SeedingSourceOptionDto(
    string SourceType,
    long SourceID,
    string Label,
    string Result,
    int AvailableTeams);

public record TournamentReferenceDataDto(
    List<TeamSummaryDto> Teams,
    List<LocationSummaryDto> Locations,
    List<SeedingSourceOptionDto> SeedingSources,
    long? RegularSeasonID,
    int RegularSeasonTeamCount);

// -------------------------------------------------------------------- Matchup

public record MatchupDto(SpotRefDto Spot1, SpotRefDto Spot2)
{
    public static MatchupDto From(string matchup)
    {
        var (spot1, spot2) = TournamentFormats.ParseMatchup(matchup);
        return new(SpotRefDto.From(spot1), SpotRefDto.From(spot2));
    }

    public string Serialize() =>
        TournamentFormats.SerializeMatchup(Spot1.ToSpotRef(), Spot2.ToSpotRef());
}

public record SpotRefDto(string Type, int Number)
{
    public static SpotRefDto From(SeriesSpotRef spot) => new(spot.Type, spot.Number);
    public SeriesSpotRef ToSpotRef() => new(Type, Number);

    public string Label() => Type switch
    {
        SeriesSpotRef.Seed => $"#{Number}",
        SeriesSpotRef.Winner => $"Winner of series {Number}",
        SeriesSpotRef.Loser => $"Loser of series {Number}",
        SeriesSpotRef.Reseed => $"Remaining rank {Number}",
        _ => Type,
    };
}

// --------------------------------------------------------------------- Upsert

public record TournamentCreateDto(long SeasonID);

public record BracketUpsertDto(
    string Name,
    string Format,
    bool Historical,
    List<SeedGroupDto> Seeding,
    List<RoundUpsertDto> Rounds);

public record RoundUpsertDto(
    long? ID,
    string Name,
    List<SeriesUpsertDto> Series);

public record SeriesUpsertDto(
    long? ID,
    long Number,
    string Format,
    List<int> HostOrder,
    MatchupDto Matchup);

public record RoundRobinUpsertDto(
    string Name,
    bool Historical,
    List<SeedGroupDto> Seeding);

public record SeriesGameScheduleDto(
    long GameNumber,
    DateTime Date,
    long LocationID);

public record RoundRobinGameScheduleDto(
    DateTime Date,
    long HostTeamID,
    long VisitingTeamID,
    long LocationID);
