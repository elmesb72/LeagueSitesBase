public class Year
{
    public long CalendarYear { get; set; }
    public List<Season> Seasons { get; set; } = [];
    public Season? RegularSeason
    {
        get
        {
            return Seasons.FirstOrDefault(s => s.Subseason == "Regular Season");
        }
    }
    public readonly Standings? RegularSeasonStandings;
    public readonly Team? RegularSeasonWinner;
    public readonly TeamResultSet? RegularSeasonWinnerResults;
    public Season? Playoffs
    {
        get
        {
            return Seasons.FirstOrDefault(s => s.Subseason == "Playoffs");
        }
    }
    public Tournament? PlayoffsTournament
    {
        get
        {
            if (Playoffs == null) return null;
            return Playoffs.Tournaments.FirstOrDefault();
        }
    }
    public string? ExceptionYearDescription { get; set; }

    public Year(long calendarYear, List<Season> seasons)
    {
        CalendarYear = calendarYear;
        Seasons = seasons;
        RegularSeasonStandings = new Standings(RegularSeason?.Games ?? []);
        if (RegularSeasonIsComplete())
        {
            var winner = RegularSeasonStandings.First();
            RegularSeasonWinner = winner.Key;
            RegularSeasonWinnerResults = winner.Value;
        }
    }
    public Year(long calendarYear, string exceptionYearDescription)
    {
        CalendarYear = calendarYear;
        ExceptionYearDescription = exceptionYearDescription;
    }
    public Year(long calendarYear, Team team)
    {
        CalendarYear = calendarYear;
        RegularSeasonWinner = team;
    }

    public bool HasPlayoffs()
    {
        return Playoffs != null && PlayoffsTournament != null;
    }

    public bool RegularSeasonIsComplete()
    {
        return RegularSeason != null && RegularSeason.Games.Any() && RegularSeason.Games.All(g => g.Status.Name != "Upcoming");
    }

    public bool PlayoffsAreComplete()
    {
        return HasPlayoffs() && (PlayoffsTournament?.Brackets.SelectMany(b => b.Rounds).SelectMany(r => r.Series).All(s => s.Winner != null) ?? false);
    }

}