using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public partial class Player
{
    public Player()
    {
        BattingEvents = [];
        BattingLineupEntries = [];
        Invitations = [];
        Socials = [];
    }

    public long ID { get; set; }
    [MinLength(1)]
    public required string FirstName { get; set; }
    [MinLength(1)]
    public required string LastName { get; set; }
    [NotMapped]
    public string Name
    {
        get
        {
            return $"{FirstName} {LastName}";
        }
    }
    [NotMapped]
    public string NameSort
    {
        get
        {
            return $"{LastName}, {FirstName}";
        }
    }
    public string? Number { get; set; }
    [MaxLength(9)]
    public required string ShortCode { get; set; }

    public virtual PlayerBio? Bio { get; set; }

    [JsonIgnore]
    public ICollection<BattingEvent> BattingEvents { get; set; }
    [JsonIgnore]
    public ICollection<BattingLineupEntry> BattingLineupEntries { get; set; }
    [JsonIgnore]
    public virtual ICollection<Invitation> Invitations { get; set; }
    [JsonIgnore]
    public virtual ICollection<PlayerSocial> Socials { get; set; }
}