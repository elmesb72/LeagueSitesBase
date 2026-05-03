public partial class News
{
    public long ID { get; set; }
    public required long AuthorID { get; set; }
    public required long AuthorInvitationID { get; set; }
    public required DateTime Date { get; set; }
    public DateTime? Edited { get; set; }
    public required string Title { get; set; }
    public required string Contents { get; set; }
    public required string Source { get; set; }
    

    public bool IsDeleted { get; set; }
    public bool IsHidden { get; set; }

    public virtual User? Author { get; set; }

    public string RenderContents() => MarkdownHelper.ToHtml(Contents);
    
    public static News GeneratePlaceholderPost(SiteConfig? siteConfig)
    {
        var siteName = siteConfig?.Name ?? "this";
        var home = siteConfig is not null
            ? System.Text.Json.JsonSerializer.Deserialize<SiteHomeConfig>(
                siteConfig.HomeJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })
                ?? new SiteHomeConfig()
            : new SiteHomeConfig();

        return new()
        {
            AuthorID = -1,
            AuthorInvitationID = -1,
            Author = new()
            {
                UserLogins = [
                    new() {
                        Name = "Webmaster",
                        IsPrimary = true,
                        Email = $"webmaster@leaguesites.com",
                    }
                ],
                Invitations = [
                    new() {
                        ID = -1,
                        Team = new() {
                            Location = "Unknown",
                            Name = "Team",
                            Abbreviation = "?",
                            BackgroundColor = "FFFFFF",
                            Color = "000000"
                        }
                    }
                ]
            },
            Title = "Placeholder Post",
            Contents = $"Welcome to the news section for the {siteName} website. This section shows news posts made within the last {home.NewsMaxAgeDays} days or the {home.NewsMinItems} most recent posts.",
            Date = DateTime.Now,
            Source = string.Empty
        };
    }
}