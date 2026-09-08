/// <summary>
/// Parsing, validation, and serialization for StandingsConfig blobs, which
/// are stored per season in Season.StandingsJson. Pure static helpers: every
/// caller already has the season row (or its json) in hand, so there is no
/// resolution service — ranking code receives the config of the season that
/// owns the games being ranked.
/// </summary>
public static class StandingsConfigService
{
    static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    /// <summary>Serializes a config for storage in Season.StandingsJson (camelCase, matching Parse).</summary>
    public static string Serialize(StandingsConfig config) =>
        System.Text.Json.JsonSerializer.Serialize(config, JsonOptions);

    /// <summary>
    /// Storage-time validation for the admin save path. Returns every
    /// violation (not just the first) so the UI can show them all at once.
    /// </summary>
    public static List<string> Validate(StandingsConfig config)
    {
        var problems = new List<string>();

        foreach (var (value, label) in new[]
        {
            (config.WinsValue, "Win points"),
            (config.TiesValue, "Tie points"),
            (config.LossesValue, "Loss points"),
        })
        {
            if (value is < -100 or > 100)
                problems.Add($"{label} must be between -100 and 100.");
        }

        foreach (var (value, label) in new[]
        {
            (config.ForfeitWinnerScore, "Forfeit winner score"),
            (config.ForfeitLoserScore, "Forfeit loser score"),
        })
        {
            if (value is < 0 or > 99)
                problems.Add($"{label} must be between 0 and 99.");
        }

        if (config.ForfeitWinnerScore <= config.ForfeitLoserScore)
            problems.Add("The forfeit winner score must be higher than the loser score.");

        if (config.Tiebreakers is null || config.Tiebreakers.Count == 0)
        {
            problems.Add("At least one tiebreaker is required.");
            return problems;
        }

        foreach (var name in config.Tiebreakers.Where(t => StandingsComparators.Find(t) is null))
            problems.Add($"'{name}' is not a recognized tiebreaker.");

        var duplicates = config.Tiebreakers
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        foreach (var name in duplicates)
            problems.Add($"'{name}' appears more than once in the tiebreaker list.");

        return problems;
    }

    /// <summary>
    /// Defensive parse: absent, empty, or unparsable JSON — and unknown or
    /// empty tiebreaker lists — all resolve to workable defaults, so
    /// standings can always be computed. Storage-time validation
    /// (Validate above) lives in the Executive save path; this is the safety
    /// net for whatever is actually in the database.
    /// </summary>
    public static StandingsConfig Parse(string? json, ILogger? logger = null)
    {
        var config = new StandingsConfig();
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                config = System.Text.Json.JsonSerializer.Deserialize<StandingsConfig>(json, JsonOptions)
                    ?? new StandingsConfig();
            }
            catch (System.Text.Json.JsonException e)
            {
                logger?.LogError(e, "StandingsJson is not valid JSON; using default standings rules.");
                config = new StandingsConfig();
            }
        }

        // Drop tiebreaker names the registry doesn't know (hand-edited blobs,
        // renames); if nothing usable remains, fall back to the default rule.
        config.Tiebreakers ??= StandingsConfig.DefaultTiebreakers();
        var known = config.Tiebreakers
            .Where(t => StandingsComparators.Find(t) is not null)
            .ToList();
        if (known.Count != config.Tiebreakers.Count)
        {
            logger?.LogWarning(
                "StandingsJson contains unknown tiebreakers ({Unknown}); ignoring them.",
                string.Join(", ", config.Tiebreakers.Where(t => StandingsComparators.Find(t) is null)));
        }
        config.Tiebreakers = known.Count > 0 ? known : StandingsConfig.DefaultTiebreakers();

        return config;
    }
}
