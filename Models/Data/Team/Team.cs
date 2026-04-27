using System.ComponentModel.DataAnnotations.Schema;

public partial class Team : ITeamScoped, IEquatable<Team>
{
    public Team()
    {
        GameHostTeam = [];
        GameVisitingTeam = [];
        Invitations = [];
        Socials = [];
    }

    public IEnumerable<long> GetRelatedTeamIds() => [ID];
    public IEnumerable<Team?> GetRelatedTeams() => [this];

    public long ID { get; set; }
    public required string Location { get; set; }
    public required string Name { get; set; }
    public string FullName
    {
        get
        {
            return $"{Location} {Name}";
        }
    }
    public required string Abbreviation { get; set; }
    public bool Active { get; set; }
    public bool Hidden { get; set; }
    public required string BackgroundColor { get; set; }
    public required string Color { get; set; }

    [JsonIgnore]
    public virtual ICollection<Game> GameHostTeam { get; set; }
    [JsonIgnore]
    public virtual ICollection<Game> GameVisitingTeam { get; set; }
    [JsonIgnore]
    [NotMapped]
    public IEnumerable<Game> Games
    {
        get
        {
            return GameHostTeam.Union(GameVisitingTeam);
        }
    }
    [JsonIgnore]
    public virtual ICollection<Invitation> Invitations { get; set; }
    [JsonIgnore]
    public virtual ICollection<TeamSocial> Socials { get; set; }

    public bool Equals(Team? other)
    {
        return ID == other?.ID;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Team);
    }

    public override int GetHashCode()
    {
        return Convert.ToInt32(ID);
    }

    public static bool operator ==(Team? left, Team? right)
    {
        if (left is null)
        {
            return right is null;
        }
        return left.Equals(right);
    }

    public static bool operator !=(Team? left, Team? right)
    {
        return !(left == right);
    }
}