public partial class BattingEvent
{
    public long GameID { get; set; }
    public bool IsHostTeam { get; set; }
    public long Index { get; set; }
    public long PlayerID { get; set; }
    public string? Before { get; set; }
    public required string During { get; set; }
    public string? After { get; set; }
    public string? PitchSequence { get; set; }
    public string? Notes { get; set; }

    public virtual Game? Game { get; set; }
    public virtual Player? Player { get; set; }
}