using System.Diagnostics.CodeAnalysis;

public class SocialUserProfile
{
    public required string Name { get; set; }
    public required string Email { get; set; }

    [SetsRequiredMembers]
    public SocialUserProfile(string name, string email)
    {
        Name = name;
        Email = email;
    }
}