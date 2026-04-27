public record ScheduleImportGameRow(
    int RowNumber,
    string DateRaw,
    string TimeRaw,
    string HostRaw,
    string VisitorRaw,
    string LocationRaw,
    DateTime? Date,
    long? HostTeamID,
    string? HostTeamName,
    long? VisitingTeamID,
    string? VisitingTeamName,
    long? LocationID,
    string? LocationName,
    List<string> Warnings
);

public record ScheduleImportPreview(
    long SeasonID,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    List<ScheduleImportGameRow> Games,
    // Host × Visitor matrix: dict[host abbr][visitor abbr] = count
    Dictionary<string, Dictionary<string, int>> MatchupMatrix,
    List<string> GlobalWarnings
);
