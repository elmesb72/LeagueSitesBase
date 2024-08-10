public partial class InvitationRole
{
    public long ID { get; set; }
    public long InvitationID { get; set; }
    public long RoleID { get; set; }

    [JsonIgnore]
    public virtual Invitation? Invitation { get; set; }
    [JsonIgnore]
    public virtual Role? Role { get; set; }
}