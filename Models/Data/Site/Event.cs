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

    static readonly Newtonsoft.Json.JsonSerializerSettings SerializerSettings = new()
    {
        ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
        MaxDepth = 4,
    };

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

    public static Event Log(EventType type, long userID, string resource, string summary, object payload)
    {
        string description;
        try
        {
            description = Newtonsoft.Json.JsonConvert.SerializeObject(payload, SerializerSettings);
        }
        catch (Exception ex)
        {
            description = $"[Failed to serialize payload: {ex.Message}]";
        }

        return Log(type, userID, resource, summary, description);
    }
}
