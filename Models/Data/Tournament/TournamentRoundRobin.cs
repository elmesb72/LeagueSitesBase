using System.ComponentModel.DataAnnotations.Schema;

public partial class TournamentRoundRobin
{
    public TournamentRoundRobin()
    {
        Games = [];
    }

    public long ID { get; set; }
    public required string Name { get; set; }
    public long TournamentID { get; set; }

    [NotMapped]
    public Standings? Standings { get; set; }
    public virtual Tournament? Tournament { get; set; }
    public ICollection<RoundRobinGame> Games { get; set; }

}
