using Markdig;

public partial class News
{
    public long ID { get; set; }
    public required long AuthorID { get; set; }
    public required DateTime Date { get; set; }
    public DateTime? Edited { get; set; }
    public required string Title { get; set; }
    public required string Contents { get; set; }
    public required string Source { get; set; }
    
    public bool IsDeleted { get; set; }
    public bool IsHidden { get; set; }

    public virtual User? Author { get; set; }

    public string RenderContents()
    {
        return Markdown.ToHtml(Contents);
    }
}