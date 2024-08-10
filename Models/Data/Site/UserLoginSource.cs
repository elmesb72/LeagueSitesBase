public partial class UserLoginSource
{
    public UserLoginSource()
    {
        UserLogins = [];
    }

    public long ID { get; set; }
    public required string Source { get; set; }

    [JsonIgnore]
    public virtual ICollection<UserLogin> UserLogins { get; set; }
}