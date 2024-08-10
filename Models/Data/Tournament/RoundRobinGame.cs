public partial class RoundRobinGame
{
    public RoundRobinGame()
    {
    }

    public long ID { get; set; }
    public long TournamentRoundRobinID { get; set; }
    public long? GameID { get; set; }

    public virtual TournamentRoundRobin? RoundRobin { get; set; }
    public virtual Game? Game { get; set; }
}