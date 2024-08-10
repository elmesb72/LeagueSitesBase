public partial class PlayerSocial
{
    public long ID { get; set; }
    public long PlayerID { get; set; }
    public long SocialPlatformID { get; set; }
    public required string Account { get; set; }

    public virtual Player? Player { get; set; }
    public virtual SocialPlatform? Platform { get; set; }
}