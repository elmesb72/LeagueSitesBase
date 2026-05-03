using FluentAssertions;

namespace LeagueSitesBackend.Tests;

public class MarkdownHelperTests
{
    [Fact]
    public void ToHtml_EmptyInput_ReturnsEmpty()
    {
        MarkdownHelper.ToHtml("").Should().Be(string.Empty);
        MarkdownHelper.ToHtml(null).Should().Be(string.Empty);
    }

    [Fact]
    public void ToHtml_PlainText_WrapsInParagraph()
    {
        var html = MarkdownHelper.ToHtml("Hello world").Trim();
        html.Should().Be("<p>Hello world</p>");
    }

    [Fact]
    public void ToHtml_MarkdownEmphasis_RendersAsTags()
    {
        var html = MarkdownHelper.ToHtml("*italic* and **bold**").Trim();
        html.Should().Be("<p><em>italic</em> and <strong>bold</strong></p>");
    }

    /// <summary>
    /// The legacy about-blurb value contained inline HTML (e.g. &lt;i&gt;).
    /// Markdig's default pipeline passes inline HTML through unchanged, so
    /// existing content keeps rendering correctly after the switch to
    /// Markdown authoring.
    /// </summary>
    [Fact]
    public void ToHtml_InlineHtml_IsPreserved()
    {
        var html = MarkdownHelper.ToHtml(
            "Welcome to the official website for the <i>Empty Generic League</i>!").Trim();
        html.Should().Be(
            "<p>Welcome to the official website for the <i>Empty Generic League</i>!</p>");
    }
}
