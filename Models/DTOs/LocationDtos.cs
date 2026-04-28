using Facet;

[Facet(typeof(Location),
    Include = [nameof(Location.ID), nameof(Location.Name), nameof(Location.FormalName),
               nameof(Location.City), nameof(Location.Address), nameof(Location.MapsPlaceID)])]
public partial record LocationSummaryDto;

[Facet(typeof(Location),
    Include = [nameof(Location.ID), nameof(Location.Active), nameof(Location.Name),
               nameof(Location.FormalName), nameof(Location.City),
               nameof(Location.Address), nameof(Location.MapsPlaceID)])]
public partial record LocationDetailDto;

public record LocationUpsertDto(
    string Name,
    string? FormalName,
    string City,
    string? Address,
    string? MapsPlaceID);
