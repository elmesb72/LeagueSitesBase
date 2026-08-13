/// <summary>
/// Writes structured upsert shapes onto tournament entities, serializing to the stored
/// string encodings on the way. The mirror image of the read mapping in TournamentService.
/// </summary>
public static class TournamentStructureMapper
{
    public static void Apply(TournamentBracket bracket, BracketUpsertDto dto)
    {
        bracket.Name = dto.Name.Trim();
        bracket.Format = dto.Format;
        bracket.Historical = dto.Historical;
        bracket.SeedingConfiguration = SerializeSeeding(dto.Seeding);
    }

    public static void Apply(TournamentRoundRobin roundRobin, RoundRobinUpsertDto dto)
    {
        roundRobin.Name = dto.Name.Trim();
        roundRobin.Historical = dto.Historical;
        roundRobin.SeedingConfiguration = SerializeSeeding(dto.Seeding);
    }

    public static void Apply(BracketRound round, RoundUpsertDto dto)
    {
        round.Name = dto.Name.Trim();
    }

    public static void Apply(RoundSeries series, SeriesUpsertDto dto)
    {
        series.Number = dto.Number;
        series.Format = dto.Format;
        series.HostOrder = TournamentFormats.SerializeHostOrder(dto.HostOrder);
        series.Matchup = dto.Matchup.Serialize();
    }

    public static RoundSeries BuildSeries(SeriesUpsertDto dto)
    {
        var series = new RoundSeries
        {
            Format = dto.Format,
            HostOrder = TournamentFormats.SerializeHostOrder(dto.HostOrder),
            Matchup = dto.Matchup.Serialize(),
        };
        series.Number = dto.Number;
        return series;
    }

    public static BracketRound BuildRound(RoundUpsertDto dto)
    {
        var round = new BracketRound { Name = dto.Name.Trim() };
        foreach (var series in dto.Series) round.Series.Add(BuildSeries(series));
        return round;
    }

    /// <summary>
    /// Seeding is always stored explicitly, even when it is just "everyone in regular season
    /// order". Leaving it null makes the tournament infer it at read time, which then hides
    /// the seed order from anything that needs to reconstruct it later (such as ranking the
    /// losers of a round).
    /// </summary>
    static string SerializeSeeding(List<SeedGroupDto> seeding) =>
        TournamentFormats.SerializeSeeding(seeding.Select(s => s.ToSeedGroup()))
        ?? throw new TournamentFormatException("At least one seeding rule is required.");
}
