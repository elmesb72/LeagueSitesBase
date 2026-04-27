public class TrafficDaily
{
    public required string Date { get; set; }
    public int TotalRequests { get; set; }
    public int UniqueIPs { get; set; }
    public required string StatusCounts { get; set; }
    public required string TopPaths { get; set; }
    public required string TopReferrers { get; set; }
    public required string UpdatedAt { get; set; }
}
