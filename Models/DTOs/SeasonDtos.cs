using Facet;

[Facet(typeof(Season),
    Include = [nameof(Season.ID), nameof(Season.Year), nameof(Season.Subseason),
               nameof(Season.Name), nameof(Season.StartDate)])]
public partial record SeasonSummaryDto;
