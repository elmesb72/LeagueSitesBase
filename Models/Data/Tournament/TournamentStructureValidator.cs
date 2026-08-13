/// <summary>
/// Validates a bracket or round robin structure before it is written to the database.
///
/// <para>
/// Everything here throws <see cref="TournamentFormatException"/> with a message written for
/// a league executive, not a developer. The goal is that an invalid structure is impossible
/// to save, so the public Playoffs page can never be handed data it cannot render.
/// </para>
/// </summary>
public static class TournamentStructureValidator
{
    public static void ValidateBracket(BracketUpsertDto bracket)
    {
        if (string.IsNullOrWhiteSpace(bracket.Name))
            throw new TournamentFormatException("The bracket needs a name.");

        TournamentFormats.ValidateBracketFormat(bracket.Format);
        ValidateSeeding(bracket.Seeding, "bracket");

        if (bracket.Rounds is null || bracket.Rounds.Count == 0)
            throw new TournamentFormatException("The bracket needs at least one round.");

        ValidateRoundShape(bracket.Rounds);
        ValidateSeriesNumbers(bracket.Rounds);
        ValidateSeriesFormats(bracket.Rounds);
        ValidateSeedReferences(bracket.Rounds);
        ValidateProgressionReferences(bracket.Rounds);
    }

    public static void ValidateRoundRobin(RoundRobinUpsertDto roundRobin)
    {
        if (string.IsNullOrWhiteSpace(roundRobin.Name))
            throw new TournamentFormatException("The pool needs a name.");

        ValidateSeeding(roundRobin.Seeding, "pool");
    }

    static void ValidateSeeding(List<SeedGroupDto>? seeding, string subject)
    {
        if (seeding is null || seeding.Count == 0)
            throw new TournamentFormatException($"The {subject} needs at least one seeding rule.");

        TournamentFormats.ValidateSeedGroups(seeding.Select(s => s.ToSeedGroup()));
    }

    static void ValidateRoundShape(List<RoundUpsertDto> rounds)
    {
        foreach (var round in rounds)
        {
            if (string.IsNullOrWhiteSpace(round.Name))
                throw new TournamentFormatException("Every round needs a name.");

            if (round.Series is null || round.Series.Count == 0)
                throw new TournamentFormatException($"Round \"{round.Name}\" needs at least one series.");
        }

        // The display engine orders rounds by descending series count, so a structure whose
        // rounds do not shrink cannot be laid out reliably. Enforced here rather than silently
        // rendering in the wrong order. Lifting this needs an explicit round order column.
        for (var i = 1; i < rounds.Count; i++)
        {
            if (rounds[i].Series.Count >= rounds[i - 1].Series.Count)
                throw new TournamentFormatException(
                    $"Round \"{rounds[i].Name}\" has {rounds[i].Series.Count} series but comes after " +
                    $"\"{rounds[i - 1].Name}\" which has {rounds[i - 1].Series.Count}. " +
                    "Each round must be smaller than the one before it.");
        }
    }

    static void ValidateSeriesNumbers(List<RoundUpsertDto> rounds)
    {
        var seen = new HashSet<long>();
        foreach (var series in rounds.SelectMany(r => r.Series))
        {
            if (series.Number < 1)
                throw new TournamentFormatException("Series numbers must be 1 or greater.");

            if (!seen.Add(series.Number))
                throw new TournamentFormatException(
                    $"Series number {series.Number} is used more than once. Series numbers must be unique " +
                    "within a bracket because later rounds refer to them.");
        }
    }

    static void ValidateSeriesFormats(List<RoundUpsertDto> rounds)
    {
        foreach (var series in rounds.SelectMany(r => r.Series))
        {
            // Round-trips through the serializer so anything unwritable is rejected now.
            var hostOrder = TournamentFormats.SerializeHostOrder(series.HostOrder);
            TournamentFormats.ValidateSeriesFormat(series.Format, hostOrder);
            series.Matchup.Serialize();
        }
    }

    static void ValidateSeedReferences(List<RoundUpsertDto> rounds)
    {
        var seedSpots = rounds
            .SelectMany(r => r.Series)
            .SelectMany(s => new[] { s.Matchup.Spot1, s.Matchup.Spot2 })
            .Where(s => s.Type == SeriesSpotRef.Seed)
            .ToList();

        var seen = new HashSet<int>();
        foreach (var spot in seedSpots)
        {
            if (!seen.Add(spot.Number))
                throw new TournamentFormatException(
                    $"Seed #{spot.Number} appears in more than one series. Each seed can only enter the bracket once.");
        }
    }

    static void ValidateProgressionReferences(List<RoundUpsertDto> rounds)
    {
        var numbersInEarlierRounds = new HashSet<long>();

        foreach (var round in rounds)
        {
            var spots = round.Series.SelectMany(s => new[] { s.Matchup.Spot1, s.Matchup.Spot2 });
            foreach (var spot in spots)
            {
                if (spot.Type != SeriesSpotRef.Winner && spot.Type != SeriesSpotRef.Loser) continue;

                if (!numbersInEarlierRounds.Contains(spot.Number))
                    throw new TournamentFormatException(
                        $"Round \"{round.Name}\" refers to the {spot.Type.ToLower()} of series {spot.Number}, " +
                        "but no earlier round has a series with that number.");
            }

            foreach (var series in round.Series) numbersInEarlierRounds.Add(series.Number);
        }
    }
}
