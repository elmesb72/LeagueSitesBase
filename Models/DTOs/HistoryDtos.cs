public record HistoryYearDto(
    long CalendarYear,
    string? ExceptionYearDescription,
    string? Champion,
    string? ChampionAbbreviation,
    string? BestRecord,
    string? BestRecordAbbreviation,
    string? BestRecordResults,
    bool PlayoffsComplete,
    bool RegularSeasonComplete)
{
    public static HistoryYearDto From(Year year)
    {
        string? champion = null;
        string? championAbbr = null;

        if (!string.IsNullOrEmpty(year.ExceptionYearDescription))
        {
            // Exception year — no champion or best record
        }
        else if (year.PlayoffsAreComplete() && year.PlayoffsTournament != null)
        {
            // Only brackets and pools the league has flagged Historical count,
            // the same rule the public Playoffs page uses for its champion
            // banner. One flagged: its winner is the champion. Several: each is
            // listed by name. None: no champion is recorded for the year.
            var winners = year.PlayoffsTournament.Brackets
                .Where(b => b.Historical)
                .Select(b => (b.Name, Team: b.GetWinner()))
                .Concat(year.PlayoffsTournament.RoundRobins
                    .Where(r => r.Historical && r.Standings is { Count: > 0 })
                    .Select(r => (r.Name, Team: r.Standings!.Keys.First())))
                .ToList();

            if (winners.Count == 1)
            {
                champion = winners[0].Team.FullName;
                championAbbr = winners[0].Team.Abbreviation;
            }
            else if (winners.Count > 1)
            {
                champion = string.Join("; ", winners.Select(w => $"{w.Name}: {w.Team.FullName}"));
            }
        }

        return new HistoryYearDto(
            year.CalendarYear,
            year.ExceptionYearDescription,
            champion,
            championAbbr,
            year.RegularSeasonWinner?.FullName,
            year.RegularSeasonWinner?.Abbreviation,
            year.RegularSeasonWinnerResults?.ToString(),
            year.PlayoffsAreComplete(),
            year.RegularSeasonIsComplete());
    }
}
