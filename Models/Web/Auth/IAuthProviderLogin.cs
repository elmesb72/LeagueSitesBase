public interface IAuthProviderLogin
{

    public SocialUserProfile? Profile { get; set; }
    public Dictionary<string, string>? Errors { get; set; }

    public Task GetProfileDataAsync();
    public bool WasSuccessful();

}
