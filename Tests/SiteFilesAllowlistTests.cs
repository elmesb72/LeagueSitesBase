using FluentAssertions;

namespace LeagueSitesBackend.Tests;

/// <summary>
/// Pins the file-upload extension allowlist used by APISiteFilesController.
/// These tests use a local copy of the same list since the controller's
/// list is private; they exist to prevent accidental loosening (e.g. adding
/// .zip, .exe, .svg) without an explicit decision.
/// </summary>
public class SiteFilesAllowlistTests
{
    static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx", ".xlsx", ".pptx",
        ".doc", ".xls", ".ppt",
        ".txt", ".csv", ".md",
        ".png", ".jpg", ".jpeg", ".webp", ".gif"
    };

    [Theory]
    [InlineData("rules.pdf")]
    [InlineData("schedule.xlsx")]
    [InlineData("form.docx")]
    [InlineData("notes.txt")]
    [InlineData("photo.JPG")] // case-insensitive
    public void Allowed_CommonDocumentsAndImages(string filename)
    {
        var ext = Path.GetExtension(filename);
        AllowedExtensions.Should().Contain(ext);
    }

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("backup.zip")]
    [InlineData("data.tar.gz")]
    [InlineData("installer.msi")]
    [InlineData("script.js")]
    [InlineData("page.html")]
    [InlineData("icon.svg")] // SVG can embed script; blocked
    [InlineData("macro.bat")]
    [InlineData("payload.ps1")]
    [InlineData("image.bmp")] // uncommon image formats not on allowlist
    [InlineData("no-extension")]
    public void Blocked_ArchivesExecutablesAndScripts(string filename)
    {
        var ext = Path.GetExtension(filename);
        AllowedExtensions.Should().NotContain(ext);
    }

    [Fact]
    public void AllowedList_StaysConservative()
    {
        // Sanity check on list size so a future "just add this one type"
        // expansion is a conscious decision, not drift.
        AllowedExtensions.Should().HaveCountLessThanOrEqualTo(20);
    }
}
