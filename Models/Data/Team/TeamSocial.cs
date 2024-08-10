public partial class TeamSocial
{
    public long ID { get; set; }
    public long TeamID { get; set; }
    public long SocialPlatformID { get; set; }
    public required string Account { get; set; }

    public virtual Team? Team { get; set; }
    public virtual SocialPlatform? Platform { get; set; }
}