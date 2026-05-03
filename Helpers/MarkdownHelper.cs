using Markdig;

/// <summary>
/// Shared Markdown → HTML pipeline for user-authored content (news posts,
/// site about blurb). Using a single pipeline keeps rendering behavior
/// consistent across the app.
/// </summary>
public static class MarkdownHelper
{
    static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseEmphasisExtras().Build();

    public static string ToHtml(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;
        return Markdown.ToHtml(markdown, Pipeline);
    }
}
