public partial class BattingLineupEntry
{
    public long ID { get; set; }
    public long GameID { get; set; }
    public bool IsHostTeam { get; set; }
    public long Row { get; set; }
    public long PlayerID { get; set; }
    public long? FirstAB { get; set; }
    public string? BattingSide { get; set; }
    public string? Positions { get; set; } // Position/In[;Position/In;...] where Position is a fielding position or offensive replacement (PH, PR, DR, etc.), and In is the box the player entered the game at (opponent box # by default, team box # for offensive replacement)
    public long? Out { get; set; } // Opponent box #

    public virtual Game? Game { get; set; }
    public virtual Player? Player { get; set; }

    public static BattingEvent GetFirstABEvent(ICollection<BattingEvent> gameEvents)
    {
        throw new NotImplementedException();
    }

    public static BattingEvent GetOutEvent(ICollection<BattingEvent> opponentEvents)
    {
        throw new NotImplementedException();
    }
}