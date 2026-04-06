using Facet;

[Facet(typeof(News),
    Include = [nameof(News.ID), nameof(News.Date), nameof(News.Edited),
               nameof(News.Title), nameof(News.Contents),
               nameof(News.IsHidden), nameof(News.AuthorID), nameof(News.AuthorInvitationID),
               nameof(News.Author)],
    NestedFacets = [typeof(NewsAuthorDto)])]
public partial record NewsSummaryDto;

[Facet(typeof(User),
    Include = [nameof(User.ID), nameof(User.Name)])]
public partial record NewsAuthorDto;

public record NewsUpsertDto(
    long AuthorInvitationID,
    string Title,
    string Contents,
    bool IsHidden);
