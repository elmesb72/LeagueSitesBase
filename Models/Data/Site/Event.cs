public partial class Event
{
    public long ID { get; set; }
    public EventType Type { get; set; }
    public DateTime Date { get; set; }
    public long UserID { get; set; }
    public required string Resource { get; set; }
    public required string Summary { get; set; }
    public required string Description { get; set; }

    public virtual User? User { get; set; }

    public static Event Log(EventType type, long userID, string resource, string summary, string description)
    {
        return new Event()
        {
            Type = type,
            Date = DateTime.Now,
            UserID = userID,
            Resource = resource,
            Summary = summary,
            Description = description,
        };
    }
}
