public partial class InvitationStatus
{
    public InvitationStatus()
    {
        Invitations = [];
    }

    public long ID { get; set; }
    public required string Name { get; set; }
    [JsonIgnore]
    public virtual ICollection<Invitation> Invitations { get; set; }
}