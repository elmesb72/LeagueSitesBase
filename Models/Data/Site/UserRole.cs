public partial class UserRole
{
    public long ID { get; set; }
    public long UserID { get; set; }
    public long RoleID { get; set; }

    [JsonIgnore]
    public virtual User? User { get; set; }
    public virtual Role? Role { get; set; }
}