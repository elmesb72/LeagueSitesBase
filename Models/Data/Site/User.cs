public partial class User
{
    public User()
    {
        Invitations = [];
        UserLogins = [];
        UserRoles = [];
        Events = [];
        NewsPosts = [];
    }

    public long ID { get; set; }

    [JsonIgnore]
    public virtual ICollection<Invitation> Invitations { get; set; }
    public virtual ICollection<UserLogin> UserLogins { get; set; }
    public virtual ICollection<UserRole> UserRoles { get; set; }
    [JsonIgnore]
    public virtual ICollection<Event> Events { get; set; }
    public virtual ICollection<News> NewsPosts { get; set; }

    public string Name
    {
        get
        {
            return UserLogins.FirstOrDefault(ul => ul.IsPrimary)?.Name ?? string.Empty;
        }
    }
}