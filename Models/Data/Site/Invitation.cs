public partial class Invitation
{
    public Invitation()
    {
        InvitationEmails = [];
        InvitationRoles = [];
    }

    public long ID { get; set; }
    public long? PlayerID { get; set; }
    public long TeamID { get; set; }
    public long? UserID { get; set; }

    public virtual Player? Player { get; set; }
    [JsonIgnore]
    public virtual Team? Team { get; set; }
    [JsonIgnore]
    public virtual User? User { get; set; }
    public long StatusID { get; set; }
    public string? EmergencyContactInfo { get; set; }

    [JsonIgnore]
    public virtual InvitationStatus? Status { get; set; }

    public virtual ICollection<InvitationEmail> InvitationEmails { get; set; }
    public virtual ICollection<InvitationRole> InvitationRoles { get; set; }

}