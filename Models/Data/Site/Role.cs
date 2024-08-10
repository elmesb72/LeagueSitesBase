public partial class Role
{
    public Role()
    {
        InvitationRoles = [];
        UserRoles = [];
    }

    public long ID { get; set; }
    public required string Name { get; set; }

    [JsonIgnore]
    public virtual ICollection<InvitationRole> InvitationRoles { get; set; }
    [JsonIgnore]
    public virtual ICollection<UserRole> UserRoles { get; set; }
}