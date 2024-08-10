public partial class Season
{
    public Season()
    {
        Games = [];
        Tournaments = [];
    }

    public long ID { get; set; }
    public long Year { get; set; }
    public required string Subseason { get; set; }
    public string Name
    {
        get
        {
            return $"{Year} {Subseason}";
        }
    }
    public DateTime StartDate { get; set; }

    public virtual ICollection<Game> Games { get; set; }
    public virtual ICollection<Tournament> Tournaments { get; set; }
}
