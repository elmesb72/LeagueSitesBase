public class SiteConfig
{
    public long ID { get; set; }
    public required string Name { get; set; }
    public required string ShortName { get; set; }

    /// <summary>
    /// JSON blob containing the Home section: AboutBlurb, NewsMaxAgeDays,
    /// NewsMinItems, Executives, Socials, Links, Information.
    /// Deserialized as SiteHomeConfig.
    /// </summary>
    public required string HomeJson { get; set; }

    /// <summary>
    /// JSON array of history overrides: [{ "Year": 2020, "Result": "..." }, ...]
    /// Used by the History controller to supplement DB-derived season data.
    /// </summary>
    public string HistoryJson { get; set; } = "[]";
}

public class SiteHomeConfig
{
    public string AboutBlurb { get; set; } = "";
    public int NewsMaxAgeDays { get; set; } = 30;
    public int NewsMinItems { get; set; } = 3;
    public Dictionary<string, string> Executives { get; set; } = [];
    public Dictionary<string, string> Socials { get; set; } = [];
    public Dictionary<string, string> Links { get; set; } = [];
    public Dictionary<string, string> Information { get; set; } = [];
}
