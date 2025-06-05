public class NewsForm
{
    public long NewsID { get; set; }
    public long AuthorInvitationID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Contents { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public bool IsHidden { get; set; }

    public DateTime Date { get; set; }
    public DateTime? Edited { get; set; }
    public bool IsNewNewsPost { get; set; }

    public Dictionary<Invitation, Team> InvitationTeams { get; set; } = [];

    public NewsForm() {}
    public NewsForm(User u)
    {
        IsNewNewsPost = true;

        InvitationTeams = u.Invitations.ToDictionary(i => i, i => i.Team!);
    }

    public NewsForm(News n)
    {
        IsNewNewsPost = false;

        NewsID = n.ID;
        AuthorInvitationID = n.AuthorInvitationID;
        Title = n.Title;
        Contents = n.Contents;
        IsDeleted = n.IsDeleted;
        IsHidden = n.IsHidden;

        Date = n.Date;
        Edited = n.Edited;

        InvitationTeams = n.Author!.Invitations.ToDictionary(i => i, i => i.Team!);
    }

    public void UpdateExistingNewsPost(ref News n)
    {
        n.Title = Title;
        n.AuthorInvitationID = AuthorInvitationID;
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
            AuthorInvitationID = AuthorInvitationID,
            Date = DateTime.Now,
            Title = Title,
            Contents = Contents,
            Source = string.Empty,
            IsDeleted = IsDeleted,
            IsHidden = IsHidden,
        };
    }
}