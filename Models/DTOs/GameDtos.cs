using Facet;

[Facet(typeof(Game),
    Include = [nameof(Game.ID), nameof(Game.Date), nameof(Game.ScoreHost),
               nameof(Game.ScoreVisitor), nameof(Game.HostTeam),
               nameof(Game.VisitingTeam), nameof(Game.Location),
               nameof(Game.Status), nameof(Game.Season)],
    NestedFacets = [typeof(TeamSummaryDto), typeof(LocationSummaryDto),
                    typeof(GameStatusDto), typeof(SeasonSummaryDto)])]
public partial record GameSummaryDto;

[Facet(typeof(Game),
    Include = [nameof(Game.ID), nameof(Game.SeasonID), nameof(Game.Date),
               nameof(Game.HostTeamID), nameof(Game.VisitingTeamID),
               nameof(Game.LocationID), nameof(Game.StatusID),
               nameof(Game.ScoreHost), nameof(Game.ScoreVisitor),
               nameof(Game.HostTeam), nameof(Game.VisitingTeam),
               nameof(Game.Location), nameof(Game.Status), nameof(Game.Season)],
    NestedFacets = [typeof(TeamSummaryDto), typeof(LocationSummaryDto),
                    typeof(GameStatusDto), typeof(SeasonSummaryDto)])]
public partial record GameDetailDto;

[Facet(typeof(GameStatus),
    Include = [nameof(GameStatus.ID), nameof(GameStatus.Name)])]
public partial record GameStatusDto;

public record GameUpsertDto(
    long SeasonID,
    DateTime Date,
    long HostTeamID,
    long VisitingTeamID,
    long LocationID,
    long StatusID,
    long? ScoreHost,
    long? ScoreVisitor);
