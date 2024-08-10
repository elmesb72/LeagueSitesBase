public partial class BracketRound
{
    public BracketRound()
    {
        Series = [];
    }

    public long ID { get; set; }
    public required string Name { get; set; }
    public long BracketID { get; set; }
    
    public virtual TournamentBracket? Bracket { get; set; }
    public ICollection<RoundSeries> Series { get; set; }

}
