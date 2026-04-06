using Facet;

[Facet(typeof(Team),
    Include = [nameof(Team.ID), nameof(Team.Location), nameof(Team.Name),
               nameof(Team.FullName), nameof(Team.Abbreviation),
               nameof(Team.BackgroundColor), nameof(Team.Color)])]
public partial record TeamSummaryDto;

[Facet(typeof(Team),
    Include = [nameof(Team.ID), nameof(Team.Location), nameof(Team.Name),
               nameof(Team.FullName), nameof(Team.Abbreviation),
               nameof(Team.Active), nameof(Team.Hidden),
               nameof(Team.BackgroundColor), nameof(Team.Color)])]
public partial record TeamDetailDto;
