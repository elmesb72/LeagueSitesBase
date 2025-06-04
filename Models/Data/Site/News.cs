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
    
    public static News GeneratePlaceholderPost(IConfiguration config)
    {
        return new()
        {
            AuthorID = -1,
            Author = new() {
                UserLogins = [
                new() {
                    Name = "Webmaster",
                    IsPrimary = true,
                    Email = $"webmaster@leaguesites.com",
                }
            ]
            },
            Title = "Placeholder Post",
            Contents = $"Welcome to the news section for the {config["Site:Name"]} website. This section shows news posts made within the last {config["Site:Home:NewsMaxAgeDays"]} days or the {config["Site:Home:NewsMinItems"]} most recent posts.",
            Date = DateTime.Now,
            Source = string.Empty
        };
    }
}