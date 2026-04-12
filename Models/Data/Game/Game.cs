using System.ComponentModel.DataAnnotations.Schema;

public partial class Game : ITeamScoped, IEquatable<Game>
{
    public Game()
    {
        BattingEvents = [];
        BattingLineupEntries = [];
        SeriesGames = [];
        RoundRobinGames = [];
    }

    public IEnumerable<long> GetRelatedTeamIds() => [HostTeamID, VisitingTeamID];
    public IEnumerable<Team?> GetRelatedTeams() => [HostTeam, VisitingTeam];
    public long ID { get; set; }
    public long SeasonID { get; set; }
    public DateTime Date { get; set; }
    public long HostTeamID { get; set; }
    public long VisitingTeamID { get; set; }
    public long LocationID { get; set; }
    public long StatusID { get; set; }
    public long? ScoreHost { get; set; }
    public long? ScoreVisitor { get; set; }

    [JsonIgnore]
    public virtual Team? HostTeam { get; set; }
    [JsonIgnore]
    public virtual Location? Location { get; set; }
    [JsonIgnore]
    public virtual Season? Season { get; set; }
    [NotMapped, JsonIgnore]
    public Standings? Standings { get; set; }
    [JsonIgnore]
    public virtual GameStatus? Status { get; set; }
    [JsonIgnore]
    public virtual Team? VisitingTeam { get; set; }
    [JsonIgnore]
    public virtual ICollection<BattingEvent> BattingEvents { get; set; }
    [JsonIgnore]
    public virtual ICollection<BattingLineupEntry> BattingLineupEntries { get; set; }
    [JsonIgnore]
    public virtual ICollection<SeriesGame> SeriesGames { get; set; }
    [JsonIgnore]
    public virtual ICollection<RoundRobinGame> RoundRobinGames { get; set; }

    public bool Equals(Game? other) => ID == other?.ID;
    public override bool Equals(object? obj) => Equals(obj as Game);
    public override int GetHashCode() => Convert.ToInt32(ID);
    public static bool operator ==(Game? left, Game? right) => left is null ? right is null : left.Equals(right);
    public static bool operator !=(Game? left, Game? right) => !(left == right);
}
