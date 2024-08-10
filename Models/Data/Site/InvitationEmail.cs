public partial class InvitationEmail
{
    public long ID { get; set; }
    public long InvitationID { get; set; }
    public required string Email { get; set; }

    [JsonIgnore]
    public virtual Invitation? Invitation { get; set; }
}