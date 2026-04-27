using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

public interface IScheduleImportService
{
    Task<ScheduleImportPreview> ParseAsync(Stream xlsxStream, long seasonID);
}

public class ScheduleImportService(LeagueSitesContext dbContext) : IScheduleImportService
{
    static readonly string[] ExpectedHeaders = ["Date", "Time", "Host", "Visitor", "Location"];

    public async Task<ScheduleImportPreview> ParseAsync(Stream xlsxStream, long seasonID)
    {
        var teams = await dbContext.Teams.AsNoTracking().ToListAsync();
        var locations = await dbContext.Locations.AsNoTracking().ToListAsync();
        var globalWarnings = new List<string>();
        var rows = new List<ScheduleImportGameRow>();

        using var workbook = new XLWorkbook(xlsxStream);
        var sheet = workbook.Worksheets.First();

        // Find header row and column mapping
        var headerRow = sheet.FirstRowUsed();
        if (headerRow is null)
        {
            globalWarnings.Add("Spreadsheet appears to be empty.");
            return new ScheduleImportPreview(seasonID, 0, 0, 0, rows, new(), globalWarnings);
        }

        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(header))
                headerMap[header] = cell.Address.ColumnNumber;
        }

        foreach (var expected in ExpectedHeaders)
        {
            if (!headerMap.ContainsKey(expected))
                globalWarnings.Add($"Missing expected column: {expected}");
        }

        if (globalWarnings.Count > 0)
            return new ScheduleImportPreview(seasonID, 0, 0, 0, rows, new(), globalWarnings);

        int validCount = 0;
        int invalidCount = 0;
        var dataRows = sheet.RowsUsed().Skip(1); // skip header

        foreach (var row in dataRows)
        {
            var warnings = new List<string>();
            var rowNum = row.RowNumber();

            string dateRaw = row.Cell(headerMap["Date"]).GetString().Trim();
            string timeRaw = row.Cell(headerMap["Time"]).GetString().Trim();
            string hostRaw = row.Cell(headerMap["Host"]).GetString().Trim();
            string visitorRaw = row.Cell(headerMap["Visitor"]).GetString().Trim();
            string locationRaw = row.Cell(headerMap["Location"]).GetString().Trim();

            // Skip entirely empty rows silently
            if (string.IsNullOrWhiteSpace(dateRaw) && string.IsNullOrWhiteSpace(hostRaw)
                && string.IsNullOrWhiteSpace(visitorRaw))
                continue;

            DateTime? parsedDate = ParseDateTime(row.Cell(headerMap["Date"]), row.Cell(headerMap["Time"]), warnings);

            var hostTeam = FuzzyMatchTeam(hostRaw, teams);
            var visitorTeam = FuzzyMatchTeam(visitorRaw, teams);
            var location = FuzzyMatchLocation(locationRaw, locations);

            if (hostTeam is null) warnings.Add($"Host team '{hostRaw}' could not be matched.");
            if (visitorTeam is null) warnings.Add($"Visiting team '{visitorRaw}' could not be matched.");
            if (location is null) warnings.Add($"Location '{locationRaw}' could not be matched.");
            if (hostTeam != null && visitorTeam != null && hostTeam.ID == visitorTeam.ID)
                warnings.Add("Host and visiting team are the same.");

            if (warnings.Count == 0) validCount++;
            else invalidCount++;

            rows.Add(new ScheduleImportGameRow(
                rowNum, dateRaw, timeRaw, hostRaw, visitorRaw, locationRaw,
                parsedDate,
                hostTeam?.ID, hostTeam?.FullName,
                visitorTeam?.ID, visitorTeam?.FullName,
                location?.ID, location?.Name,
                warnings));
        }

        var matrix = BuildMatchupMatrix(rows);
        return new ScheduleImportPreview(seasonID, rows.Count, validCount, invalidCount, rows, matrix, globalWarnings);
    }

    static DateTime? ParseDateTime(IXLCell dateCell, IXLCell timeCell, List<string> warnings)
    {
        DateTime? date = null;
        if (dateCell.DataType == XLDataType.DateTime)
        {
            date = dateCell.GetDateTime();
        }
        else
        {
            var dateStr = dateCell.GetString().Trim();
            if (DateTime.TryParse(dateStr, out var parsed))
                date = parsed.Date;
            else if (!string.IsNullOrEmpty(dateStr))
                warnings.Add($"Could not parse date '{dateStr}'.");
        }

        if (date is null) return null;

        TimeSpan time = TimeSpan.Zero;
        if (timeCell.DataType == XLDataType.DateTime)
        {
            time = timeCell.GetDateTime().TimeOfDay;
        }
        else
        {
            var timeStr = timeCell.GetString().Trim();
            if (TimeSpan.TryParse(timeStr, out var ts)) time = ts;
            else if (DateTime.TryParse(timeStr, out var dt)) time = dt.TimeOfDay;
            else if (!string.IsNullOrEmpty(timeStr)) warnings.Add($"Could not parse time '{timeStr}'.");
        }

        return date.Value.Add(time);
    }

    static Team? FuzzyMatchTeam(string input, List<Team> teams)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var needle = input.Trim();

        // Exact match on abbreviation (case-insensitive)
        var match = teams.FirstOrDefault(t => t.Abbreviation.Equals(needle, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // Exact match on full name
        match = teams.FirstOrDefault(t => t.FullName.Equals(needle, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // Exact match on name only (e.g. "Rays" → "Hillcrest Rays")
        match = teams.FirstOrDefault(t => t.Name.Equals(needle, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // Contains match on full name
        match = teams.FirstOrDefault(t => t.FullName.Contains(needle, StringComparison.OrdinalIgnoreCase));
        return match;
    }

    static Location? FuzzyMatchLocation(string input, List<Location> locations)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var needle = input.Trim();

        var match = locations.FirstOrDefault(l => l.Name.Equals(needle, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        match = locations.FirstOrDefault(l => l.FormalName != null && l.FormalName.Equals(needle, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        match = locations.FirstOrDefault(l => l.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || (l.FormalName != null && l.FormalName.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        return match;
    }

    static Dictionary<string, Dictionary<string, int>> BuildMatchupMatrix(List<ScheduleImportGameRow> rows)
    {
        var matrix = new Dictionary<string, Dictionary<string, int>>();
        foreach (var r in rows)
        {
            if (r.HostTeamName is null || r.VisitingTeamName is null) continue;
            if (!matrix.TryGetValue(r.HostTeamName, out var inner))
            {
                inner = new Dictionary<string, int>();
                matrix[r.HostTeamName] = inner;
            }
            inner[r.VisitingTeamName] = inner.GetValueOrDefault(r.VisitingTeamName, 0) + 1;
        }
        return matrix;
    }
}
