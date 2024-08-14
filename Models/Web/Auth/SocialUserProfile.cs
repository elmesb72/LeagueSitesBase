using System.Diagnostics.CodeAnalysis;

[method: SetsRequiredMembers]
public class SocialUserProfile(string name, string email)
{
    public required string Name { get; set; } = name;
    public required string Email { get; set; } = email;
}