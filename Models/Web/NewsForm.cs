public class NewsForm
{
    public long NewsID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Contents { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public bool IsHidden { get; set; }

    public DateTime Date { get; set; }
    public DateTime? Edited { get; set; }
    public bool IsNewNewsPost { get; set; } = true;

    public NewsForm() { }

    public NewsForm(News n)
    {
        IsNewNewsPost = false;

        NewsID = n.ID;
        Title = n.Title;
        Contents = n.Contents;
        IsDeleted = n.IsDeleted;
        IsHidden = n.IsHidden;

        Date = n.Date;
        Edited = n.Edited;
    }

    public void UpdateExistingNewsPost(ref News n)
    {
        n.Title = Title;
        n.Contents = Contents;
        n.IsDeleted = IsDeleted;
        n.IsHidden = IsHidden;
        n.Edited = DateTime.Now;
    }

    public News ToNews(long authorID)
    {
        return new()
        {
            AuthorID = authorID,
            Date = DateTime.Now,
            Title = Title,
            Contents = Contents,
            Source = string.Empty,
        };
    }
}