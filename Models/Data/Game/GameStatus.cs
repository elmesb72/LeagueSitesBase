public partial class GameStatus
{
    public GameStatus()
    {
        Games = [];
    }

    public long ID { get; set; }
    public required string Name { get; set; }

    [JsonIgnore]
    public virtual ICollection<Game> Games { get; set; }
}