public partial class Game
{
    public Game()
    {
        BattingEvents = [];
        BattingLineupEntries = [];
    }
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
    [JsonIgnore]
    public virtual GameStatus? Status { get; set; }
    [JsonIgnore]
    public virtual Team? VisitingTeam { get; set; }
    [JsonIgnore]
    public virtual ICollection<BattingEvent> BattingEvents { get; set; }
    [JsonIgnore]
    public virtual ICollection<BattingLineupEntry> BattingLineupEntries { get; set; }

}
