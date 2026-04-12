public partial class Season : IEquatable<Season>
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

    public bool Equals(Season? other) => ID == other?.ID;
    public override bool Equals(object? obj) => Equals(obj as Season);
    public override int GetHashCode() => Convert.ToInt32(ID);
    public static bool operator ==(Season? left, Season? right) => left is null ? right is null : left.Equals(right);
    public static bool operator !=(Season? left, Season? right) => !(left == right);
}
