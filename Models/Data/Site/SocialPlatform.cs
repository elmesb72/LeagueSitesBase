public partial class SocialPlatform
{
    public SocialPlatform()
    {
        TeamSocials = [];
        PlayerSocials = [];
    }

    public long ID { get; set; }
    public required string Name { get; set; }
    public Uri? BaseUrl { get; set; }

    public ICollection<TeamSocial> TeamSocials { get; set; }
    public ICollection<PlayerSocial> PlayerSocials { get; set; }

}