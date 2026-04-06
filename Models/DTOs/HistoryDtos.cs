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
            var historicalBrackets = year.PlayoffsTournament.Brackets.Where(b => b.Historical);
            var historicalRoundRobins = year.PlayoffsTournament.RoundRobins.Where(r => r.Historical);

            if (historicalBrackets.Count() + historicalRoundRobins.Count() > 1)
            {
                var bracketWinners = historicalBrackets
                    .ToDictionary(b => b.Name, b => b.GetWinner());
                var roundRobinWinners = historicalRoundRobins
                    .ToDictionary(b => b.Name, b => b.Standings!.Keys.First());
                champion = string.Join("; ",
                    bracketWinners.Select(w => $"{w.Key}: {w.Value.FullName}")
                    .Concat(roundRobinWinners.Select(w => $"{w.Key}: {w.Value.FullName}")));
            }
            else if (year.PlayoffsTournament.Brackets.Any())
            {
                var winner = year.PlayoffsTournament.Brackets.First().GetWinner();
                champion = winner.FullName;
                championAbbr = winner.Abbreviation;
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
