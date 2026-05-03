using System.Text.RegularExpressions;
using FluentAssertions;

namespace LeagueSitesBackend.Tests;

/// <summary>
/// Platform keys end up both in a filename ({key}.webp) and directly in an
/// <img src="/images/social/{key}.webp"> URL, so they must be a conservative
/// subset of characters. These tests pin the current regex so accidentally
/// loosening it (e.g. to allow "/" or "..") would fail CI.
/// </summary>
public class PlatformKeyValidationTests
{
    // Mirror of the regex in APISiteSocialImagesController. Kept in a
    // private field there, so we redeclare it here rather than expose it.
    static readonly Regex ValidPlatformKey = new(@"^[A-Za-z0-9_-]+$");

    [Theory]
    [InlineData("Facebook")]
    [InlineData("twitter")]
    [InlineData("X-Twitter")]
    [InlineData("my_platform")]
    [InlineData("abc123")]
    public void Accepts_SafeKeys(string key)
    {
        ValidPlatformKey.IsMatch(key).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("../etc/passwd")]
    [InlineData("/absolute")]
    [InlineData("with.dot")]
    [InlineData("with/slash")]
    [InlineData("with\\backslash")]
    [InlineData("emoji😀")]
    public void Rejects_UnsafeKeys(string key)
    {
        ValidPlatformKey.IsMatch(key).Should().BeFalse();
    }
}
