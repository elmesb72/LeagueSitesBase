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
    public required string SeedingConfiguration { get; set; }
    public required bool Historical { get; set; } // if true, the winner of this round robin is shown on the league History page

    [NotMapped]
    public Standings? Standings { get; set; }
    public virtual Tournament? Tournament { get; set; }
    public ICollection<RoundRobinGame> Games { get; set; }

}
